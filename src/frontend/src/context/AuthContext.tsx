import { createContext, useState, useEffect, type ReactNode } from 'react'
import axios from 'axios'
import { setAccessToken, API_URL } from '../api/axios'
import { logout as apiLogout } from '../api/auth'

interface AuthContextValue {
  isAuthenticated: boolean
  loading: boolean
  login: (token: string) => void
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue>({
  isAuthenticated: false, loading: true,
  login: () => {}, logout: async () => {}
})

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [loading, setLoading] = useState(true)

  // On mount: try silent refresh to restore session
  useEffect(() => {
    axios.post<{ accessToken: string }>(`${API_URL}/auth/refresh`, {}, { withCredentials: true })
      .then(({ data }) => {
        setAccessToken(data.accessToken)
        setIsAuthenticated(true)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  const login = (token: string) => {
    setAccessToken(token)
    setIsAuthenticated(true)
  }

  const logout = async () => {
    await apiLogout()
    setIsAuthenticated(false)
  }

  return (
    <AuthContext.Provider value={{ isAuthenticated, loading, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}
