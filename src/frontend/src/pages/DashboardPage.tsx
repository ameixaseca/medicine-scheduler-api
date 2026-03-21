import { useState, useEffect, useCallback, useRef } from 'react'
import { Link } from 'react-router-dom'
import { getTodaySchedule, confirmLog, skipLog } from '../api/schedule'
import type { ScheduleItem as Item } from '../api/schedule'
import { getSettings } from '../api/settings'
import ScheduleItemComponent from '../components/ScheduleItem'
import Toast from '../components/Toast'
import { useAuth } from '../hooks/useAuth'
import { addToQueue, getPendingQueue, removeFromQueue, incrementAttempts } from '../offline/queue'
import { playAlarm } from '../alarm/alarm'

export default function DashboardPage() {
  const [items, setItems] = useState<Item[]>([])
  const [toast, setToast] = useState<string | null>(null)
  const [pendingSyncIds, setPendingSyncIds] = useState<Set<string>>(new Set())
  const { logout } = useAuth()
  const alarmFiredRef = useRef<Set<string>>(new Set())

  const load = useCallback(async () => {
    const data = await getTodaySchedule()
    setItems(data)
    const queue = await getPendingQueue()
    setPendingSyncIds(new Set(queue.map(q => q.logId)))
  }, [])

  useEffect(() => { load() }, [load])

  // In-app alarm: check every 30s for pending items due within 1 minute
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

  // Retry queued actions when coming back online
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
