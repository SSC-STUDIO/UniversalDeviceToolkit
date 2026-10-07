import { create } from 'zustand'
import { sanitizeBridgeError } from '../../../shared/bridge/bridge'
import { DEFAULT_OSD_SETTINGS, osdApi, type OsdSettingsStore } from '../api/osd'

export interface OsdSettingsStoreState {
  settings: OsdSettingsStore
  loading: boolean
  loaded: boolean
  error: string | null
  load: () => Promise<void>
  update: (patch: Partial<OsdSettingsStore>) => Promise<boolean>
}

export const useOsdSettingsStore = create<OsdSettingsStoreState>((set, get) => {
  let saveQueue: Promise<boolean> = Promise.resolve(true)

  return {
    settings: { ...DEFAULT_OSD_SETTINGS },
    loading: false,
    loaded: false,
    error: null,

    async load() {
      if (get().loading) return
      set({ loading: true, loaded: false, error: null })
      try {
        await saveQueue
        const settings = await osdApi.get()
        set({ settings, loaded: true, error: null })
      } catch (error) {
        set({ error: sanitizeBridgeError(error) })
      } finally {
        set({ loading: false })
      }
    },

    async update(patch) {
      if (!get().loaded || get().loading) return false
      const merged: OsdSettingsStore = { ...get().settings, ...patch }
      set({ settings: merged, error: null })
      saveQueue = saveQueue.then(async () => {
        try {
          await osdApi.save(merged)
          set({ error: null })
          return true
        } catch (error) {
          set({ error: sanitizeBridgeError(error) })
          return false
        }
      })
      return saveQueue
    }
  }
})
