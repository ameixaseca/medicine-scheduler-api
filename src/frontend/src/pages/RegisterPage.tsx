import { useState, type FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { register } from '../api/auth'
import { useAuth } from '../hooks/useAuth'
import Toast from '../components/Toast'

export default function RegisterPage() {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const { login: setAuth } = useAuth()
  const navigate = useNavigate()

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    try {
      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone
      const { accessToken } = await register(name, email, password, tz)
      setAuth(accessToken)
      navigate('/')
    } catch {
      setError('Registration failed. Check your details.')
    }
  }

  return (
    <main>
      <h1>Create account</h1>
      <form onSubmit={handleSubmit}>
        <label htmlFor="name">Name</label>
        <input id="name" value={name} onChange={e => setName(e.target.value)} required />
        <label htmlFor="email">Email</label>
        <input id="email" type="email" value={email} onChange={e => setEmail(e.target.value)} required />
        <label htmlFor="password">Password</label>
        <input id="password" type="password" value={password} onChange={e => setPassword(e.target.value)} required minLength={8} />
        <button type="submit">Register</button>
      </form>
      <Link to="/login">Already have an account?</Link>
      {error && <Toast message={error} onDismiss={() => setError(null)} />}
    </main>
  )
}
