import api from './axios'
export interface Settings { notificationPreference: string; timezone: string }
export const getSettings = () => api.get<Settings>('/settings').then(r => r.data)
export const updateSettings = (body: Settings) => api.put<Settings>('/settings', body).then(r => r.data)
