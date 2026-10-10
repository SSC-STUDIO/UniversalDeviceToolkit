import { create } from 'zustand'
import type {
  MacroApi,
  MacroEvent,
  MacroRecordingMode,
  MacroState,
  SaveMacroSequenceParams
} from '../api/macroClient'

export interface MacroStore {
  state: MacroState
  loaded: boolean
  loading: boolean
  error: string | null
  load: () => Promise<void>
  setEnabled: (enabled: boolean) => Promise<boolean>
  play: (key: number) => Promise<boolean>
  startRecording: (mode: MacroRecordingMode, key: number) => Promise<boolean>
  stopRecording: () => Promise<MacroEvent[] | null>
  saveSequence: (params: SaveMacroSequenceParams) => Promise<boolean>
  clearSequence: (key: number) => Promise<boolean>
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

export function createMacroStore(api: MacroApi) {
  return create<MacroStore>()((set, get) => ({
    state: { isEnabled: false, slots: [] },
    loaded: false,
    loading: false,
    error: null,

    async load() {
      if (get().loading) return
      set({ loading: true, error: null })
      try {
        const state = await api.getState()
        set({ state, loaded: true })
      } catch (error) {
        set({ error: errorMessage(error) })
      } finally {
        set({ loading: false })
      }
    },

    async setEnabled(enabled) {
      set({ error: null })
      try {
        const result = await api.setEnabled(enabled)
        if (!result.ok) {
          set({ error: 'Macro state change was rejected.' })
          return false
        }
        set({ state: { ...get().state, isEnabled: enabled } })
        return true
      } catch (error) {
        set({ error: errorMessage(error) })
        return false
      }
    },

    async play(key) {
      set({ error: null })
      try {
        const result = await api.play(key)
        if (!result.ok) {
          set({ error: 'Macro playback was rejected.' })
          return false
        }
        return true
      } catch (error) {
        set({ error: errorMessage(error) })
        return false
      }
    },

    async startRecording(mode, key) {
      set({ error: null })
      try {
        const result = await api.startRecording(mode, key)
        if (!result.ok) {
          set({ error: 'Macro recording start was rejected.' })
          return false
        }
        return true
      } catch (error) {
        set({ error: errorMessage(error) })
        return false
      }
    },

    async stopRecording() {
      set({ error: null })
      try {
        const result = await api.stopRecording()
        return result.events
      } catch (error) {
        set({ error: errorMessage(error) })
        return null
      }
    },

    async saveSequence(params) {
      if (params.events.length === 0) {
        return get().clearSequence(params.key)
      }

      set({ error: null })
      try {
        const result = await api.saveSequence(params)
        if (!result.ok) {
          set({ error: 'Macro sequence save was rejected.' })
          return false
        }
        await get().load()
        return get().error === null
      } catch (error) {
        set({ error: errorMessage(error) })
        return false
      }
    },

    async clearSequence(key) {
      set({ error: null })
      try {
        const result = await api.clearSequence(key)
        if (!result.ok) {
          set({ error: 'Macro sequence clear was rejected.' })
          return false
        }
        await get().load()
        return get().error === null
      } catch (error) {
        set({ error: errorMessage(error) })
        return false
      }
    }
  }))
}
