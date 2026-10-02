import { create } from 'zustand'
import {
  isUiScaleOption,
  parseUiScalePreference,
  resolveUiScale,
  UI_SCALE_AUTO,
  type UiScalePreference
} from '../theme/uiScale'

export type ThemeMode = 'light' | 'dark'
export type ThemePreference = 'system' | 'light' | 'dark'
/** 风格偏好：与明暗正交的外观维度，驱动 <html data-style> 属性。 */
export type StylePreference = 'default' | 'focus' | 'neubrutalism'
export {
  UI_SCALE_AUTO,
  UI_SCALE_OPTIONS,
  type UiScale,
  type UiScalePreference
} from '../theme/uiScale'

export interface ThemeStore {
  themeMode: ThemeMode
  /** The user's theme preference ('system' follows the OS light/dark). */
  themePreference: ThemePreference
  stylePreference: StylePreference
  colorPrimary?: string
  /** Currently applied UI scale factor (1.0 = default). */
  uiScale: number
  /**
   * Persisted scale choice. 'auto' follows the window size; a number locks
   * the matching settings step.
   */
  uiScalePreference: UiScalePreference
  /**
   * ApplyAccentColorToTheme gate: when true the accent tints the surface
   * palette (surfaces/controls/strokes/secondary text); when false the
   * neutral default surfaces from global.css are used.
   */
  accentTintsSurfaces: boolean
  setThemeMode: (mode: ThemeMode) => void
  setThemePreference: (preference: ThemePreference) => void
  setStylePreference: (preference: StylePreference) => void
  setAccent: (color?: string) => void
  setUiScale: (scale: number) => void
  setUiScalePreference: (preference: UiScalePreference) => void
  applyComputedUiScale: (scale: number) => void
  setAccentTintsSurfaces: (enabled: boolean) => void
}

const UI_SCALE_STORAGE_KEY = 'udt-ui-scale'
const THEME_STORAGE_KEY = 'udt.theme'
const STYLE_STORAGE_KEY = 'udt.theme-style'
export const ACCENT_TINTS_STORAGE_KEY = 'udt.accent-tints'

function readStoredAccentTintsPreference(): boolean {
  try {
    const stored = localStorage.getItem(ACCENT_TINTS_STORAGE_KEY)
    if (stored === 'false') return false
    if (stored === 'true') return true
  } catch {
    /* ignore quota / private mode */
  }
  return true
}

function readStoredThemePreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY)
    if (stored === 'light' || stored === 'dark' || stored === 'system') return stored
  } catch {
    /* ignore quota / private mode */
  }
  return 'system'
}

function readStoredStylePreference(): StylePreference {
  try {
    const stored = localStorage.getItem(STYLE_STORAGE_KEY)
    if (stored === 'default' || stored === 'focus' || stored === 'neubrutalism') return stored
  } catch {
    /* ignore quota / private mode */
  }
  return 'default'
}

function persistUiScalePreference(preference: UiScalePreference): void {
  try {
    localStorage.setItem(UI_SCALE_STORAGE_KEY, String(preference))
  } catch {
    /* ignore quota / private mode */
  }
}

function readStoredUiScalePreference(): UiScalePreference {
  try {
    return parseUiScalePreference(localStorage.getItem(UI_SCALE_STORAGE_KEY)) ?? UI_SCALE_AUTO
  } catch {
    /* ignore quota / private mode */
  }
  return UI_SCALE_AUTO
}

/**
 * Applies the UI scale to the whole interface.
 *
 * In Electron the scale is pushed to the main process, which multiplies it
 * into webContents.setZoomFactor for every surface. Zoom factor (unlike CSS
 * zoom) keeps @media breakpoints, @container queries and devicePixelRatio
 * consistent with each other, and satellite windows share the same density.
 *
 * In browser dev (dev:web, platform 'web') there is no main process, so CSS
 * `zoom` on <html> remains the fallback.
 */
export function applyUiScale(scale: number): void {
  const bridge = window.bridge
  if (bridge && bridge.platform !== 'web') {
    document.documentElement.style.removeProperty('zoom')
    void bridge.setUiScale?.(scale)?.catch(() => undefined)
    return
  }
  const html = document.documentElement
  if (scale === 1) {
    html.style.removeProperty('zoom')
  } else {
    html.style.zoom = String(scale)
  }
}

/** 应用风格维度：<html data-style="default|focus|neubrutalism">，始终显式写出属性值。 */
export function applyThemeStyle(style: StylePreference): void {
  document.documentElement.setAttribute('data-style', style)
}

const initialUiScalePreference = readStoredUiScalePreference()
const initialUiScale = resolveUiScale(initialUiScalePreference)
const initialStylePreference = readStoredStylePreference()

export const useThemeStore = create<ThemeStore>()((set, get) => ({
  themeMode: 'dark',
  themePreference: readStoredThemePreference(),
  stylePreference: initialStylePreference,
  colorPrimary: undefined,
  uiScale: initialUiScale,
  uiScalePreference: initialUiScalePreference,
  accentTintsSurfaces: readStoredAccentTintsPreference(),
  setThemeMode: (themeMode) => set({ themeMode }),
  setThemePreference: (themePreference) => {
    set({ themePreference })
    try {
      localStorage.setItem(THEME_STORAGE_KEY, themePreference)
    } catch {
      /* ignore quota / private mode */
    }
  },
  setStylePreference: (stylePreference) => {
    set({ stylePreference })
    try {
      localStorage.setItem(STYLE_STORAGE_KEY, stylePreference)
    } catch {
      /* ignore quota / private mode */
    }
  },
  setAccent: (colorPrimary) => set({ colorPrimary }),
  setUiScalePreference: (uiScalePreference) => {
    const uiScale = resolveUiScale(uiScalePreference)
    set({ uiScalePreference, uiScale })
    persistUiScalePreference(uiScalePreference)
    applyUiScale(uiScale)
  },
  setUiScale: (scale) => {
    get().setUiScalePreference(isUiScaleOption(scale) ? scale : 1)
  },
  applyComputedUiScale: (uiScale) => {
    if (uiScale === get().uiScale) return
    set({ uiScale })
    applyUiScale(uiScale)
  },
  setAccentTintsSurfaces: (accentTintsSurfaces) => {
    set({ accentTintsSurfaces })
    try {
      localStorage.setItem(ACCENT_TINTS_STORAGE_KEY, String(accentTintsSurfaces))
    } catch {
      /* ignore quota / private mode */
    }
  }
}))

// Apply the persisted scale once at startup (themeStore is imported by main.tsx
// before the first render, so the whole interface is scaled from launch).
applyUiScale(useThemeStore.getState().uiScale)
// 启动时应用一次持久化的风格（含显式 default），保证首帧即带 data-style 属性。
applyThemeStyle(useThemeStore.getState().stylePreference)
