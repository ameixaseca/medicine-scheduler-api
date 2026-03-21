import { createContext, type ReactNode } from 'react'

export const AuthContext = createContext<unknown>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  return <AuthContext.Provider value={null}>{children}</AuthContext.Provider>
}
