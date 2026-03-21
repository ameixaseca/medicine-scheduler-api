import api from './axios'
export const subscribe = (sub: { endpoint: string; p256dh: string; auth: string }) =>
  api.post('/push/subscribe', sub)
export const unsubscribe = (endpoint: string) => api.post('/push/unsubscribe', { endpoint })
