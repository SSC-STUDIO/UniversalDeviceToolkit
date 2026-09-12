/**
 * Main-window geometry persistence (pure UI shell state, not Host settings).
 * Stored as userData/window-state.json: normal bounds, maximized flag and the
 * last applied uiScale so the first paint after a restart already uses the
 * user's scale instead of flashing at the default.
 */
import { app } from 'electron'
import { join } from 'path'
import { mkdirSync, readFileSync, renameSync, unlinkSync, writeFileSync } from 'fs'

export interface PersistedWindowState {
  x?: number
  y?: number
  width?: number
  height?: number
  isMaximized?: boolean
  uiScale?: number
}

let cachedState: PersistedWindowState | null = null

function stateFilePath(): string {
  return join(app.getPath('userData'), 'window-state.json')
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value)
}

export function readWindowState(): PersistedWindowState {
  if (cachedState) return cachedState
  try {
    const raw = JSON.parse(readFileSync(stateFilePath(), 'utf8')) as Record<string, unknown>
    const state: PersistedWindowState = {}
    if (isFiniteNumber(raw.x)) state.x = raw.x
    if (isFiniteNumber(raw.y)) state.y = raw.y
    if (isFiniteNumber(raw.width) && raw.width >= 200) state.width = Math.round(raw.width)
    if (isFiniteNumber(raw.height) && raw.height >= 200) state.height = Math.round(raw.height)
    if (raw.isMaximized === true) state.isMaximized = true
    if (isFiniteNumber(raw.uiScale) && raw.uiScale > 0) state.uiScale = raw.uiScale
    cachedState = state
  } catch {
    // First run or unreadable file: fall back to defaults.
    cachedState = {}
  }
  return cachedState
}

function writeStateFileAtomic(contents: string): void {
  const dest = stateFilePath()
  mkdirSync(app.getPath('userData'), { recursive: true })
  const temp = `${dest}.${process.pid}.tmp`
  try {
    writeFileSync(temp, contents, 'utf8')
    renameSync(temp, dest)
  } catch (error) {
    try {
      unlinkSync(temp)
    } catch {
      // temp may not exist if writeFileSync failed
    }
    throw error
  }
}

export function updateWindowState(partial: PersistedWindowState): void {
  const next = { ...readWindowState(), ...partial }
  cachedState = next
  try {
    writeStateFileAtomic(JSON.stringify(next, null, 2))
  } catch (error) {
    console.error('[main] failed to persist window state:', error)
  }
}
