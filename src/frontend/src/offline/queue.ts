export interface QueueItem {
  logId: string
  action: 'confirm' | 'skip'
}

export function getPendingQueue(): QueueItem[] {
  return []
}
