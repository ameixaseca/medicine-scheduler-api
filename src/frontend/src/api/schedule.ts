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
