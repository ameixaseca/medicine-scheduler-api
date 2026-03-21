import { useState, useEffect, useCallback } from 'react'
import { Link } from 'react-router-dom'
import { getTodaySchedule, confirmLog, skipLog } from '../api/schedule'
import type { ScheduleItem as Item } from '../api/schedule'
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
      await confirmLog(logId)
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
