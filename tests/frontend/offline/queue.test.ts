import { describe, it, expect, beforeEach } from 'vitest'
import { addToQueue, getPendingQueue, removeFromQueue, incrementAttempts } from '../../../src/frontend/src/offline/queue'

describe('offline queue', () => {
  it('adds and retrieves a pending action', async () => {
    await addToQueue({ logId: 'log1', action: 'confirm', attempts: 0 })
    const queue = await getPendingQueue()
    expect(queue.some(q => q.logId === 'log1' && q.action === 'confirm')).toBe(true)
  })

  it('removes an action from the queue', async () => {
    await addToQueue({ logId: 'log2', action: 'skip', attempts: 0 })
    const before = await getPendingQueue()
    const item = before.find(q => q.logId === 'log2')!
    await removeFromQueue(item.id!)
    const after = await getPendingQueue()
    expect(after.some(q => q.logId === 'log2')).toBe(false)
  })

  it('increments attempts', async () => {
    await addToQueue({ logId: 'log3', action: 'confirm', attempts: 0 })
    const before = await getPendingQueue()
    const item = before.find(q => q.logId === 'log3')!
    await incrementAttempts(item.id!)
    const after = await getPendingQueue()
    const updated = after.find(q => q.logId === 'log3')!
    expect(updated.attempts).toBe(1)
  })
})
