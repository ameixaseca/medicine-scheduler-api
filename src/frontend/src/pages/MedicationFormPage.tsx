import { useEffect, useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { getMedication, createMedication, updateMedication } from '../api/medications'
import type { MedicationPayload } from '../api/medications'
import MedicationForm from '../components/MedicationForm'

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
      await createMedication(patientId!, data)
      navigate(`/patients/${patientId}`)
    }
  }

  if (isEdit && !initial) return <p>Loading…</p>

  return (
    <main>
      <Link to={isEdit ? '#' : `/patients/${patientId}`} onClick={() => navigate(-1)}>← Back</Link>
      <h1>{isEdit ? 'Edit Medication' : 'New Medication'}</h1>
      <MedicationForm initial={initial} onSubmit={handleSubmit} submitLabel={isEdit ? 'Save' : 'Create'} />
    </main>
  )
}
