import { useState } from 'react'
import type { FormEvent } from 'react'
import type { Patient } from '../api/patients'

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
