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
