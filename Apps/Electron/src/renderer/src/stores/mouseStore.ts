import { create } from 'zustand'
import { sanitizeBridgeError } from '../api/bridge'
import {
  CURSOR_THEME_MODES,
  applyCursorThemeNow,
  applyWindowsMouse,
  getMouseState,
  restoreWindowsDefaultCursor,
  setCursorThemeMode,
  syncMouseFromWindows,
  type CursorThemeMode,
  type MousePointerState
} from '../api/mouse'

/**
 * Host-backed pointer/cursor state for the native Mouse page. Mirrors the
 * KeyboardBacklightPage store convention: components render store data and
 * never run host round-trips (or their setState churn) inside effects.
 */
export interface MouseStore {
  state: MousePointerState | null
  loading: boolean
  /** Sanitized Host error from the initial/sync load, shown inline. */
  error: string | null
  /** True while any host write is in flight (drives per-action disabled UI). */
  writing: boolean
  load: () => Promise<void>
  applyPointer: (speed: number, swapButtons: boolean) => Promise<boolean>
  /**
   * Persists the theme mode; modes Auto/Light/Dark go through
   * mouse.setCursorThemeMode, WindowsDefault restores the original scheme.
   */
  changeThemeMode: (mode: CursorThemeMode) => Promise<boolean>
  applyCursorStyleNow: () => Promise<boolean>
  /** Re-reads pointer speed/buttons from Windows and adopts them. */
  refreshFromWindows: () => Promise<boolean>
}

export const useMouseStore = create<MouseStore>()((set, get) => ({
  state: null,
  loading: true,
  error: null,
  writing: false,

  async load() {
    set({ loading: true, error: null })
    try {
      const state = await getMouseState()
      set({ state })
    } catch (error) {
      set({ error: sanitizeBridgeError(error) })
    } finally {
      set({ loading: false })
    }
  },

  async applyPointer(speed, swapButtons) {
    set({ writing: true })
    try {
      const result = await applyWindowsMouse(speed, swapButtons)
      if (!result.ok) return false
      const current = get().state
      set({
        state:
          current === null
            ? current
            : { ...current, pointerSpeed: speed, swapButtons }
      })
      return true
    } catch {
      return false
    } finally {
      set({ writing: false })
    }
  },

  async changeThemeMode(mode) {
    set({ writing: true })
    try {
      const result =
        mode === CURSOR_THEME_MODES.windowsDefault
          ? await restoreWindowsDefaultCursor()
          : await setCursorThemeMode(mode)
      if (!result.ok) return false
      const current = get().state
      set({
        state: current === null ? current : { ...current, cursorThemeMode: mode }
      })
      return true
    } catch {
      return false
    } finally {
      set({ writing: false })
    }
  },

  async applyCursorStyleNow() {
    set({ writing: true })
    try {
      return (await applyCursorThemeNow()).ok
    } catch {
      return false
    } finally {
      set({ writing: false })
    }
  },

  async refreshFromWindows() {
    set({ writing: true })
    try {
      const state = await syncMouseFromWindows()
      set({ state })
      return true
    } catch {
      return false
    } finally {
      set({ writing: false })
    }
  }
}))
