import { useState, useEffect } from 'react'
import { getSettings, updateSettings } from '../api/settings'
import type { Settings } from '../api/settings'

export function useSettings() {
  const [settings, setSettings] = useState<Settings | null>(null)

  useEffect(() => { getSettings().then(setSettings) }, [])

  const save = async (data: Settings) => {
    const updated = await updateSettings(data)
    setSettings(updated)
  }

  return { settings, save }
}
