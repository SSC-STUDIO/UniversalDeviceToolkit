import type { MacroEvent, MacroSlot } from '../../api/macroClient'

export const NUMPAD_VIRTUAL_KEY_MIN = 0x60
export const NUMPAD_VIRTUAL_KEY_MAX = 0x69

export const NUMPAD_LAYOUT: ReadonlyArray<ReadonlyArray<number | null>> = [
  [0x67, 0x68, 0x69],
  [0x64, 0x65, 0x66],
  [0x61, 0x62, 0x63],
  [null, 0x60, null]
]

const VK_NAMES: Readonly<Record<number, string>> = {
  0x08: 'Backspace',
  0x09: 'Tab',
  0x0d: 'Enter',
  0x10: 'Shift',
  0x11: 'Ctrl',
  0x12: 'Alt',
  0x13: 'Pause',
  0x14: 'CapsLock',
  0x1b: 'Esc',
  0x20: 'Space',
  0x21: 'PageUp',
  0x22: 'PageDown',
  0x23: 'End',
  0x24: 'Home',
  0x25: 'Left',
  0x26: 'Up',
  0x27: 'Right',
  0x28: 'Down',
  0x2d: 'Insert',
  0x2e: 'Delete',
  0x5b: 'LWin',
  0x5c: 'RWin',
  0x5d: 'Menu',
  0x90: 'NumLock',
  0x91: 'ScrollLock',
  0xba: ';',
  0xbb: '=',
  0xbc: ',',
  0xbd: '-',
  0xbe: '.',
  0xbf: '/',
  0xc0: '`',
  0xdb: '[',
  0xdc: '\\',
  0xdd: ']',
  0xde: "'"
}

export interface MacroEditorDraft {
  key: number
  repeatCount: number
  ignoreDelays: boolean
  interruptOnOtherKey: boolean
  events: MacroEvent[]
}

export function isNumpadVirtualKey(key: number): boolean {
  return Number.isInteger(key) && key >= NUMPAD_VIRTUAL_KEY_MIN && key <= NUMPAD_VIRTUAL_KEY_MAX
}

export function numpadVirtualKeyToDigit(key: number): number | null {
  return isNumpadVirtualKey(key) ? key - NUMPAD_VIRTUAL_KEY_MIN : null
}

export function numpadDigitToVirtualKey(digit: number): number | null {
  if (!Number.isInteger(digit) || digit < 0 || digit > 9) return null
  return NUMPAD_VIRTUAL_KEY_MIN + digit
}

export function macroVirtualKeyName(code: number): string {
  const named = VK_NAMES[code]
  if (named) return named
  if (code >= 0x41 && code <= 0x5a) return String.fromCharCode(code)
  if (code >= 0x30 && code <= 0x39) return String.fromCharCode(code)

  const numpadDigit = numpadVirtualKeyToDigit(code)
  if (numpadDigit !== null) return `NumPad ${numpadDigit}`

  if (code >= 0x70 && code <= 0x7b) return `F${code - 0x6f}`
  return `Key ${code}`
}

export function createMacroEditorDraft(
  key: number,
  slots: readonly MacroSlot[]
): MacroEditorDraft {
  if (!isNumpadVirtualKey(key)) {
    throw new RangeError(`Unsupported macro key: ${key}`)
  }

  const slot = slots.find((candidate) => candidate.key === key)
  return {
    key,
    repeatCount: slot?.repeatCount ?? 1,
    ignoreDelays: slot?.ignoreDelays ?? false,
    interruptOnOtherKey: slot?.interruptOnOtherKey ?? false,
    events: slot?.events.map((event) => ({ ...event })) ?? []
  }
}

export function appendCapturedEvents(
  current: readonly MacroEvent[],
  captured: readonly MacroEvent[],
  interrupted: boolean
): MacroEvent[] {
  if (interrupted || captured.length === 0) return [...current]
  return [...current, ...captured]
}

export function hasMacroEvents(events: readonly MacroEvent[]): boolean {
  return events.length > 0
}
