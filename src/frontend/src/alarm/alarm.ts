import { subscribe, unsubscribe } from '../api/push'

export function playAlarm() {
  const ctx = new AudioContext()
  const oscillator = ctx.createOscillator()
  const gain = ctx.createGain()
  oscillator.connect(gain)
  gain.connect(ctx.destination)
  oscillator.type = 'sine'
  oscillator.frequency.setValueAtTime(880, ctx.currentTime)
  gain.gain.setValueAtTime(0.5, ctx.currentTime)
  gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 2)
  oscillator.start()
  oscillator.stop(ctx.currentTime + 2)
}

export async function subscribeToPush() {
  const registration = await navigator.serviceWorker.ready
  const existing = await registration.pushManager.getSubscription()
  if (existing) return existing

  const sub = await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: import.meta.env.VITE_VAPID_PUBLIC_KEY
  })

  const key = sub.getKey('p256dh')
  const auth = sub.getKey('auth')

  await subscribe({
    endpoint: sub.endpoint,
    p256dh: key ? btoa(String.fromCharCode(...new Uint8Array(key))) : '',
    auth: auth ? btoa(String.fromCharCode(...new Uint8Array(auth))) : ''
  })

  return sub
}

export async function unsubscribeFromPush() {
  const registration = await navigator.serviceWorker.ready
  const sub = await registration.pushManager.getSubscription()
  if (sub) {
    await unsubscribe(sub.endpoint)
    await sub.unsubscribe()
  }
}
