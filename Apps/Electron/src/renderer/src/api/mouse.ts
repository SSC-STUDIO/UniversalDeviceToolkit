import { invoke } from './bridge'

/** Cursor theme modes supported by the Host (`CursorThemeMode` enum). */
export const CURSOR_THEME_MODES = {
  /** Apply the UDT scheme matching the current Windows light/dark theme. */
  auto: 0,
  light: 1,
  dark: 2,
  /** Restore the backed-up original "Windows Aero" cursor scheme. */
  windowsDefault: 3
} as const

export type CursorThemeMode = (typeof CURSOR_THEME_MODES)[keyof typeof CURSOR_THEME_MODES]

export interface MousePointerState {
  /** Windows pointer motion speed, 1-20 (default 10). */
  pointerSpeed: number
  swapButtons: boolean
  cursorThemeMode: CursorThemeMode
  /** True while the Host watches the system theme and re-applies automatically. */
  autoThemeCursorStyle: boolean
  /** UDT cursor scheme applied for the current theme: 'light' | 'dark' | ''. */
  lastAppliedTheme: string
}

export interface MouseActionResult {
  ok: boolean
}

const DEFAULT_POINTER_SPEED = 10

function asRecord(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null
}

function clampPointerSpeed(value: unknown): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) return DEFAULT_POINTER_SPEED
  return Math.min(20, Math.max(1, Math.round(value)))
}

function parseCursorThemeMode(value: unknown): CursorThemeMode {
  if (
    typeof value === 'number' &&
    Number.isInteger(value) &&
    value >= CURSOR_THEME_MODES.auto &&
    value <= CURSOR_THEME_MODES.windowsDefault
  ) {
    // Range-validated above; safe literal narrowing.
    return value as CursorThemeMode
  }
  return CURSOR_THEME_MODES.auto
}

function readLastAppliedTheme(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function parseMouseState(value: unknown): MousePointerState {
  const record = asRecord(value)
  if (!record) throw new Error('Invalid mouse pointer state')
  return {
    pointerSpeed: clampPointerSpeed(record.pointerSpeed),
    swapButtons: record.swapButtons === true,
    cursorThemeMode: parseCursorThemeMode(record.cursorThemeMode),
    autoThemeCursorStyle: record.autoThemeCursorStyle === true,
    lastAppliedTheme: readLastAppliedTheme(record.lastAppliedTheme)
  }
}

function readOk(value: unknown): MouseActionResult {
  return { ok: asRecord(value)?.ok === true }
}

export async function getMouseState(): Promise<MousePointerState> {
  return parseMouseState(await invoke<unknown>('mouse.getState', {}))
}

/** Write pointer speed + button layout into the active Windows profile. */
export async function applyWindowsMouse(
  speed: number,
  swapButtons: boolean
): Promise<MouseActionResult> {
  const parsed = clampPointerSpeed(speed)
  return readOk(await invoke<unknown>('mouse.applyWindows', { speed: parsed, swapButtons }))
}

export async function setCursorThemeMode(mode: CursorThemeMode): Promise<MouseActionResult> {
  return readOk(await invoke<unknown>('mouse.setCursorThemeMode', { mode }))
}

/** Applies the UDT cursor scheme matching the current system theme. */
export async function applyCursorThemeNow(): Promise<MouseActionResult> {
  return readOk(await invoke<unknown>('mouse.applyCursorThemeNow', {}))
}

/** Re-reads pointer speed / button layout from Windows; returns fresh state. */
export async function syncMouseFromWindows(): Promise<MousePointerState> {
  return parseMouseState(await invoke<unknown>('mouse.syncFromWindows', {}))
}

/** Restores the backed-up original Windows Aero cursor scheme. */
export async function restoreWindowsDefaultCursor(): Promise<MouseActionResult> {
  return readOk(await invoke<unknown>('mouse.restoreWindowsDefault', {}))
}
