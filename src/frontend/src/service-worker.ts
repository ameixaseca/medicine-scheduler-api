import { clientsClaim } from 'workbox-core'
import { precacheAndRoute } from 'workbox-precaching'

declare let self: ServiceWorkerGlobalScope

clientsClaim()
precacheAndRoute(self.__WB_MANIFEST)

self.addEventListener('push', (event: PushEvent) => {
  const data = (event.data?.json() as { title: string; body: string } | undefined) ?? { title: 'Reminder', body: '' }
  event.waitUntil(
    self.registration.showNotification(data.title, { body: data.body, icon: '/icons/icon-192.png' })
  )
})
