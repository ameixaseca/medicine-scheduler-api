# Medicine Scheduler — Design Spec

**Date:** 2026-03-21
**Status:** Approved

---

## Overview

A web application (PWA) for caregivers to manage medication schedules for multiple patients. The caregiver registers patients and their medications, defines administration schedules, and receives reminders when it's time to administer a dose.

**Primary user:** A caregiver or family member managing medications for multiple patients.
**Target platform:** Web (PWA — installable on mobile, works in browser).
**Future extensibility:** Additional platforms (native mobile, desktop) can be added by consuming the same API.

---

## Architecture

```
┌─────────────────────────────────────┐
│         React PWA (TypeScript)      │
│  Service Worker → Web Push API      │
│  In-app alarm (Audio API)           │
└────────────────┬────────────────────┘
                 │ HTTP/REST + Bearer token
┌────────────────▼────────────────────┐
│       ASP.NET Core Web API (C#)     │
│  Auth (JWT) │ Push Sender │ Scheduler│
└────────────────┬────────────────────┘
                 │ EF Core
┌────────────────▼────────────────────┐
│           SQLite                    │
└─────────────────────────────────────┘
```

### Backend
ASP.NET Core Web API with three main responsibilities:
1. REST API for CRUD operations on patients, medications, and schedules
2. Push notification delivery via VAPID (WebPush library)
3. Single scheduled job (`IHostedService`, 60-second interval) handling both reminder dispatch and daily log generation

### Frontend
React PWA with a Service Worker that receives push notifications even when the tab is closed. When the app is open, it plays an audio alarm via the Web Audio API and shows a visual alert (based on the user's `NotificationPreference`).

### Database
SQLite via Entity Framework Core. Simple, serverless, easy to migrate to PostgreSQL or SQL Server in the future.

---

## Authentication

**Token lifetimes:**
- Access token: exactly 1 hour (`expiresIn: 3600`)
- Refresh token: exactly 7 days

**Token transport:**
- `POST /auth/login` and `POST /auth/register` return `{ accessToken: string, expiresIn: 3600 }` in the response body, plus a `refreshToken` HttpOnly, Secure, SameSite=Strict cookie.
- The access token is stored in memory on the client (not localStorage) and attached to all API requests as `Authorization: Bearer <token>`.
- `POST /auth/refresh` reads the `refreshToken` cookie implicitly (no request body). Returns `{ accessToken, expiresIn: 3600 }` and rotates the refresh cookie (new value, same attributes, expiry reset to 7 days). Returns 401 if the cookie is absent or invalid.
- `POST /auth/logout` clears the refresh cookie server-side.

**Refresh flow:**
The Axios interceptor triggers refresh on any 401 response. On success, it retries the original request with the new token. On failure (refresh also returns 401), it clears the in-memory token and redirects to login.

**Password rules:**
Minimum 8 characters, maximum 72 characters (bcrypt limit). No other complexity requirements in v1.

---

## Data Model

```
User
├── Id, Email, PasswordHash (bcrypt), Name
├── Timezone (IANA string, e.g. "America/Sao_Paulo")
├── NotificationPreference (push | alarm | both)
└── IsDeleted (soft delete flag)

PushSubscription
├── Id, Endpoint (unique per user), P256dhKey, AuthKey
└── UserId (FK → User)

Patient
├── Id, Name (max 200 chars), DateOfBirth, Notes
├── IsDeleted (soft delete flag)
└── UserId (FK → User)

Medication
├── Id, Name (max 200 chars), Dosage (string, max 50), Unit (max 50)
├── ApplicationMethod (max 100)
├── StartDate, EndDate (nullable, inclusive — logs for ScheduledDate <= EndDate)
├── IsDeleted (soft delete flag)
└── PatientId (FK → Patient)

MedicationSchedule  [one active schedule per medication]
├── Id, FrequencyPerDay (integer, equals Times[].Count — always kept in sync)
├── Times[] (JSON array of HH:mm strings, min 1, max 24 entries)
└── MedicationId (FK → Medication, unique constraint)

MedicationScheduleSnapshot  [immutable historical record]
├── Id, FrequencyPerDay, Times[] (JSON), CreatedAt (UTC)
└── MedicationId (FK → Medication)

MedicationLog
├── Id, ScheduledTime (UTC), TakenAt (UTC, nullable)
├── Status (pending | taken | skipped)
├── SkippedBy (auto | caregiver, nullable — populated when Status = skipped)
├── NotificationSentAt (UTC, nullable — set after push is dispatched)
├── MedicationId (FK → Medication)
└── MedicationScheduleSnapshotId (FK → MedicationScheduleSnapshot)
```

---

## Key Decisions

**Soft delete.** Patients and medications are soft-deleted (`IsDeleted = true`). Associated records (schedules, logs, snapshots) are retained for history. Soft-deleted resources are excluded from all API responses and job processing. Pending future `MedicationLog` entries are hard-deleted when a medication is soft-deleted (the job should not fire reminders for deleted medications).

**FrequencyPerDay.** Stored as a computed field that always equals `Times[].Count`. The frontend displays it and allows the caregiver to adjust it (e.g., change from 2x to 3x per day, which updates the suggested times). On submission, `FrequencyPerDay` is ignored if supplied by the client — the server derives it from `Times[].Count`. `Times[]` must contain between 1 and 24 entries; entries must be valid HH:mm strings.

**MedicationScheduleSnapshot creation.** A snapshot is created (a) when a medication is first registered, and (b) whenever the schedule is updated. `MedicationLog.MedicationScheduleSnapshotId` is always populated.

**Schedule update atomicity.** Executed in a single database transaction: (1) create new snapshot, (2) update `MedicationSchedule` in place, (3) delete pending future `MedicationLog` entries, (4) generate same-day logs and next-day logs if the 23:00 threshold has already been crossed. Transaction is rolled back on any failure.

**Same-day log generation.** When a medication is registered or its schedule is updated, log entries for the current calendar day (in the user's timezone) are generated immediately — but only from the current time forward (no backfilling past hours of the same day). No retroactive logs are generated for days prior to registration.

**StartDate in the past.** If `StartDate` is a past date, only logs from the current calendar day onward are generated. Past days are not backfilled.

**Daily log generation (per-user).** On each 60-second job run, for each active user independently: if the current local time in `User.Timezone` is at or after 23:00 and no logs exist for the next calendar day, generate logs for the next day. Each user's timezone is evaluated independently.

**Timezone change.** When `User.Timezone` is updated via `PUT /settings`, all pending `MedicationLog` entries where `ScheduledTime > UTC now` are deleted and regenerated using the new timezone, in a single transaction.

**EndDate (inclusive).** Logs are generated for calendar dates where `ScheduledDate <= EndDate`. When `EndDate` is updated, pending logs with `ScheduledTime > UTC now` past the new `EndDate` are deleted.

**Notification window.** The job queries: `ScheduledTime BETWEEN (now - 1 minute) AND (now + 2 minutes) AND NotificationSentAt IS NULL AND Status = pending`. The 2-minute forward window means a push can fire slightly early — this is intentional and acceptable. After sending, `NotificationSentAt` is set. The deduplication flag ensures each entry is sent at most once.

**Job evaluation order within each run.** For each run:
1. Dispatch pending notifications (notification window query)
2. Auto-skip overdue entries (pending entries where ScheduledTime < now - 30 minutes)
3. Generate next-day logs (if 23:00 threshold reached for any user)

**NotificationPreference and push dispatch.** The backend checks `User.NotificationPreference` before sending a VAPID push:
- `push` or `both` → send push notification
- `alarm` → do NOT send push; the in-app alarm handles it when the app is open

The frontend plays an audio alarm and shows a visual alert when the app is open only if `NotificationPreference` is `alarm` or `both`.

**Push subscription uniqueness.** `PushSubscription.Endpoint` has a unique constraint per user. `POST /push/subscribe` upserts: if an identical endpoint already exists for the user, it updates the keys; otherwise it inserts.

**Skip semantics.** Auto-skip: `SkippedBy = auto`, triggered by job step 2. Caregiver skip: `SkippedBy = caregiver`, triggered via API. A late caregiver skip arriving after auto-skip is accepted: `SkippedBy` updated to `caregiver`, status remains `skipped`. A late confirm arriving after auto-skip sets `Status = taken`, `TakenAt = now` (overrides auto-skip).

**Offline sync.** Confirm/skip actions queued in IndexedDB are retried on each browser `online` event, up to 3 attempts. After 3 failures, the action is removed from the queue and an error toast is shown. Late confirms and caregiver skips are always accepted by the server as described above.

**Authorization.** Service-layer ownership checks on all resource endpoints (transitively via `Patient.UserId`). Cross-user access returns 403.

**Validation rules.**
- `Patient.Name`: required, max 200 chars
- `Patient.DateOfBirth`: required, valid date, not in the future
- `Medication.Name`: required, max 200 chars
- `Medication.Dosage`: required, max 50 chars
- `Medication.Unit`: required, max 50 chars
- `Medication.ApplicationMethod`: required, max 100 chars
- `Medication.StartDate`: required; if `EndDate` is provided, `StartDate <= EndDate`
- `MedicationSchedule.Times[]`: required, 1–24 entries, each must match `HH:mm` format, no duplicates
- `User.Email`: required, valid email format, unique
- `User.Password`: 8–72 characters

---

## Application Flow (Frontend)

### Screen Structure

```
Login / Register
└── Dashboard (today's schedule)
    ├── Patients
    │   ├── Patient list
    │   ├── Create/edit patient
    │   └── Patient detail
    │       ├── Medication list
    │       └── Create/edit medication
    │           └── Set frequency + adjust times
    └── Settings
        └── Notification preferences (push / alarm / both) + timezone
```

### Dashboard
Chronological list of the day's medications across all patients. Each item shows patient name, medication name, dosage, scheduled time (HH:mm in user's timezone), and status. Overdue items are visually highlighted. Pending items have Confirm and Skip buttons. Items with a pending offline sync show a sync indicator.

### Medication Registration and Edit
The caregiver selects frequency (e.g. 3x per day) and the system suggests evenly distributed times. Both the frequency and individual times can be adjusted. On submit, the server derives `FrequencyPerDay` from `Times[].Count`.

### Reminder Flow
1. Job (notification step) finds eligible log entry → checks `NotificationPreference`:
   - If `push` or `both`: sends VAPID push → Service Worker displays system notification
   - If `alarm`: no push sent
2. If app is open and preference is `alarm` or `both`: plays audio alarm + visual highlight
3. Caregiver confirms or skips → server updates log

---

## API Endpoints

```
POST   /auth/register             body: { name, email, password, timezone }
                                  → 201 { accessToken, expiresIn: 3600 } + refreshToken cookie
POST   /auth/login                body: { email, password }
                                  → 200 { accessToken, expiresIn: 3600 } + refreshToken cookie
POST   /auth/refresh              (reads refreshToken cookie)
                                  → 200 { accessToken, expiresIn: 3600 } + rotated cookie
POST   /auth/logout               → 204 (clears refreshToken cookie)

GET    /patients                  → 200 [{ id, name, dateOfBirth, notes }]
POST   /patients                  body: { name, dateOfBirth, notes }
                                  → 201 { id, name, dateOfBirth, notes }
GET    /patients/{id}             → 200 { id, name, dateOfBirth, notes }
PUT    /patients/{id}             body: { name, dateOfBirth, notes } (full replacement)
                                  → 200 { id, name, dateOfBirth, notes }
DELETE /patients/{id}             → 204 (soft delete)

GET    /patients/{id}/medications → 200 [{ id, name, dosage, unit, applicationMethod,
                                          startDate, endDate,
                                          schedule: { frequencyPerDay, times[] } }]
POST   /patients/{id}/medications body: { name, dosage, unit, applicationMethod,
                                          startDate, endDate, times[] }
                                  → 201 { id, name, dosage, unit, applicationMethod,
                                          startDate, endDate,
                                          schedule: { frequencyPerDay, times[] } }
GET    /medications/{id}          → 200 { id, name, dosage, unit, applicationMethod,
                                          startDate, endDate,
                                          schedule: { frequencyPerDay, times[] } }
PUT    /medications/{id}          body: { name, dosage, unit, applicationMethod,
                                          startDate, endDate (null = remove), times[] }
                                          (full replacement; frequencyPerDay derived from times[])
                                  → 200 { id, name, dosage, unit, applicationMethod,
                                          startDate, endDate,
                                          schedule: { frequencyPerDay, times[] } }
DELETE /medications/{id}          → 204 (soft delete; pending future logs hard-deleted)

GET    /schedule/today            → 200 [ ScheduleItem ]
GET    /schedule?date=YYYY-MM-DD  → 200 [ ScheduleItem ]
                                  (date interpreted in user's timezone)
POST   /schedule/{logId}/confirm  → 200 { id, status: "taken", takenAt }
POST   /schedule/{logId}/skip     → 200 { id, status: "skipped", skippedBy: "caregiver" }

POST   /push/subscribe            body: { endpoint, p256dh, auth }
                                  → 201 or 200 (upsert)
POST   /push/unsubscribe          body: { endpoint }
                                  → 204

GET    /settings                  → 200 { notificationPreference, timezone }
PUT    /settings                  body: { notificationPreference, timezone }
                                  → 200 { notificationPreference, timezone }
```

**ScheduleItem shape:**
```json
{
  "logId": "uuid",
  "scheduledTime": "2026-03-21T08:00:00Z",
  "scheduledTimeLocal": "08:00",
  "status": "pending",
  "skippedBy": null,
  "patient": { "id": "uuid", "name": "João" },
  "medication": {
    "id": "uuid",
    "name": "Losartana",
    "dosage": "50",
    "unit": "mg",
    "applicationMethod": "oral"
  }
}
```
List ordered by `scheduledTime` ascending. `scheduledTimeLocal` is formatted as `HH:mm` in the user's timezone.

All resource endpoints enforce ownership — cross-user access returns 403.

---

## Error Handling

### Backend
- Global exception middleware → `{ error: string, details?: string }`
- FluentValidation → 422 with `{ error: "Validation failed", details: [{ field, message }] }`
- Serilog structured logging (file + console)
- HTTP status semantics: 200/201/204 success; 401 unauthenticated; 403 ownership violation; 404 not found; 422 validation error; 500 unexpected error

### Frontend
- Axios interceptor: 401 → attempt refresh → retry original; refresh fails → redirect to login
- Toast notifications for network and server errors
- Service Worker offline cache for read-only access to today's schedule
- Offline confirm/skip queued in IndexedDB; retried on browser `online` event, up to 3 attempts; error toast after 3 failures

---

## Testing

- **Backend:** xUnit unit tests for scheduling logic, push deduplication, timezone conversion, EndDate boundary handling, and schedule-update transaction rollback
- **Frontend:** Vitest + React Testing Library for dashboard, medication form, and offline sync queue behavior
- **E2E:** Out of scope for initial version

---

## Out of Scope (Initial Version)

- Native mobile app (iOS/Android)
- Multi-caregiver access to the same patient
- SMS or WhatsApp notifications
- Medication inventory tracking
- Integration with external health systems
