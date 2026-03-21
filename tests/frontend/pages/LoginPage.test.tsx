import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { AuthContext } from '../../../src/frontend/src/context/AuthContext'
import LoginPage from '../../../src/frontend/src/pages/LoginPage'
import { vi } from 'vitest'
import * as authApi from '../../../src/frontend/src/api/auth'

describe('LoginPage', () => {
  it('calls login and redirects on success', async () => {
    vi.spyOn(authApi, 'login').mockResolvedValue({ accessToken: 'tok', expiresIn: 3600 })
    const mockLogin = vi.fn()

    render(
      <AuthContext.Provider value={{ isAuthenticated: false, loading: false, login: mockLogin, logout: vi.fn() }}>
        <MemoryRouter>
          <LoginPage />
        </MemoryRouter>
      </AuthContext.Provider>
    )

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'u@t.com' } })
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'password123' } })
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => expect(mockLogin).toHaveBeenCalledWith('tok'))
  })

  it('shows error toast on failed login', async () => {
    vi.spyOn(authApi, 'login').mockRejectedValue(new Error('Invalid'))

    render(
      <AuthContext.Provider value={{ isAuthenticated: false, loading: false, login: vi.fn(), logout: vi.fn() }}>
        <MemoryRouter>
          <LoginPage />
        </MemoryRouter>
      </AuthContext.Provider>
    )

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'u@t.com' } })
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'wrong' } })
    fireEvent.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument())
  })
})
