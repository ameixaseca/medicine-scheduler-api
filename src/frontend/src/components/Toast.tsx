import { useEffect } from 'react'

interface Props { message: string; onDismiss: () => void }

export default function Toast({ message, onDismiss }: Props) {
  useEffect(() => {
    const t = setTimeout(onDismiss, 4000)
    return () => clearTimeout(t)
  }, [onDismiss])

  return (
    <div role="alert" style={{
      position: 'fixed', bottom: 24, left: '50%', transform: 'translateX(-50%)',
      background: '#333', color: '#fff', padding: '12px 24px', borderRadius: 8
    }}>
      {message}
    </div>
  )
}
