import { create } from 'zustand'
import { keyboardApi } from '../api/keyboard'
import type {
  KeyboardMode,
  RgbPreset,
  RgbState,
  SpectrumEffect,
  SpectrumLayoutResult
} from '../api/keyboard'
import {
  SPECTRUM_KEYBOARD_LAYOUTS,
  getSpectrumLayoutKeyCodes
} from '../components/keyboard/spectrum/keyboardLayouts'

/**
 * All key codes of the ANSI keyboard zone table, used to simulate a full
 * keyboard when no physical device is present (dev-only demo mode).
 */
const ANSI_KEY_CODES: number[] = getSpectrumLayoutKeyCodes(SPECTRUM_KEYBOARD_LAYOUTS.Ansi)

const SIMULATED_LAYOUT: SpectrumLayoutResult = {
  spectrumLayout: 'KeyboardOnly',
  keyboardLayout: 'Ansi',
  keys: ANSI_KEY_CODES
}

const DEMO_EFFECTS: SpectrumEffect[] = [
  {
    Type: 'RainbowScrew',
    Speed: 'Speed1',
    Direction: 'None',
    ClockwiseDirection: 'Clockwise',
    Colors: [{ R: 255, G: 255, B: 255 }],
    Keys: ANSI_KEY_CODES
  },
  {
    Type: 'ColorPulse',
    Speed: 'Speed2',
    Direction: 'None',
    ClockwiseDirection: 'None',
    Colors: [{ R: 79, G: 157, B: 247 }],
    Keys: ANSI_KEY_CODES
  }
]

export interface KeyboardSpectrumState {
  layout: SpectrumLayoutResult | null
  brightness: number
  logo: boolean
  profile: number
  effects: SpectrumEffect[]
}

export interface KeyboardStore {
  mode: KeyboardMode | null
  rgbState: RgbState | null
  spectrum: KeyboardSpectrumState
  loading: boolean
  error: string | null
  /** Dev-only demo mode: no keyboard detected, spectrum UI shows simulated data. */
  simulated: boolean
  load: () => Promise<void>
  setRgb: (state: RgbState) => Promise<boolean>
  setPreset: (preset: RgbPreset) => Promise<boolean>
  nextPreset: () => Promise<boolean>
  setBrightness: (value: number) => Promise<boolean>
  setLogo: (value: boolean) => Promise<boolean>
  setProfile: (profile: number) => Promise<boolean>
  loadProfileDesc: (profile: number) => Promise<boolean>
  saveProfileDesc: (profile: number, effects: SpectrumEffect[]) => Promise<boolean>
}

const EMPTY_SPECTRUM: KeyboardSpectrumState = {
  layout: null,
  brightness: 0,
  logo: false,
  profile: 1,
  effects: []
}

export const useKeyboardStore = create<KeyboardStore>()((set, get) => {
  let loadGeneration = 0
  let rgbWriteGeneration = 0
  let profileGeneration = 0

  return {
    mode: null,
    rgbState: null,
    spectrum: EMPTY_SPECTRUM,
    loading: false,
    error: null,
    simulated: false,

    async load() {
      const generation = ++loadGeneration
      set({ loading: true, error: null })
      try {
        const { mode } = await keyboardApi.detect()
        if (generation !== loadGeneration) return

        if (mode === 'rgb') {
          const { state } = await keyboardApi.getRgbState()
          if (generation !== loadGeneration) return
          set({ mode, simulated: false, rgbState: state })
        } else if (mode === 'spectrum') {
          const [layout, brightness, logo, profile] = await Promise.all([
            keyboardApi.spectrumGetLayout(),
            keyboardApi.spectrumGetBrightness(),
            keyboardApi.spectrumGetLogo(),
            keyboardApi.spectrumGetProfile()
          ])
          if (generation !== loadGeneration) return

          let effects: SpectrumEffect[] = []
          try {
            const desc = await keyboardApi.spectrumGetProfileDesc(profile.profile)
            effects = desc.effects
          } catch {
            // Profile description read is best-effort; the list stays empty.
          }
          if (generation !== loadGeneration) return

          set({
            mode,
            simulated: false,
            spectrum: {
              layout,
              brightness: brightness.brightness,
              logo: logo.isOn,
              profile: profile.profile,
              effects
            }
          })
        } else if (mode === 'white' || mode === 'oneLevelWhite') {
          set({ mode, simulated: false, rgbState: null, spectrum: EMPTY_SPECTRUM })
        } else if (import.meta.env.DEV) {
          // Dev-only demo mode: without a physical keyboard the spectrum
          // interface renders with simulated data so the UI can be inspected.
          set({
            mode: 'spectrum',
            simulated: true,
            spectrum: {
              layout: SIMULATED_LAYOUT,
              brightness: 6,
              logo: true,
              profile: 1,
              effects: DEMO_EFFECTS
            }
          })
        } else {
          set({ mode: 'none', simulated: false })
        }
      } catch (error) {
        if (generation === loadGeneration) set({ error: (error as Error).message })
      } finally {
        if (generation === loadGeneration) set({ loading: false })
      }
    },

    async setRgb(state) {
      if (get().simulated) {
        set({ rgbState: state })
        return true
      }
      const generation = ++rgbWriteGeneration
      try {
        const result = await keyboardApi.setRgbState(state)
        if (!result.ok) return false
        if (generation !== rgbWriteGeneration) return true
        set({ rgbState: state })
        return true
      } catch {
        return false
      }
    },

    async setPreset(preset) {
      if (get().simulated) {
        return true
      }
      const generation = ++rgbWriteGeneration
      try {
        const { state } = await keyboardApi.setPreset(preset)
        if (generation !== rgbWriteGeneration) return true
        set({ rgbState: state })
        return true
      } catch {
        return false
      }
    },

    async nextPreset() {
      if (get().simulated) {
        return true
      }
      const generation = ++rgbWriteGeneration
      try {
        const { state } = await keyboardApi.nextPreset()
        if (generation !== rgbWriteGeneration) return true
        set({ rgbState: state })
        return true
      } catch {
        return false
      }
    },

    async setBrightness(value) {
      if (get().simulated) {
        set({ spectrum: { ...get().spectrum, brightness: value } })
        return true
      }
      try {
        const result = await keyboardApi.spectrumSetBrightness(value)
        if (!result.ok) return false
        set({ spectrum: { ...get().spectrum, brightness: value } })
        return true
      } catch {
        return false
      }
    },

    async setLogo(value) {
      if (get().simulated) {
        set({ spectrum: { ...get().spectrum, logo: value } })
        return true
      }
      try {
        const result = await keyboardApi.spectrumSetLogo(value)
        if (!result.ok) return false
        set({ spectrum: { ...get().spectrum, logo: value } })
        return true
      } catch {
        return false
      }
    },

    async setProfile(profile) {
      if (get().simulated) {
        set({ spectrum: { ...get().spectrum, profile } })
        return true
      }
      const generation = ++profileGeneration
      try {
        const result = await keyboardApi.spectrumSetProfile(profile)
        if (!result.ok) return false
        if (generation !== profileGeneration) return true
        set({ spectrum: { ...get().spectrum, profile } })
        return true
      } catch {
        return false
      }
    },

    async loadProfileDesc(profile) {
      if (get().simulated) {
        return true
      }
      const generation = ++profileGeneration
      try {
        const desc = await keyboardApi.spectrumGetProfileDesc(profile)
        if (generation !== profileGeneration) return true
        set({ spectrum: { ...get().spectrum, profile: desc.profile, effects: desc.effects } })
        return true
      } catch {
        return false
      }
    },

    async saveProfileDesc(profile, effects) {
      if (get().simulated) {
        set({ spectrum: { ...get().spectrum, profile, effects } })
        return true
      }
      const generation = ++profileGeneration
      try {
        const result = await keyboardApi.spectrumSetProfileDesc(profile, effects)
        if (!result.ok) return false
        if (generation !== profileGeneration) return true
        set({ spectrum: { ...get().spectrum, profile, effects } })
        return true
      } catch {
        return false
      }
    }
  }
})
