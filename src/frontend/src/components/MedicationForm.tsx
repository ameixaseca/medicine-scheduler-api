import { useState, type FormEvent } from 'react'
import type { MedicationPayload } from '../api/medications'

interface Props {
  initial?: Partial<MedicationPayload>
  onSubmit: (data: MedicationPayload) => Promise<void>
  submitLabel: string
}

function distributeEvenly(count: number): string[] {
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
