import { invoke } from './bridge'

export type KeyboardMode = 'rgb' | 'spectrum' | 'white' | 'oneLevelWhite' | 'none'

export type RgbPreset = 'Off' | 'One' | 'Two' | 'Three' | 'Four'
export type RgbEffect = 'Static' | 'Breath' | 'Smooth' | 'WaveRTL' | 'WaveLTR'
export type RgbSpeed = 'Slowest' | 'Slow' | 'Fast' | 'Fastest'
export type RgbBrightness = 'Low' | 'High'

export interface RgbColor {
  R: number
  G: number
  B: number
}

export interface RgbPresetDescription {
  Effect: RgbEffect
  Speed: RgbSpeed
  Brightness: RgbBrightness
  Zone1: RgbColor
  Zone2: RgbColor
  Zone3: RgbColor
  Zone4: RgbColor
}

export interface RgbState {
  SelectedPreset: RgbPreset
  Presets: Partial<Record<RgbPreset, RgbPresetDescription>>
}

export type SpectrumLayoutName = 'KeyboardOnly' | 'KeyboardAndFront' | 'Full' | 'FullAlternative'
export type KeyboardLayoutName = 'Ansi' | 'Iso' | 'Jis' | 'Keyboard24Zone'

export type SpectrumEffectType =
  | 'Always'
  | 'RainbowScrew'
  | 'RainbowWave'
  | 'ColorChange'
  | 'ColorWave'
  | 'ColorPulse'
  | 'Smooth'
  | 'Rain'
  | 'Ripple'
  | 'Type'
  | 'AudioBounce'
  | 'AudioRipple'
  | 'AuroraSync'

export type SpectrumSpeed = 'None' | 'Speed1' | 'Speed2' | 'Speed3'
export type SpectrumDirection = 'None' | 'BottomToTop' | 'TopToBottom' | 'LeftToRight' | 'RightToLeft'
export type SpectrumClockwiseDirection = 'None' | 'Clockwise' | 'CounterClockwise'

export interface SpectrumEffect {
  Type: SpectrumEffectType
  Speed: SpectrumSpeed
  Direction: SpectrumDirection
  ClockwiseDirection: SpectrumClockwiseDirection
  Colors: RgbColor[]
  Keys: number[]
}

export interface SpectrumLayoutResult {
  spectrumLayout: SpectrumLayoutName
  keyboardLayout: KeyboardLayoutName
  keys: number[]
}

export interface SpectrumKeyColor {
  key: number
  r: number
  g: number
  b: number
}

export interface SpectrumStateResult {
  keys: SpectrumKeyColor[]
}

export interface RgbStateResult {
  state: RgbState
}

export interface SpectrumProfileDescriptionResult {
  profile: number
  effects: SpectrumEffect[]
}

export interface KeyboardApi {
  detect(): Promise<{ mode: KeyboardMode }>
  getRgbState(): Promise<RgbStateResult>
  setRgbState(state: RgbState): Promise<{ ok: boolean }>
  setPreset(preset: RgbPreset): Promise<RgbStateResult>
  nextPreset(): Promise<RgbStateResult>
  takeOwnership(enable: boolean, restorePreset?: boolean): Promise<{ ok: boolean }>
  spectrumGetLayout(): Promise<SpectrumLayoutResult>
  spectrumGetState(): Promise<SpectrumStateResult>
  spectrumGetBrightness(): Promise<{ brightness: number }>
  spectrumSetBrightness(brightness: number): Promise<{ ok: boolean }>
  spectrumGetLogo(): Promise<{ isOn: boolean }>
  spectrumSetLogo(isOn: boolean): Promise<{ ok: boolean }>
  spectrumGetProfile(): Promise<{ profile: number }>
  spectrumSetProfile(profile: number): Promise<{ ok: boolean }>
  spectrumGetProfileDesc(profile: number): Promise<SpectrumProfileDescriptionResult>
  spectrumSetProfileDesc(profile: number, effects: SpectrumEffect[]): Promise<{ ok: boolean }>
}

function asRecord(value: unknown): Record<string, unknown> | null {
  if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
    return value as Record<string, unknown>
  }
  return null
}

function readProp(record: Record<string, unknown> | null, ...names: string[]): unknown {
  if (!record) return undefined
  for (const name of names) {
    if (Object.prototype.hasOwnProperty.call(record, name)) {
      const value = record[name]
      if (value !== undefined) return value
    }
  }
  return undefined
}

function readFiniteNumber(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isFinite(value) ? value : undefined
}

function readOk(value: unknown): { ok: boolean } {
  return { ok: readProp(asRecord(value), 'ok', 'Ok') === true }
}

function readRgbState(value: unknown): RgbStateResult {
  const state = readProp(asRecord(value), 'state', 'State')
  const record = asRecord(state)
  if (!record) throw new Error('Invalid RGB backlight state')
  return { state: state as RgbState }
}

function readKeyColor(value: unknown): SpectrumKeyColor | null {
  const record = asRecord(value)
  const key = readFiniteNumber(readProp(record, 'key', 'Key'))
  const r = readFiniteNumber(readProp(record, 'r', 'R'))
  const g = readFiniteNumber(readProp(record, 'g', 'G'))
  const b = readFiniteNumber(readProp(record, 'b', 'B'))
  if (key === undefined || r === undefined || g === undefined || b === undefined) return null
  return { key, r, g, b }
}

function readSpectrumState(value: unknown): SpectrumStateResult {
  const keysRaw = readProp(asRecord(value), 'keys', 'Keys')
  if (!Array.isArray(keysRaw)) return { keys: [] }
  const keys: SpectrumKeyColor[] = []
  for (const item of keysRaw) {
    const color = readKeyColor(item)
    if (color) keys.push(color)
  }
  return { keys }
}

function readSpectrumLayout(value: unknown): SpectrumLayoutResult {
  const record = asRecord(value)
  const spectrumLayout = readProp(record, 'spectrumLayout', 'SpectrumLayout')
  const keyboardLayout = readProp(record, 'keyboardLayout', 'KeyboardLayout')
  const keysRaw = readProp(record, 'keys', 'Keys')
  if (typeof spectrumLayout !== 'string' || typeof keyboardLayout !== 'string') {
    throw new Error('Invalid Spectrum layout')
  }
  const keys = Array.isArray(keysRaw)
    ? keysRaw.filter((code): code is number => typeof code === 'number' && Number.isFinite(code))
    : []
  return {
    spectrumLayout: spectrumLayout as SpectrumLayoutName,
    keyboardLayout: keyboardLayout as KeyboardLayoutName,
    keys
  }
}

function readProfileDescription(value: unknown): SpectrumProfileDescriptionResult {
  const record = asRecord(value)
  const profile = readFiniteNumber(readProp(record, 'profile', 'Profile'))
  const effects = readProp(record, 'effects', 'Effects')
  if (profile === undefined || !Array.isArray(effects)) {
    throw new Error('Invalid Spectrum profile description')
  }
  return { profile, effects: effects as SpectrumEffect[] }
}

export const keyboardApi: KeyboardApi = {
  async detect() {
    const raw = await invoke<unknown>('keyboard.detect', {})
    const mode = readProp(asRecord(raw), 'mode', 'Mode')
    if (
      mode !== 'rgb' &&
      mode !== 'spectrum' &&
      mode !== 'white' &&
      mode !== 'oneLevelWhite' &&
      mode !== 'none'
    ) {
      throw new Error('Invalid keyboard mode')
    }
    return { mode }
  },

  async getRgbState() {
    return readRgbState(await invoke<unknown>('rgb.getState', {}))
  },

  async setRgbState(state) {
    return readOk(await invoke<unknown>('rgb.setState', { state }))
  },

  async setPreset(preset) {
    return readRgbState(await invoke<unknown>('rgb.setPreset', { preset }))
  },

  async nextPreset() {
    return readRgbState(await invoke<unknown>('rgb.nextPreset', {}))
  },

  async takeOwnership(enable, restorePreset) {
    return readOk(
      await invoke<unknown>(
        'rgb.takeOwnership',
        restorePreset === undefined ? { enable } : { enable, restorePreset }
      )
    )
  },

  async spectrumGetLayout() {
    return readSpectrumLayout(await invoke<unknown>('spectrum.getLayout', {}))
  },

  async spectrumGetState() {
    return readSpectrumState(await invoke<unknown>('spectrum.getState', {}))
  },

  async spectrumGetBrightness() {
    const brightness = readFiniteNumber(
      readProp(asRecord(await invoke<unknown>('spectrum.getBrightness', {})), 'brightness', 'Brightness')
    )
    if (brightness === undefined) throw new Error('Invalid Spectrum brightness')
    return { brightness }
  },

  async spectrumSetBrightness(brightness) {
    return readOk(await invoke<unknown>('spectrum.setBrightness', { brightness }))
  },

  async spectrumGetLogo() {
    const isOn = readProp(asRecord(await invoke<unknown>('spectrum.getLogoStatus', {})), 'isOn', 'IsOn')
    if (typeof isOn !== 'boolean') throw new Error('Invalid Spectrum logo status')
    return { isOn }
  },

  async spectrumSetLogo(isOn) {
    return readOk(await invoke<unknown>('spectrum.setLogoStatus', { isOn }))
  },

  async spectrumGetProfile() {
    const profile = readFiniteNumber(
      readProp(asRecord(await invoke<unknown>('spectrum.getProfile', {})), 'profile', 'Profile')
    )
    if (profile === undefined) throw new Error('Invalid Spectrum profile')
    return { profile }
  },

  async spectrumSetProfile(profile) {
    return readOk(await invoke<unknown>('spectrum.setProfile', { profile }))
  },

  async spectrumGetProfileDesc(profile) {
    return readProfileDescription(await invoke<unknown>('spectrum.getProfileDescription', { profile }))
  },

  async spectrumSetProfileDesc(profile, effects) {
    return readOk(await invoke<unknown>('spectrum.setProfileDescription', { profile, effects }))
  }
}
