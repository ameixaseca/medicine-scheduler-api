import api from './axios'
export interface Patient { id: string; name: string; dateOfBirth: string; notes?: string }
export const getPatients = () => api.get<Patient[]>('/patients').then(r => r.data)
export const getPatient = (id: string) => api.get<Patient>(`/patients/${id}`).then(r => r.data)
export const createPatient = (body: Omit<Patient, 'id'>) => api.post<Patient>('/patients', body).then(r => r.data)
export const updatePatient = (id: string, body: Omit<Patient, 'id'>) => api.put<Patient>(`/patients/${id}`, body).then(r => r.data)
export const deletePatient = (id: string) => api.delete(`/patients/${id}`)
