# Medicine Scheduler — Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a React PWA (TypeScript) with JWT auth, dashboard view of today's medications, patient/medication CRUD, push subscription, in-app alarm, offline confirm/skip queue, and a Service Worker for background push notifications.

**Architecture:** Vite + React 19 SPA with the vite-plugin-pwa generating the Service Worker. Auth tokens live in React state (not localStorage). Axios handles all API calls with a 401-retry interceptor. Offline actions are queued in IndexedDB and retried on the `online` event (up to 3 attempts). The Web Audio API plays an alarm when the app is open and the user preference is `alarm` or `both`.

**Tech Stack:** Vite, React 19, TypeScript, Axios, React Router v7, vite-plugin-pwa, Workbox, Vitest, React Testing Library, IndexedDB (idb library)

---

## File Structure

```
src/frontend/
├── index.html
├── vite.config.ts
├── tsconfig.json
├── package.json
├── public/
│   └── icons/                         PWA icons
├── src/
│   ├── main.tsx
│   ├── App.tsx                        Router setup
│   ├── service-worker.ts              Workbox SW (push handler)
│   ├── api/
│   │   ├── axios.ts                   Axios instance + 401 interceptor
│   │   ├── auth.ts                    register/login/refresh/logout calls
│   │   ├── patients.ts
│   │   ├── medications.ts
│   │   ├── schedule.ts
│   │   ├── push.ts
│   │   └── settings.ts
│   ├── context/
│   │   └── AuthContext.tsx            token state, login/logout helpers
│   ├── hooks/
│   │   ├── useAuth.ts
│   │   └── useSettings.ts
│   ├── pages/
│   │   ├── LoginPage.tsx
│   │   ├── RegisterPage.tsx
│   │   ├── DashboardPage.tsx
│   │   ├── PatientsPage.tsx
│   │   ├── PatientDetailPage.tsx
│   │   ├── MedicationFormPage.tsx
│   │   └── SettingsPage.tsx
│   ├── components/
│   │   ├── PrivateRoute.tsx
│   │   ├── ScheduleItem.tsx           single log row with Confirm/Skip
│   │   ├── PatientForm.tsx
│   │   ├── MedicationForm.tsx         frequency picker + time inputs
│   │   ├── Toast.tsx
│   │   └── SyncIndicator.tsx
│   ├── offline/
│   │   └── queue.ts                   IndexedDB queue for confirm/skip
│   └── alarm/
│       └── alarm.ts                   Web Audio API alarm
tests/
└── frontend/
    ├── pages/DashboardPage.test.tsx
    ├── pages/LoginPage.test.tsx
    ├── components/MedicationForm.test.tsx
    └── offline/queue.test.ts
```

---

### Task 1: Project Setup (Vite + React + TypeScript + PWA)

**Files:**
- Create: `src/frontend/package.json`
- Create: `src/frontend/vite.config.ts`
- Create: `src/frontend/tsconfig.json`
- Create: `src/frontend/index.html`
- Create: `src/frontend/src/main.tsx`
- Create: `src/frontend/src/App.tsx`

- [ ] **Step 1: Scaffold Vite project**

```bash
cd src
npm create vite@latest frontend -- --template react-ts
cd frontend
```

- [ ] **Step 2: Install dependencies**

```bash
npm install axios react-router-dom idb
npm install -D vite-plugin-pwa workbox-window vitest @vitest/ui @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom
```

- [ ] **Step 3: Configure vite.config.ts**

```ts
// src/frontend/vite.config.ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'service-worker.ts',
      manifest: {
        name: 'Medicine Scheduler',
        short_name: 'MedSched',
        theme_color: '#ffffff',
        icons: [
          { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png' }
        ]
      },
      devOptions: { enabled: true, type: 'module' }
    })
  ],
  server: {
    proxy: { '/api': { target: 'http://localhost:5000', rewrite: path => path.replace(/^\/api/, '') } }
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
    globals: true
  }
})
```

- [ ] **Step 4: Create test setup file**

```ts
// src/frontend/src/test-setup.ts
import '@testing-library/jest-dom'
import 'fake-indexeddb/auto'  // global IndexedDB stub for all tests
```

Install the fake-indexeddb package now:

```bash
cd src/frontend && npm install -D fake-indexeddb
```

- [ ] **Step 5: Create stub service worker**

```ts
// src/frontend/src/service-worker.ts
import { clientsClaim } from 'workbox-core'
import { precacheAndRoute } from 'workbox-precaching'

declare let self: ServiceWorkerGlobalScope

clientsClaim()
precacheAndRoute(self.__WB_MANIFEST)

self.addEventListener('push', (event) => {
  const data = event.data?.json() ?? { title: 'Reminder', body: '' }
  event.waitUntil(
    self.registration.showNotification(data.title, { body: data.body, icon: '/icons/icon-192.png' })
  )
})
```

- [ ] **Step 6: Create App.tsx with router skeleton**

```tsx
// src/frontend/src/App.tsx
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider } from './context/AuthContext'
import PrivateRoute from './components/PrivateRoute'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import DashboardPage from './pages/DashboardPage'
import PatientsPage from './pages/PatientsPage'
import PatientDetailPage from './pages/PatientDetailPage'
import MedicationFormPage from './pages/MedicationFormPage'
import SettingsPage from './pages/SettingsPage'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route element={<PrivateRoute />}>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/patients" element={<PatientsPage />} />
            <Route path="/patients/:id" element={<PatientDetailPage />} />
            <Route path="/patients/:patientId/medications/new" element={<MedicationFormPage />} />
            <Route path="/medications/:id/edit" element={<MedicationFormPage />} />
            <Route path="/settings" element={<SettingsPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
```

- [ ] **Step 7: Verify dev server starts**

```bash
npm run dev
```
Expected: No errors, Vite serves on port 5173.

- [ ] **Step 8: Commit**

```bash
cd ../..
git add src/frontend/
git commit -m "chore: scaffold React PWA with Vite, TypeScript, and vite-plugin-pwa"
```

---

### Task 2: Axios Instance and Auth API

**Files:**
- Create: `src/frontend/src/api/axios.ts`
- Create: `src/frontend/src/api/auth.ts`
- Create: `src/frontend/src/context/AuthContext.tsx`
- Create: `src/frontend/src/hooks/useAuth.ts`
- Create: `src/frontend/src/components/PrivateRoute.tsx`

- [ ] **Step 1: Create Axios instance**

```ts
// src/frontend/src/api/axios.ts
import axios from 'axios'

// Token lives in memory — set by AuthContext after login/register/refresh
let accessToken: string | null = null

export function setAccessToken(token: string | null) {
  accessToken = token
}

export function getAccessToken() {
  return accessToken
}

const api = axios.create({ baseURL: '/', withCredentials: true })

api.interceptors.request.use((config) => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`
  return config
})

// 401 interceptor: attempt refresh, then retry once
let isRefreshing = false
let refreshSubscribers: ((token: string) => void)[] = []

function subscribeTokenRefresh(cb: (token: string) => void) {
  refreshSubscribers.push(cb)
}

function onTokenRefreshed(token: string) {
  refreshSubscribers.forEach(cb => cb(token))
  refreshSubscribers = []
}

api.interceptors.response.use(
  res => res,
  async (error) => {
    const original = error.config
    if (error.response?.status !== 401 || original._retry) {
      return Promise.reject(error)
    }

    if (isRefreshing) {
      return new Promise((resolve) => {
        subscribeTokenRefresh((token) => {
          original.headers.Authorization = `Bearer ${token}`
          resolve(api(original))
        })
      })
    }

    original._retry = true
    isRefreshing = true

    try {
      const { data } = await axios.post('/auth/refresh', {}, { withCredentials: true })
      setAccessToken(data.accessToken)
      onTokenRefreshed(data.accessToken)
      original.headers.Authorization = `Bearer ${data.accessToken}`
      return api(original)
    } catch {
      setAccessToken(null)
      window.location.href = '/login'
      return Promise.reject(error)
    } finally {
      isRefreshing = false
    }
  }
)

export default api
```

- [ ] **Step 2: Create auth API calls**

```ts
// src/frontend/src/api/auth.ts
import axios from 'axios'
import { setAccessToken } from './axios'

interface AuthResponse { accessToken: string; expiresIn: number }

export async function register(name: string, email: string, password: string, timezone: string) {
  const { data } = await axios.post<AuthResponse>('/auth/register',
    { name, email, password, timezone }, { withCredentials: true })
  setAccessToken(data.accessToken)
  return data
}

export async function login(email: string, password: string) {
  const { data } = await axios.post<AuthResponse>('/auth/login',
    { email, password }, { withCredentials: true })
  setAccessToken(data.accessToken)
  return data
}

export async function logout() {
  await axios.post('/auth/logout', {}, { withCredentials: true })
  setAccessToken(null)
}
```

- [ ] **Step 3: Create AuthContext**

```tsx
// src/frontend/src/context/AuthContext.tsx
import { createContext, useState, useEffect, ReactNode } from 'react'
import axios from 'axios'
import { setAccessToken } from '../api/axios'
import { logout as apiLogout } from '../api/auth'

interface AuthContextValue {
  isAuthenticated: boolean
  loading: boolean
  login: (token: string) => void
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue>({
  isAuthenticated: false, loading: true,
  login: () => {}, logout: async () => {}
})

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [loading, setLoading] = useState(true)

  // On mount: try silent refresh to restore session
  useEffect(() => {
    axios.post<{ accessToken: string }>('/auth/refresh', {}, { withCredentials: true })
      .then(({ data }) => {
        setAccessToken(data.accessToken)
        setIsAuthenticated(true)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  const login = (token: string) => {
    setAccessToken(token)
    setIsAuthenticated(true)
  }

  const logout = async () => {
    await apiLogout()
    setIsAuthenticated(false)
  }

  return (
    <AuthContext.Provider value={{ isAuthenticated, loading, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}
```

- [ ] **Step 4: Create useAuth hook**

```ts
// src/frontend/src/hooks/useAuth.ts
import { useContext } from 'react'
import { AuthContext } from '../context/AuthContext'
export const useAuth = () => useContext(AuthContext)
```

- [ ] **Step 5: Create PrivateRoute**

```tsx
// src/frontend/src/components/PrivateRoute.tsx
import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

export default function PrivateRoute() {
  const { isAuthenticated, loading } = useAuth()
  if (loading) return <div>Loading…</div>
  return isAuthenticated ? <Outlet /> : <Navigate to="/login" />
}
```

- [ ] **Step 6: Build**

```bash
cd src/frontend && npm run build
```

- [ ] **Step 7: Commit**

```bash
cd ../..
git add src/frontend/src/api/ src/frontend/src/context/ src/frontend/src/hooks/ src/frontend/src/components/PrivateRoute.tsx
git commit -m "feat: add Axios instance with 401 interceptor, AuthContext, and PrivateRoute"
```

---

### Task 3: Login and Register Pages

**Files:**
- Create: `src/frontend/src/pages/LoginPage.tsx`
- Create: `src/frontend/src/pages/RegisterPage.tsx`
- Create: `src/frontend/src/components/Toast.tsx`
- Test: `tests/frontend/pages/LoginPage.test.tsx`

- [ ] **Step 1: Create Toast component**

```tsx
// src/frontend/src/components/Toast.tsx
import { useEffect, useState } from 'react'

interface Props { message: string; onDismiss: () => void }

export default function Toast({ message, onDismiss }: Props) {
  useEffect(() => {
    const t = setTimeout(onDismiss, 4000)
    return () => clearTimeout(t)
  }, [onDismiss])

  return (
    <div role="alert" style={{
      position: 'fixed', bottom: 24, left: '50%', transform: 'translateX(-50%)',
      background: '#333', color: '#fff', padding: '12px 24px', borderRadius: 8
    }}>
      {message}
    </div>
  )
}
```

- [ ] **Step 2: Write failing LoginPage test**

```tsx
// tests/frontend/pages/LoginPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { AuthContext } from '../../../src/frontend/src/context/AuthContext'
import LoginPage from '../../../src/frontend/src/pages/LoginPage'
import { vi } from 'vitest'
import * as authApi from '../../../src/frontend/src/api/auth'

describe('LoginPage', () => {
  it('calls login and redirects on success', async () => {
    vi.spyOn(authApi, 'login').mockResolvedValue({ accessToken: 'tok', expiresIn: 3600 })
    const mockLogin = vi.fn()

    render(
      <AuthContext.Provider value={{ isAuthenticated: false, loading: false, login: mockLogin, logout: vi.fn() }}>
        <MemoryRouter>
          <LoginPage />
        </MemoryRouter>
      </AuthContext.Provider>
    )

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'u@t.com' } })
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'password123' } })
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => expect(mockLogin).toHaveBeenCalledWith('tok'))
  })

  it('shows error toast on failed login', async () => {
    vi.spyOn(authApi, 'login').mockRejectedValue(new Error('Invalid'))

    render(
      <AuthContext.Provider value={{ isAuthenticated: false, loading: false, login: vi.fn(), logout: vi.fn() }}>
        <MemoryRouter>
          <LoginPage />
        </MemoryRouter>
      </AuthContext.Provider>
    )

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'u@t.com' } })
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'wrong' } })
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument())
  })
})
```

- [ ] **Step 3: Run tests — verify they fail**

```bash
cd src/frontend && npx vitest run ../../tests/frontend/pages/LoginPage.test.tsx
```

- [ ] **Step 4: Implement LoginPage**

```tsx
// src/frontend/src/pages/LoginPage.tsx
import { useState, FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { login } from '../api/auth'
import { useAuth } from '../hooks/useAuth'
import Toast from '../components/Toast'

export default function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const { login: setAuth } = useAuth()
  const navigate = useNavigate()

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    try {
      const { accessToken } = await login(email, password)
      setAuth(accessToken)
      navigate('/')
    } catch {
      setError('Invalid email or password.')
    }
  }

  return (
    <main>
      <h1>Log in</h1>
      <form onSubmit={handleSubmit}>
        <label htmlFor="email">Email</label>
        <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)} required />
        <label htmlFor="password">Password</label>
        <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required />
        <button type="submit">Log in</button>
      </form>
      <Link to="/register">Create an account</Link>
      {error && <Toast message={error} onDismiss={() => setError(null)} />}
    </main>
  )
}
```

- [ ] **Step 5: Implement RegisterPage**

```tsx
// src/frontend/src/pages/RegisterPage.tsx
import { useState, FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { register } from '../api/auth'
import { useAuth } from '../hooks/useAuth'
import Toast from '../components/Toast'

export default function RegisterPage() {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const { login: setAuth } = useAuth()
  const navigate = useNavigate()

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    try {
      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone
      const { accessToken } = await register(name, email, password, tz)
      setAuth(accessToken)
      navigate('/')
    } catch {
      setError('Registration failed. Check your details.')
    }
  }

  return (
    <main>
      <h1>Create account</h1>
      <form onSubmit={handleSubmit}>
        <label htmlFor="name">Name</label>
        <input id="name" value={name} onChange={e => setName(e.target.value)} required />
        <label htmlFor="email">Email</label>
        <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)} required />
        <label htmlFor="password">Password</label>
        <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required minLength={8} />
        <button type="submit">Register</button>
      </form>
      <Link to="/login">Already have an account?</Link>
      {error && <Toast message={error} onDismiss={() => setError(null)} />}
    </main>
  )
}
```

- [ ] **Step 6: Run tests — verify they pass**

```bash
npx vitest run ../../tests/frontend/pages/LoginPage.test.tsx
```

- [ ] **Step 7: Commit**

```bash
cd ../..
git add src/frontend/src/pages/LoginPage.tsx src/frontend/src/pages/RegisterPage.tsx src/frontend/src/components/Toast.tsx tests/frontend/pages/LoginPage.test.tsx
git commit -m "feat: add Login and Register pages"
```

---

### Task 4: REST API Wrappers

**Files:**
- Create: `src/frontend/src/api/patients.ts`
- Create: `src/frontend/src/api/medications.ts`
- Create: `src/frontend/src/api/schedule.ts`
- Create: `src/frontend/src/api/push.ts`
- Create: `src/frontend/src/api/settings.ts`

- [ ] **Step 1: Create all API wrappers**

```ts
// src/frontend/src/api/patients.ts
import api from './axios'
export interface Patient { id: string; name: string; dateOfBirth: string; notes?: string }
export const getPatients = () => api.get<Patient[]>('/patients').then(r => r.data)
export const getPatient = (id: string) => api.get<Patient>(`/patients/${id}`).then(r => r.data)
export const createPatient = (body: Omit<Patient, 'id'>) => api.post<Patient>('/patients', body).then(r => r.data)
export const updatePatient = (id: string, body: Omit<Patient, 'id'>) => api.put<Patient>(`/patients/${id}`, body).then(r => r.data)
export const deletePatient = (id: string) => api.delete(`/patients/${id}`)
```

```ts
// src/frontend/src/api/medications.ts
import api from './axios'
export interface MedicationSchedule { frequencyPerDay: number; times: string[] }
export interface Medication {
  id: string; name: string; dosage: string; unit: string; applicationMethod: string
  startDate: string; endDate?: string; schedule: MedicationSchedule
}
export interface MedicationPayload {
  name: string; dosage: string; unit: string; applicationMethod: string
  startDate: string; endDate?: string; times: string[]
}
export const getMedications = (patientId: string) =>
  api.get<Medication[]>(`/patients/${patientId}/medications`).then(r => r.data)
export const getMedication = (id: string) => api.get<Medication>(`/medications/${id}`).then(r => r.data)
export const createMedication = (patientId: string, body: MedicationPayload) =>
  api.post<Medication>(`/patients/${patientId}/medications`, body).then(r => r.data)
export const updateMedication = (id: string, body: MedicationPayload) =>
  api.put<Medication>(`/medications/${id}`, body).then(r => r.data)
export const deleteMedication = (id: string) => api.delete(`/medications/${id}`)
```

```ts
// src/frontend/src/api/schedule.ts
import api from './axios'
export interface ScheduleItem {
  logId: string; scheduledTime: string; scheduledTimeLocal: string
  status: 'pending' | 'taken' | 'skipped'; skippedBy: string | null
  patient: { id: string; name: string }
  medication: { id: string; name: string; dosage: string; unit: string; applicationMethod: string }
}
export const getTodaySchedule = () => api.get<ScheduleItem[]>('/schedule/today').then(r => r.data)
export const getScheduleByDate = (date: string) =>
  api.get<ScheduleItem[]>('/schedule', { params: { date } }).then(r => r.data)
export const confirmLog = (logId: string) =>
  api.post(`/schedule/${logId}/confirm`).then(r => r.data)
export const skipLog = (logId: string) =>
  api.post(`/schedule/${logId}/skip`).then(r => r.data)
```

```ts
// src/frontend/src/api/push.ts
import api from './axios'
export const subscribe = (sub: { endpoint: string; p256dh: string; auth: string }) =>
  api.post('/push/subscribe', sub)
export const unsubscribe = (endpoint: string) => api.post('/push/unsubscribe', { endpoint })
```

```ts
// src/frontend/src/api/settings.ts
import api from './axios'
export interface Settings { notificationPreference: string; timezone: string }
export const getSettings = () => api.get<Settings>('/settings').then(r => r.data)
export const updateSettings = (body: Settings) => api.put<Settings>('/settings', body).then(r => r.data)
```

- [ ] **Step 2: Build**

```bash
cd src/frontend && npm run build
```

- [ ] **Step 3: Commit**

```bash
cd ../..
git add src/frontend/src/api/
git commit -m "feat: add API wrapper modules for all endpoints"
```

---

### Task 5: Dashboard Page

**Files:**
- Create: `src/frontend/src/components/ScheduleItem.tsx`
- Create: `src/frontend/src/components/SyncIndicator.tsx`
- Create: `src/frontend/src/pages/DashboardPage.tsx`
- Test: `tests/frontend/pages/DashboardPage.test.tsx`

- [ ] **Step 1: Create SyncIndicator**

```tsx
// src/frontend/src/components/SyncIndicator.tsx
export default function SyncIndicator() {
  return <span title="Pending sync" aria-label="Pending sync">⏳</span>
}
```

- [ ] **Step 2: Create ScheduleItem**

```tsx
// src/frontend/src/components/ScheduleItem.tsx
import { ScheduleItem as Item } from '../api/schedule'
import SyncIndicator from './SyncIndicator'

interface Props {
  item: Item
  pendingSync?: boolean
  onConfirm: (logId: string) => void
  onSkip: (logId: string) => void
}

export default function ScheduleItem({ item, pendingSync, onConfirm, onSkip }: Props) {
  const isOverdue = item.status === 'pending' && new Date(item.scheduledTime) < new Date()

  return (
    <div style={{ opacity: item.status !== 'pending' ? 0.6 : 1,
      borderLeft: isOverdue ? '4px solid red' : undefined }}>
      <span>{item.scheduledTimeLocal}</span>
      <span>{item.patient.name} — {item.medication.name} {item.medication.dosage}{item.medication.unit}</span>
      <span>{item.status}</span>
      {pendingSync && <SyncIndicator />}
      {item.status === 'pending' && (
        <>
          <button onClick={() => onConfirm(item.logId)}>Confirm</button>
          <button onClick={() => onSkip(item.logId)}>Skip</button>
        </>
      )}
    </div>
  )
}
```

- [ ] **Step 3: Write failing DashboardPage test**

```tsx
// tests/frontend/pages/DashboardPage.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import * as scheduleApi from '../../../src/frontend/src/api/schedule'
import DashboardPage from '../../../src/frontend/src/pages/DashboardPage'

const mockItem: scheduleApi.ScheduleItem = {
  logId: '1', scheduledTime: new Date().toISOString(), scheduledTimeLocal: '08:00',
  status: 'pending', skippedBy: null,
  patient: { id: 'p1', name: 'João' },
  medication: { id: 'm1', name: 'Losartana', dosage: '50', unit: 'mg', applicationMethod: 'oral' }
}

describe('DashboardPage', () => {
  it('renders schedule items', async () => {
    vi.spyOn(scheduleApi, 'getTodaySchedule').mockResolvedValue([mockItem])

    render(<MemoryRouter><DashboardPage /></MemoryRouter>)

    await waitFor(() => expect(screen.getByText(/Losartana/)).toBeInTheDocument())
    expect(screen.getByText(/João/)).toBeInTheDocument()
  })

  it('confirm button calls confirmLog', async () => {
    vi.spyOn(scheduleApi, 'getTodaySchedule').mockResolvedValue([mockItem])
    const confirmSpy = vi.spyOn(scheduleApi, 'confirmLog').mockResolvedValue({ id: '1', status: 'taken' })

    render(<MemoryRouter><DashboardPage /></MemoryRouter>)

    await waitFor(() => screen.getByRole('button', { name: /confirm/i }))
    fireEvent.click(screen.getByRole('button', { name: /confirm/i }))

    await waitFor(() => expect(confirmSpy).toHaveBeenCalledWith('1'))
  })
})
```

- [ ] **Step 4: Run tests — verify they fail**

```bash
cd src/frontend && npx vitest run ../../tests/frontend/pages/DashboardPage.test.tsx
```

- [ ] **Step 5: Implement DashboardPage**

> **Note:** This is the initial implementation. Task 10 will replace `handleConfirm`/`handleSkip` with offline-queue-aware versions.

```tsx
// src/frontend/src/pages/DashboardPage.tsx
import { useState, useEffect, useCallback } from 'react'
import { Link } from 'react-router-dom'
import { getTodaySchedule, confirmLog, skipLog, ScheduleItem as Item } from '../api/schedule'
import ScheduleItemComponent from '../components/ScheduleItem'
import Toast from '../components/Toast'
import { useAuth } from '../hooks/useAuth'
import { getPendingQueue } from '../offline/queue'

export default function DashboardPage() {
  const [items, setItems] = useState<Item[]>([])
  const [toast, setToast] = useState<string | null>(null)
  const [pendingSyncIds, setPendingSyncIds] = useState<Set<string>>(new Set())
  const { logout } = useAuth()

  const load = useCallback(async () => {
    const data = await getTodaySchedule()
    setItems(data)
    const queue = await getPendingQueue()
    setPendingSyncIds(new Set(queue.map(q => q.logId)))
  }, [])

  useEffect(() => { load() }, [load])

  const handleConfirm = async (logId: string) => {
    try {
      const updated = await confirmLog(logId)
      setItems(prev => prev.map(i => i.logId === logId ? { ...i, status: 'taken' } : i))
    } catch {
      setToast('Failed to confirm. Queued for retry.')
    }
  }

  const handleSkip = async (logId: string) => {
    try {
      await skipLog(logId)
      setItems(prev => prev.map(i => i.logId === logId ? { ...i, status: 'skipped', skippedBy: 'caregiver' } : i))
    } catch {
      setToast('Failed to skip. Queued for retry.')
    }
  }

  return (
    <main>
      <h1>Today's Schedule</h1>
      <nav>
        <Link to="/patients">Patients</Link>
        <Link to="/settings">Settings</Link>
        <button onClick={logout}>Log out</button>
      </nav>
      {items.length === 0 && <p>No medications scheduled today.</p>}
      {items.map(item => (
        <ScheduleItemComponent
          key={item.logId}
          item={item}
          pendingSync={pendingSyncIds.has(item.logId)}
          onConfirm={handleConfirm}
          onSkip={handleSkip}
        />
      ))}
      {toast && <Toast message={toast} onDismiss={() => setToast(null)} />}
    </main>
  )
}
```

- [ ] **Step 6: Run tests — verify they pass**

```bash
npx vitest run ../../tests/frontend/pages/DashboardPage.test.tsx
```

- [ ] **Step 7: Commit**

```bash
cd ../..
git add src/frontend/src/components/ScheduleItem.tsx src/frontend/src/components/SyncIndicator.tsx src/frontend/src/pages/DashboardPage.tsx tests/frontend/pages/DashboardPage.test.tsx
git commit -m "feat: add DashboardPage with schedule items, confirm/skip, and sync indicator"
```

---

### Task 6: Patient Pages

**Files:**
- Create: `src/frontend/src/components/PatientForm.tsx`
- Create: `src/frontend/src/pages/PatientsPage.tsx`
- Create: `src/frontend/src/pages/PatientDetailPage.tsx`

- [ ] **Step 1: Create PatientForm**

```tsx
// src/frontend/src/components/PatientForm.tsx
import { useState, FormEvent } from 'react'
import { Patient } from '../api/patients'

interface Props {
  initial?: Partial<Patient>
  onSubmit: (data: Omit<Patient, 'id'>) => Promise<void>
  submitLabel: string
}

export default function PatientForm({ initial, onSubmit, submitLabel }: Props) {
  const [name, setName] = useState(initial?.name ?? '')
  const [dateOfBirth, setDateOfBirth] = useState(initial?.dateOfBirth ?? '')
  const [notes, setNotes] = useState(initial?.notes ?? '')
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    try {
      await onSubmit({ name, dateOfBirth, notes })
    } catch {
      setError('Failed to save. Check your inputs.')
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <label htmlFor="name">Name</label>
      <input id="name" value={name} onChange={e => setName(e.target.value)} required maxLength={200} />
      <label htmlFor="dob">Date of Birth</label>
      <input id="dob" type="date" value={dateOfBirth} onChange={e => setDateOfBirth(e.target.value)} required />
      <label htmlFor="notes">Notes</label>
      <textarea id="notes" value={notes} onChange={e => setNotes(e.target.value)} />
      <button type="submit">{submitLabel}</button>
      {error && <p role="alert">{error}</p>}
    </form>
  )
}
```

- [ ] **Step 2: Create PatientsPage**

```tsx
// src/frontend/src/pages/PatientsPage.tsx
import { useState, useEffect } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { getPatients, createPatient, Patient } from '../api/patients'
import PatientForm from '../components/PatientForm'

export default function PatientsPage() {
  const [patients, setPatients] = useState<Patient[]>([])
  const [showForm, setShowForm] = useState(false)

  useEffect(() => { getPatients().then(setPatients) }, [])

  const handleCreate = async (data: Omit<Patient, 'id'>) => {
    const created = await createPatient(data)
    setPatients(prev => [...prev, created])
    setShowForm(false)
  }

  return (
    <main>
      <Link to="/">← Dashboard</Link>
      <h1>Patients</h1>
      <button onClick={() => setShowForm(v => !v)}>+ Add Patient</button>
      {showForm && <PatientForm onSubmit={handleCreate} submitLabel="Create" />}
      <ul>
        {patients.map(p => (
          <li key={p.id}>
            <Link to={`/patients/${p.id}`}>{p.name}</Link>
          </li>
        ))}
      </ul>
    </main>
  )
}
```

- [ ] **Step 3: Create PatientDetailPage**

```tsx
// src/frontend/src/pages/PatientDetailPage.tsx
import { useState, useEffect } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { getPatient, updatePatient, deletePatient, Patient } from '../api/patients'
import { getMedications, deleteMedication, Medication } from '../api/medications'
import PatientForm from '../components/PatientForm'

export default function PatientDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [patient, setPatient] = useState<Patient | null>(null)
  const [medications, setMedications] = useState<Medication[]>([])
  const [editing, setEditing] = useState(false)
  const navigate = useNavigate()

  useEffect(() => {
    if (!id) return
    getPatient(id).then(setPatient)
    getMedications(id).then(setMedications)
  }, [id])

  const handleUpdate = async (data: Omit<Patient, 'id'>) => {
    const updated = await updatePatient(id!, data)
    setPatient(updated)
    setEditing(false)
  }

  const handleDelete = async () => {
    if (!confirm('Delete this patient?')) return
    await deletePatient(id!)
    navigate('/patients')
  }

  const handleDeleteMedication = async (medId: string) => {
    await deleteMedication(medId)
    setMedications(prev => prev.filter(m => m.id !== medId))
  }

  if (!patient) return <p>Loading…</p>

  return (
    <main>
      <Link to="/patients">← Patients</Link>
      <h1>{patient.name}</h1>
      <button onClick={() => setEditing(v => !v)}>Edit</button>
      <button onClick={handleDelete}>Delete</button>
      {editing && <PatientForm initial={patient} onSubmit={handleUpdate} submitLabel="Save" />}

      <h2>Medications</h2>
      <Link to={`/patients/${id}/medications/new`}>+ Add Medication</Link>
      <ul>
        {medications.map(m => (
          <li key={m.id}>
            {m.name} — {m.dosage} {m.unit}
            <Link to={`/medications/${m.id}/edit`}>Edit</Link>
            <button onClick={() => handleDeleteMedication(m.id)}>Delete</button>
          </li>
        ))}
      </ul>
    </main>
  )
}
```

- [ ] **Step 4: Build**

```bash
cd src/frontend && npm run build
```

- [ ] **Step 5: Commit**

```bash
cd ../..
git add src/frontend/src/components/PatientForm.tsx src/frontend/src/pages/PatientsPage.tsx src/frontend/src/pages/PatientDetailPage.tsx
git commit -m "feat: add patient list, detail, and form pages"
```

---

### Task 7: Medication Form Page

**Files:**
- Create: `src/frontend/src/components/MedicationForm.tsx`
- Create: `src/frontend/src/pages/MedicationFormPage.tsx`
- Test: `tests/frontend/components/MedicationForm.test.tsx`

- [ ] **Step 1: Write failing MedicationForm test**

```tsx
// tests/frontend/components/MedicationForm.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import MedicationForm from '../../../src/frontend/src/components/MedicationForm'
import { vi } from 'vitest'

describe('MedicationForm', () => {
  it('derives frequencyPerDay from times array length', () => {
    const onSubmit = vi.fn()
    render(<MedicationForm onSubmit={onSubmit} submitLabel="Save" />)

    // Default is 1 time
    expect(screen.getAllByPlaceholderText(/HH:mm/i)).toHaveLength(1)

    // Change frequency to 3
    fireEvent.change(screen.getByLabelText(/frequency/i), { target: { value: '3' } })
    expect(screen.getAllByPlaceholderText(/HH:mm/i)).toHaveLength(3)
  })

  it('distributes times evenly when frequency changes', () => {
    render(<MedicationForm onSubmit={vi.fn()} submitLabel="Save" />)

    fireEvent.change(screen.getByLabelText(/frequency/i), { target: { value: '3' } })

    const inputs = screen.getAllByPlaceholderText(/HH:mm/i) as HTMLInputElement[]
    expect(inputs[0].value).toBe('08:00')
    expect(inputs[1].value).toBe('16:00')
    expect(inputs[2].value).toBe('00:00')
  })
})
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
cd src/frontend && npx vitest run ../../tests/frontend/components/MedicationForm.test.tsx
```

- [ ] **Step 3: Implement MedicationForm**

```tsx
// src/frontend/src/components/MedicationForm.tsx
import { useState, FormEvent } from 'react'
import { MedicationPayload } from '../api/medications'

interface Props {
  initial?: Partial<MedicationPayload>
  onSubmit: (data: MedicationPayload) => Promise<void>
  submitLabel: string
}

function distributeEvenly(count: number): string[] {
  // Distribute starting from 08:00 with equal spacing
  const spacing = Math.floor(24 / count)
  return Array.from({ length: count }, (_, i) => {
    const hour = (8 + i * spacing) % 24
    return `${String(hour).padStart(2, '0')}:00`
  })
}

export default function MedicationForm({ initial, onSubmit, submitLabel }: Props) {
  const [name, setName] = useState(initial?.name ?? '')
  const [dosage, setDosage] = useState(initial?.dosage ?? '')
  const [unit, setUnit] = useState(initial?.unit ?? '')
  const [applicationMethod, setApplicationMethod] = useState(initial?.applicationMethod ?? '')
  const [startDate, setStartDate] = useState(initial?.startDate ?? '')
  const [endDate, setEndDate] = useState(initial?.endDate ?? '')
  const [frequency, setFrequency] = useState(initial?.times?.length ?? 1)
  const [times, setTimes] = useState<string[]>(initial?.times ?? ['08:00'])
  const [error, setError] = useState<string | null>(null)

  const handleFrequencyChange = (n: number) => {
    setFrequency(n)
    setTimes(distributeEvenly(n))
  }

  const handleTimeChange = (i: number, val: string) => {
    setTimes(prev => prev.map((t, idx) => idx === i ? val : t))
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    try {
      await onSubmit({ name, dosage, unit, applicationMethod, startDate,
        endDate: endDate || undefined, times })
    } catch {
      setError('Failed to save. Check your inputs.')
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <label htmlFor="med-name">Name</label>
      <input id="med-name" value={name} onChange={e => setName(e.target.value)} required />
      <label htmlFor="dosage">Dosage</label>
      <input id="dosage" value={dosage} onChange={e => setDosage(e.target.value)} required />
      <label htmlFor="unit">Unit</label>
      <input id="unit" value={unit} onChange={e => setUnit(e.target.value)} required />
      <label htmlFor="method">Application Method</label>
      <input id="method" value={applicationMethod} onChange={e => setApplicationMethod(e.target.value)} required />
      <label htmlFor="start">Start Date</label>
      <input id="start" type="date" value={startDate} onChange={e => setStartDate(e.target.value)} required />
      <label htmlFor="end">End Date (optional)</label>
      <input id="end" type="date" value={endDate} onChange={e => setEndDate(e.target.value)} />

      <label htmlFor="freq">Frequency (times per day)</label>
      <input id="freq" type="number" min={1} max={24} value={frequency}
        onChange={e => handleFrequencyChange(Number(e.target.value))} />

      {times.map((t, i) => (
        <div key={i}>
          <label htmlFor={`time-${i}`}>Time {i + 1}</label>
          <input id={`time-${i}`} type="text" placeholder="HH:mm" value={t}
            onChange={e => handleTimeChange(i, e.target.value)} pattern="\d{2}:\d{2}" />
        </div>
      ))}

      <button type="submit">{submitLabel}</button>
      {error && <p role="alert">{error}</p>}
    </form>
  )
}
```

- [ ] **Step 4: Create MedicationFormPage**

```tsx
// src/frontend/src/pages/MedicationFormPage.tsx
import { useEffect, useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { getMedication, createMedication, updateMedication, MedicationPayload } from '../api/medications'

export default function MedicationFormPage() {
  const { patientId, id } = useParams<{ patientId?: string; id?: string }>()
  const navigate = useNavigate()
  const [initial, setInitial] = useState<MedicationPayload | undefined>()
  const isEdit = !!id

  useEffect(() => {
    if (isEdit) {
      getMedication(id!).then(m => setInitial({
        name: m.name, dosage: m.dosage, unit: m.unit,
        applicationMethod: m.applicationMethod,
        startDate: m.startDate, endDate: m.endDate, times: m.schedule.times
      }))
    }
  }, [id, isEdit])

  const handleSubmit = async (data: MedicationPayload) => {
    if (isEdit) {
      await updateMedication(id!, data)
      navigate(-1)
    } else {
      const med = await createMedication(patientId!, data)
      navigate(`/patients/${patientId}`)
    }
  }

  // Dynamically import MedicationForm to avoid rendering before initial loads
  const MedicationForm = require('../components/MedicationForm').default

  if (isEdit && !initial) return <p>Loading…</p>

  return (
    <main>
      <Link to={isEdit ? '#' : `/patients/${patientId}`} onClick={() => navigate(-1)}>← Back</Link>
      <h1>{isEdit ? 'Edit Medication' : 'New Medication'}</h1>
      <MedicationForm initial={initial} onSubmit={handleSubmit} submitLabel={isEdit ? 'Save' : 'Create'} />
    </main>
  )
}
```

- [ ] **Step 5: Run tests — verify they pass**

```bash
cd src/frontend && npx vitest run ../../tests/frontend/components/MedicationForm.test.tsx
```

- [ ] **Step 6: Commit**

```bash
cd ../..
git add src/frontend/src/components/MedicationForm.tsx src/frontend/src/pages/MedicationFormPage.tsx tests/frontend/components/MedicationForm.test.tsx
git commit -m "feat: add MedicationForm with frequency picker and MedicationFormPage"
```

---

### Task 8: Settings Page

**Files:**
- Create: `src/frontend/src/hooks/useSettings.ts`
- Create: `src/frontend/src/pages/SettingsPage.tsx`

- [ ] **Step 1: Create useSettings hook**

```ts
// src/frontend/src/hooks/useSettings.ts
import { useState, useEffect } from 'react'
import { getSettings, updateSettings, Settings } from '../api/settings'
import { requestPushPermission } from '../alarm/alarm'
import { subscribe, unsubscribe } from '../api/push'

export function useSettings() {
  const [settings, setSettings] = useState<Settings | null>(null)

  useEffect(() => { getSettings().then(setSettings) }, [])

  const save = async (data: Settings) => {
    const updated = await updateSettings(data)
    setSettings(updated)
  }

  return { settings, save }
}
```

- [ ] **Step 2: Create SettingsPage**

```tsx
// src/frontend/src/pages/SettingsPage.tsx
import { useState, FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useSettings } from '../hooks/useSettings'
import Toast from '../components/Toast'
import { subscribeToPush, unsubscribeFromPush } from '../alarm/alarm'

export default function SettingsPage() {
  const { settings, save } = useSettings()
  const [toast, setToast] = useState<string | null>(null)

  if (!settings) return <p>Loading…</p>

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const form = e.currentTarget
    const pref = (form.elements.namedItem('notifPref') as HTMLSelectElement).value
    const tz = (form.elements.namedItem('timezone') as HTMLInputElement).value
    try {
      await save({ notificationPreference: pref, timezone: tz })
      setToast('Settings saved.')
    } catch {
      setToast('Failed to save settings.')
    }
  }

  const handleEnablePush = async () => {
    try {
      await subscribeToPush()
      setToast('Push notifications enabled.')
    } catch {
      setToast('Could not enable push notifications.')
    }
  }

  return (
    <main>
      <Link to="/">← Dashboard</Link>
      <h1>Settings</h1>
      <form onSubmit={handleSubmit}>
        <label htmlFor="notifPref">Notification Preference</label>
        <select id="notifPref" name="notifPref" defaultValue={settings.notificationPreference}>
          <option value="push">Push only</option>
          <option value="alarm">In-app alarm only</option>
          <option value="both">Both</option>
        </select>
        <label htmlFor="timezone">Timezone</label>
        <input id="timezone" name="timezone" defaultValue={settings.timezone} />
        <button type="submit">Save</button>
      </form>
      <hr />
      <button onClick={handleEnablePush}>Enable Push Notifications</button>
      {toast && <Toast message={toast} onDismiss={() => setToast(null)} />}
    </main>
  )
}
```

- [ ] **Step 3: Build**

```bash
cd src/frontend && npm run build
```

- [ ] **Step 4: Commit**

```bash
cd ../..
git add src/frontend/src/hooks/useSettings.ts src/frontend/src/pages/SettingsPage.tsx
git commit -m "feat: add SettingsPage with notification preference and timezone"
```

---

### Task 9: In-App Alarm and Push Subscription

**Files:**
- Create: `src/frontend/src/alarm/alarm.ts`
- Modify: `src/frontend/src/pages/DashboardPage.tsx`

- [ ] **Step 1: Create alarm module**

```ts
// src/frontend/src/alarm/alarm.ts
import { subscribe, unsubscribe } from '../api/push'

// Play an alarm using the Web Audio API
export function playAlarm() {
  const ctx = new AudioContext()
  const oscillator = ctx.createOscillator()
  const gain = ctx.createGain()
  oscillator.connect(gain)
  gain.connect(ctx.destination)
  oscillator.type = 'sine'
  oscillator.frequency.setValueAtTime(880, ctx.currentTime)
  gain.gain.setValueAtTime(0.5, ctx.currentTime)
  gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 2)
  oscillator.start()
  oscillator.stop(ctx.currentTime + 2)
}

// Subscribe to push notifications via VAPID
export async function subscribeToPush() {
  const registration = await navigator.serviceWorker.ready
  const existing = await registration.pushManager.getSubscription()
  if (existing) return existing

  const sub = await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: import.meta.env.VITE_VAPID_PUBLIC_KEY
  })

  const key = sub.getKey('p256dh')
  const auth = sub.getKey('auth')

  await subscribe({
    endpoint: sub.endpoint,
    p256dh: key ? btoa(String.fromCharCode(...new Uint8Array(key))) : '',
    auth: auth ? btoa(String.fromCharCode(...new Uint8Array(auth))) : ''
  })

  return sub
}

export async function unsubscribeFromPush() {
  const registration = await navigator.serviceWorker.ready
  const sub = await registration.pushManager.getSubscription()
  if (sub) {
    await unsubscribe(sub.endpoint)
    await sub.unsubscribe()
  }
}
```

- [ ] **Step 2: Wire in-app alarm to DashboardPage**

Add to `DashboardPage.tsx` — import the alarm and settings, and play it when a pending log is due:

```tsx
// Add inside DashboardPage, after the load useEffect:
import { playAlarm } from '../alarm/alarm'
import { getSettings } from '../api/settings'

// Track which logIds have already triggered the alarm to avoid repeating
const alarmFiredRef = useRef<Set<string>>(new Set())

// Add this useEffect inside DashboardPage:
useEffect(() => {
  const check = setInterval(async () => {
    try {
      const settings = await getSettings()
      if (settings.notificationPreference === 'push') return

      const now = new Date()
      const dueItems = items.filter(i =>
        i.status === 'pending' &&
        !alarmFiredRef.current.has(i.logId) &&
        Math.abs(new Date(i.scheduledTime).getTime() - now.getTime()) < 60_000
      )
      if (dueItems.length > 0) {
        dueItems.forEach(i => alarmFiredRef.current.add(i.logId))
        playAlarm()
      }
    } catch {
      // Silently ignore — alarm is best-effort when offline
    }
  }, 30_000)

  return () => clearInterval(check)
}, [items])
```

- [ ] **Step 3: Add VAPID_PUBLIC_KEY to env**

```
# src/frontend/.env.local
VITE_VAPID_PUBLIC_KEY=your_vapid_public_key_here
```

- [ ] **Step 4: Build**

```bash
cd src/frontend && npm run build
```

- [ ] **Step 5: Commit**

```bash
cd ../..
git add src/frontend/src/alarm/alarm.ts src/frontend/src/pages/DashboardPage.tsx src/frontend/.env.local
git commit -m "feat: add in-app alarm and push subscription via VAPID"
```

---

### Task 10: Offline Sync Queue

**Files:**
- Create: `src/frontend/src/offline/queue.ts`
- Modify: `src/frontend/src/pages/DashboardPage.tsx`
- Test: `tests/frontend/offline/queue.test.ts`

- [ ] **Step 1: Write failing queue test**

`fake-indexeddb` is already installed and globally configured in `test-setup.ts` (Task 1, Step 4).

```ts
// tests/frontend/offline/queue.test.ts
import { describe, it, expect, beforeEach } from 'vitest'
import { addToQueue, getPendingQueue, removeFromQueue, incrementAttempts } from '../../../src/frontend/src/offline/queue'

describe('offline queue', () => {
  it('adds and retrieves a pending action', async () => {
    await addToQueue({ logId: 'log1', action: 'confirm', attempts: 0 })
    const queue = await getPendingQueue()
    expect(queue.some(q => q.logId === 'log1' && q.action === 'confirm')).toBe(true)
  })

  it('removes an action from the queue', async () => {
    await addToQueue({ logId: 'log2', action: 'skip', attempts: 0 })
    const before = await getPendingQueue()
    const item = before.find(q => q.logId === 'log2')!
    await removeFromQueue(item.id!)
    const after = await getPendingQueue()
    expect(after.some(q => q.logId === 'log2')).toBe(false)
  })

  it('increments attempts', async () => {
    await addToQueue({ logId: 'log3', action: 'confirm', attempts: 0 })
    const before = await getPendingQueue()
    const item = before.find(q => q.logId === 'log3')!
    await incrementAttempts(item.id!)
    const after = await getPendingQueue()
    const updated = after.find(q => q.logId === 'log3')!
    expect(updated.attempts).toBe(1)
  })
})
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
npx vitest run ../../tests/frontend/offline/queue.test.ts
```
Expected: Compilation error (queue module not found).

- [ ] **Step 3: Implement queue.ts**

```ts
// src/frontend/src/offline/queue.ts
import { openDB, IDBPDatabase } from 'idb'

interface QueueItem {
  id?: number
  logId: string
  action: 'confirm' | 'skip'
  attempts: number
}

const DB_NAME = 'medicine-scheduler'
const STORE = 'offline-queue'

async function getDb(): Promise<IDBPDatabase> {
  return openDB(DB_NAME, 1, {
    upgrade(db) {
      if (!db.objectStoreNames.contains(STORE)) {
        db.createObjectStore(STORE, { keyPath: 'id', autoIncrement: true })
      }
    }
  })
}

export async function addToQueue(item: QueueItem): Promise<void> {
  const db = await getDb()
  await db.add(STORE, item)
}

export async function getPendingQueue(): Promise<QueueItem[]> {
  const db = await getDb()
  return db.getAll(STORE)
}

export async function removeFromQueue(id: number): Promise<void> {
  const db = await getDb()
  await db.delete(STORE, id)
}

export async function incrementAttempts(id: number): Promise<void> {
  const db = await getDb()
  const item = await db.get(STORE, id)
  if (item) await db.put(STORE, { ...item, attempts: item.attempts + 1 })
}
```

- [ ] **Step 4: Wire retry-on-online into DashboardPage**

Add to `DashboardPage.tsx`:

```tsx
import { addToQueue, getPendingQueue, removeFromQueue, incrementAttempts } from '../offline/queue'
import { confirmLog, skipLog } from '../api/schedule'

// Replace handleConfirm/handleSkip with queue-aware versions:
const handleConfirm = async (logId: string) => {
  try {
    await confirmLog(logId)
    setItems(prev => prev.map(i => i.logId === logId ? { ...i, status: 'taken' } : i))
  } catch {
    await addToQueue({ logId, action: 'confirm', attempts: 0 })
    setPendingSyncIds(prev => new Set([...prev, logId]))
    setToast('Offline. Action queued.')
  }
}

const handleSkip = async (logId: string) => {
  try {
    await skipLog(logId)
    setItems(prev => prev.map(i => i.logId === logId ? { ...i, status: 'skipped', skippedBy: 'caregiver' } : i))
  } catch {
    await addToQueue({ logId, action: 'skip', attempts: 0 })
    setPendingSyncIds(prev => new Set([...prev, logId]))
    setToast('Offline. Action queued.')
  }
}

// Add retry effect:
useEffect(() => {
  const retry = async () => {
    const queue = await getPendingQueue()
    for (const item of queue) {
      if (item.attempts >= 3) {
        await removeFromQueue(item.id!)
        setToast('Some offline actions failed permanently.')
        continue
      }
      try {
        if (item.action === 'confirm') await confirmLog(item.logId)
        else await skipLog(item.logId)
        await removeFromQueue(item.id!)
        setPendingSyncIds(prev => { const s = new Set(prev); s.delete(item.logId); return s })
      } catch {
        await incrementAttempts(item.id!)
      }
    }
    load()
  }

  window.addEventListener('online', retry)
  return () => window.removeEventListener('online', retry)
}, [load])
```

- [ ] **Step 5: Add DashboardPage offline retry integration test**

Add to `tests/frontend/pages/DashboardPage.test.tsx`:

```tsx
it('retries queued confirm when online event fires', async () => {
  vi.spyOn(scheduleApi, 'getTodaySchedule').mockResolvedValue([mockItem])
  // First call fails (offline), retry succeeds
  const confirmSpy = vi.spyOn(scheduleApi, 'confirmLog')
    .mockRejectedValueOnce(new Error('offline'))
    .mockResolvedValueOnce({ id: '1', status: 'taken' })

  render(<MemoryRouter><DashboardPage /></MemoryRouter>)
  await waitFor(() => screen.getByRole('button', { name: /confirm/i }))

  // Click while "offline" → queues the action
  fireEvent.click(screen.getByRole('button', { name: /confirm/i }))
  await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument())

  // Simulate coming back online → retry fires
  window.dispatchEvent(new Event('online'))
  await waitFor(() => expect(confirmSpy).toHaveBeenCalledTimes(2))
})
```

- [ ] **Step 6: Run queue tests — verify they pass**

```bash
cd src/frontend && npx vitest run ../../tests/frontend/offline/queue.test.ts ../../tests/frontend/pages/DashboardPage.test.tsx
```

- [ ] **Step 7: Run all frontend tests**

```bash
npx vitest run
```
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
cd ../..
git add src/frontend/src/offline/queue.ts src/frontend/src/pages/DashboardPage.tsx tests/frontend/offline/queue.test.ts tests/frontend/pages/DashboardPage.test.tsx
git commit -m "feat: add offline sync queue with IndexedDB and online-retry logic"
```

---

## Summary

| Task | What it delivers |
|------|-----------------|
| 1 | Vite + React + TypeScript + PWA scaffold, Service Worker stub |
| 2 | Axios instance with 401-retry interceptor, AuthContext, PrivateRoute |
| 3 | Login and Register pages with Toast error feedback |
| 4 | REST API wrappers for all endpoints |
| 5 | DashboardPage: today's schedule, confirm/skip, overdue highlight |
| 6 | Patient list, detail, and create/edit pages |
| 7 | MedicationForm with frequency picker and evenly-distributed times |
| 8 | SettingsPage with notification preference and timezone |
| 9 | In-app alarm (Web Audio API) + push subscription (VAPID) |
| 10 | Offline queue (IndexedDB) with up-to-3-retry on `online` event |
