import { useEffect, useState } from 'react'
import { featuresApi } from '../api/features'
import type { FeatureKey } from '../api/features'
import { useFeaturesStore } from '../stores/featuresStore'

export interface UseFeatureResult {
  supported: boolean
  state: unknown
  states: unknown[]
  loading: boolean
  error: string | null
  setState: (state: unknown) => Promise<boolean>
  refresh: () => Promise<void>
}

export function useFeature(feature: FeatureKey): UseFeatureResult {
  const info = useFeaturesStore((s) => s.infos[feature])
  const state = useFeaturesStore((s) => s.states[feature])
  const storeLoading = useFeaturesStore((s) => s.loading)
  const storeError = useFeaturesStore((s) => s.error)
  const [states, setStates] = useState<unknown[]>([])
  const [localError, setLocalError] = useState<string | null>(null)

  const supported = info?.supported ?? false

  useEffect(() => {
    let cancelled = false
    if (!supported) return
    featuresApi
      .getStates(feature)
      .then((result) => {
        if (!cancelled) setStates(result.states)
      })
      .catch((err: unknown) => {
        if (!cancelled) setLocalError((err as Error).message)
      })
    return () => {
      cancelled = true
    }
  }, [feature, supported])

  async function setState(next: unknown): Promise<boolean> {
    try {
      return await useFeaturesStore.getState().setState(feature, next)
    } catch (err) {
      setLocalError((err as Error).message)
      return false
    }
  }

  async function refresh(): Promise<void> {
    setLocalError(null)
    await useFeaturesStore.getState().refresh(feature)
  }

  return {
    supported,
    state,
    states,
    loading: storeLoading,
    error: localError ?? storeError,
    setState,
    refresh
  }
}
