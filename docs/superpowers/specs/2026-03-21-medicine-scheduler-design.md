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
                 │ HTTP/REST
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
3. Scheduled job (`IHostedService`) that checks every 60 seconds for upcoming medication times and triggers reminders

### Frontend
React PWA with a Service Worker that receives push notifications even when the tab is closed. When the app is open, it plays an audio alarm via the Web Audio API in addition to showing a visual alert.

### Database
SQLite via Entity Framework Core. Simple, serverless, easy to migrate to PostgreSQL or SQL Server if needed in the future.

---

## Data Model

```
User
├── Id, Email, PasswordHash, Name
└── PushSubscriptions[]

Patient
├── Id, Name, DateOfBirth, Notes
└── UserId (FK → User)

Medication
├── Id, Name, Dosage, Unit (mg/ml/tablet/etc.)
├── ApplicationMethod (oral/injectable/topical/etc.)
├── StartDate, EndDate (nullable)
└── PatientId (FK → Patient)

MedicationSchedule
├── Id, FrequencyPerDay (e.g. 3)
├── Times[] (e.g. ["08:00", "14:00", "20:00"])
└── MedicationId (FK → Medication)

MedicationLog
├── Id, ScheduledTime, TakenAt (nullable)
├── Status (pending/taken/skipped)
└── MedicationId (FK → Medication)
```

**Key decisions:**
- `MedicationLog` entries are generated daily by the scheduled job for the following day, with status `pending`. When the caregiver confirms a dose, it becomes `taken`. If not confirmed by a reasonable window, it becomes `skipped`.
- `PushSubscription` stores the browser endpoint and VAPID keys — a user can have multiple devices registered.
- `Times[]` stored as JSON in SQLite (natively supported by EF Core).

---

## Application Flow (Frontend)

### Screen Structure

```
Login
└── Dashboard (today's schedule)
    ├── Patients
    │   ├── Patient list
    │   ├── Create/edit patient
    │   └── Patient detail
    │       ├── Medication list
    │       └── Create/edit medication
    │           └── Set frequency + adjust times
    └── Settings
        └── Notification preferences (push / alarm / both)
```

### Dashboard
Chronological list of the day's medications across all patients. Each item shows patient name, medication, scheduled time, and a "Confirm" button. Overdue medications are visually highlighted.

### Medication Registration
The caregiver enters frequency (e.g. 3x per day) and the system suggests evenly distributed times. Times can be manually adjusted before saving.

### Reminder Flow
1. Backend job detects a medication scheduled within the next 2 minutes → sends push notification via VAPID
2. Service Worker receives it → displays system notification
3. If the app is open → plays audio alarm + visual highlight
4. Caregiver clicks "Confirm" → log updated to `taken`

---

## API Endpoints

**Authentication:** JWT with refresh token. Login returns a short-lived access token (~1h) and a long-lived refresh token (~7 days) stored in an HttpOnly cookie.

```
POST   /auth/login
POST   /auth/refresh
POST   /auth/logout

GET    /patients
POST   /patients
PUT    /patients/{id}
DELETE /patients/{id}

GET    /patients/{id}/medications
POST   /patients/{id}/medications
PUT    /medications/{id}
DELETE /medications/{id}

GET    /schedule/today
GET    /schedule?date=YYYY-MM-DD
POST   /schedule/{logId}/confirm
POST   /schedule/{logId}/skip

POST   /push/subscribe
DELETE /push/subscribe
```

---

## Error Handling

### Backend
- Global exception middleware → standardized responses `{ error: string, details?: string }`
- DTO validation with FluentValidation
- Structured logging with Serilog (file + console)

### Frontend
- Axios interceptor handles 401 (token expired → attempts refresh, otherwise redirects to login)
- Toast notifications for network errors
- Basic offline cache via Service Worker for viewing the day's schedule without connectivity

---

## Testing

- **Backend:** xUnit unit tests for scheduling and push notification services
- **Frontend:** Vitest + React Testing Library for critical components (dashboard, medication form)
- **E2E:** Out of scope for initial version

---

## Out of Scope (Initial Version)

- Native mobile app (iOS/Android)
- Multi-caregiver access to the same patient
- SMS or WhatsApp notifications
- Medication inventory tracking
- Integration with external health systems
