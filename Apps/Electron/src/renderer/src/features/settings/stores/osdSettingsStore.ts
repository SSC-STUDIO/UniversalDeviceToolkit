import { create } from 'zustand'
import { DEFAULT_OSD_SETTINGS, osdApi, type OsdSettingsStore } from '../api/osd'

export interface OsdSettingsStoreState {
  settings: OsdSettingsStore
  loading: boolean
  loaded: boolean
  error: string | null
  load: () => Promise<void>
  update: (patch: Partial<OsdSettingsStore>) => Promise<boolean>
}

export const useOsdSettingsStore = create<OsdSettingsStoreState>((set, get) => ({
  settings: { ...DEFAULT_OSD_SETTINGS },
  loading: false,
  loaded: false,
  error: null,

  async load() {
    if (get().loading) return
    set({ loading: true, error: null })
    try {
      const settings = await osdApi.get()
      set({ settings, loaded: true })
    } catch (error) {
      set({ error: (error as Error).message })
    } finally {
      set({ loading: false })
    }
  },

  async update(patch) {
    const merged: OsdSettingsStore = { ...get().settings, ...patch }
    set({ settings: merged, error: null })
    try {
      await osdApi.save(merged)
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  }
}))
