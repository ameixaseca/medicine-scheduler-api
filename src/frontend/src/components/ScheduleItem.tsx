import type { ScheduleItem as Item } from '../api/schedule'
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
