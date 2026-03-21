import { useState, useEffect } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { getPatient, updatePatient, deletePatient } from '../api/patients'
import type { Patient } from '../api/patients'
import { getMedications, deleteMedication } from '../api/medications'
import type { Medication } from '../api/medications'
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
