export type MacroSource = 'Keyboard' | 'Mouse'
export type MacroDirection = 'Unknown' | 'Down' | 'Up' | 'Wheel' | 'HorizontalWheel' | 'Move'
export type MacroRecordingMode = 'Keyboard' | 'KeyboardMouse' | 'KeyboardMouseMovement'

export interface MacroEvent {
  source: MacroSource
  direction: MacroDirection
  /** Virtual key code for keyboard events, button id (1/2/3/.../wheel delta) for mouse. */
  key: number
  x: number
  y: number
  /** Delay in milliseconds since the previous event. */
  delayMs: number
}

export interface MacroSlot {
  /** Virtual key code (numpad 0x60-0x69) this sequence is bound to. */
  key: number
  source: MacroSource
  repeatCount: number
  ignoreDelays: boolean
  interruptOnOtherKey: boolean
  events: MacroEvent[]
}

export interface MacroState {
  isEnabled: boolean
  slots: MacroSlot[]
}

export interface SaveMacroSequenceParams {
  key: number
  repeatCount: number
  ignoreDelays: boolean
  interruptOnOtherKey: boolean
  events: MacroEvent[]
}

export interface MacroApi {
  getState(): Promise<MacroState>
  setEnabled(enabled: boolean): Promise<{ ok: boolean }>
  play(key: number): Promise<{ ok: boolean }>
  startRecording(mode: MacroRecordingMode, key: number): Promise<{ ok: boolean }>
  stopRecording(): Promise<{ events: MacroEvent[] }>
  saveSequence(params: SaveMacroSequenceParams): Promise<{ ok: boolean }>
  clearSequence(key: number): Promise<{ ok: boolean }>
}

export type MacroInvoke = <T>(method: string, params: unknown) => Promise<T>

function expectInvokeObject<T extends object>(value: T | null | undefined, method: string): T {
  if (value == null || typeof value !== 'object') {
    throw new Error(`Host method ${method} returned an invalid result`)
  }
  return value
}

export function createMacroApi(invoke: MacroInvoke): MacroApi {
  return {
    async getState() {
      return expectInvokeObject(await invoke<MacroState>('macro.getState', {}), 'macro.getState')
    },

    async setEnabled(enabled) {
      return expectInvokeObject(
        await invoke<{ ok: boolean }>('macro.setEnabled', { enabled }),
        'macro.setEnabled'
      )
    },

    async play(key) {
      return expectInvokeObject(await invoke<{ ok: boolean }>('macro.play', { key }), 'macro.play')
    },

    async startRecording(mode, key) {
      return expectInvokeObject(
        await invoke<{ ok: boolean }>('macro.startRecording', { mode, key }),
        'macro.startRecording'
      )
    },

    async stopRecording() {
      const result = await invoke<{ events: MacroEvent[] }>('macro.stopRecording', {})
      if (result == null || typeof result !== 'object' || !Array.isArray(result.events)) {
        throw new Error('Host method macro.stopRecording returned an invalid result')
      }
      return result
    },

    async saveSequence(params) {
      return expectInvokeObject(
        await invoke<{ ok: boolean }>('macro.saveSequence', params),
        'macro.saveSequence'
      )
    },

    async clearSequence(key) {
      return expectInvokeObject(
        await invoke<{ ok: boolean }>('macro.clearSequence', { key }),
        'macro.clearSequence'
      )
    }
  }
}
