import { useCallback, useEffect, useState } from 'react'
import {
  dashboardHardwareApi,
  type DashboardHardwareState
} from '../api/dashboardHardware'
import { subscribeUiVisibility } from '../utils/uiVisibility'

export interface DashboardHardwareResult {
  state: DashboardHardwareState | null
  error: string | null
  refresh: () => Promise<void>
}

export function useDashboardHardware(): DashboardHardwareResult {
  const [state, setState] = useState<DashboardHardwareState | null>(null)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async (): Promise<void> => {
    try {
      const next = await dashboardHardwareApi.getState()
      setState(next)
      setError(null)
    } catch (reason) {
      setError((reason as Error).message)
    }
  }, [])

  useEffect(() => {
    let cancelled = false
    let monitoring = false
    const setMonitoring = (enabled: boolean): void => {
      if (monitoring === enabled) return
      monitoring = enabled
      void dashboardHardwareApi.setMonitoring(enabled).catch(() => undefined)
    }
    const start = (): void => {
      setMonitoring(true)
      if (!cancelled) void refresh()
    }
    const stop = (): void => {
      setMonitoring(false)
    }

    if (!document.hidden) start()
    const interval = window.setInterval(() => {
      if (!document.hidden) void refresh()
    }, 5_000)
    const unsubscribeVisibility = subscribeUiVisibility((active) => {
      if (active) start()
      else stop()
    })
    return () => {
      cancelled = true
      window.clearInterval(interval)
      unsubscribeVisibility()
      stop()
    }
  }, [refresh])

  return { state, error, refresh }
}
