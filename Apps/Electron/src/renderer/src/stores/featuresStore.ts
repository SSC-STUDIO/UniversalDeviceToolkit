import { create } from 'zustand'
import { featuresApi } from '../api/features'
import type { FeatureInfo, FeatureKey } from '../api/features'

export interface FeaturesStore {
  infos: Partial<Record<FeatureKey, FeatureInfo>>
  states: Partial<Record<FeatureKey, unknown>>
  loaded: boolean
  loading: boolean
  error: string | null
  load: () => Promise<void>
  refresh: (feature: FeatureKey) => Promise<void>
  setState: (feature: FeatureKey, state: unknown) => Promise<boolean>
}

export const useFeaturesStore = create<FeaturesStore>()((set, get) => ({
  infos: {},
  states: {},
  loaded: false,
  loading: false,
  error: null,

  async load() {
    if (get().loading) return
    set({ loading: true, error: null })
    try {
      const infos = await featuresApi.list()
      const infosMap: Partial<Record<FeatureKey, FeatureInfo>> = {}
      for (const info of infos) infosMap[info.key] = info

      const states: Partial<Record<FeatureKey, unknown>> = {}
      await Promise.all(
        infos
          .filter((info) => info.supported)
          .map(async (info) => {
            try {
              const result = await featuresApi.getState(info.key)
              states[info.key] = result.state
            } catch {
              // A single feature probe failure must not break the whole load.
            }
          }),
      )

      set({ infos: infosMap, states, loaded: true })
    } catch (error) {
      set({ error: (error as Error).message })
    } finally {
      set({ loading: false })
    }
  },

  async refresh(feature) {
    try {
      const result = await featuresApi.getState(feature)
      set({ states: { ...get().states, [feature]: result.state } })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  async setState(feature, state) {
    try {
      const result = await featuresApi.setState(feature, state)
      if (!result.ok) return false
      set({ states: { ...get().states, [feature]: state } })
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },
}))
