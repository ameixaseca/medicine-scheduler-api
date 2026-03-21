import { openDB } from 'idb'
import type { IDBPDatabase } from 'idb'

export interface QueueItem {
  id?: number
  logId: string
  action: 'confirm' | 'skip'
  attempts: number
}

const DB_NAME = 'medicine-scheduler'
const STORE = 'offline-queue'

async function getDb(): Promise<IDBPDatabase> {
  return openDB(DB_NAME, 1, {
    upgrade(db) {
      if (!db.objectStoreNames.contains(STORE)) {
        db.createObjectStore(STORE, { keyPath: 'id', autoIncrement: true })
      }
    }
  })
}

export async function addToQueue(item: Omit<QueueItem, 'id'>): Promise<void> {
  const db = await getDb()
  await db.add(STORE, item)
}

export async function getPendingQueue(): Promise<QueueItem[]> {
  const db = await getDb()
  return db.getAll(STORE)
}

export async function removeFromQueue(id: number): Promise<void> {
  const db = await getDb()
  await db.delete(STORE, id)
}

export async function incrementAttempts(id: number): Promise<void> {
  const db = await getDb()
  const item = await db.get(STORE, id)
  if (item) await db.put(STORE, { ...item, attempts: item.attempts + 1 })
}
