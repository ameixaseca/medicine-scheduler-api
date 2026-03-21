import axios from 'axios'
import { setAccessToken } from './axios'

interface AuthResponse { accessToken: string; expiresIn: number }

export async function register(name: string, email: string, password: string, timezone: string) {
  const { data } = await axios.post<AuthResponse>('/auth/register',
    { name, email, password, timezone }, { withCredentials: true })
  setAccessToken(data.accessToken)
  return data
}

export async function login(email: string, password: string) {
  const { data } = await axios.post<AuthResponse>('/auth/login',
    { email, password }, { withCredentials: true })
  setAccessToken(data.accessToken)
  return data
}

export async function logout() {
  await axios.post('/auth/logout', {}, { withCredentials: true })
  setAccessToken(null)
}
