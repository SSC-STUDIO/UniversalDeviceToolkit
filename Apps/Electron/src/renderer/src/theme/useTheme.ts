import { useEffect } from 'react'
import { settingsApi } from '../api/settings'
import { systemApi } from '../api/system'
import { applyUiScale, useThemeStore } from '../stores/themeStore'
import type { StylePreference, ThemeMode } from '../stores/themeStore'
import { computeAutoUiScale, layoutWidthChanged, readLayoutWidth, UI_SCALE_AUTO } from './uiScale'
import {
  applyAccentSurfacePalette,
  clearAccentSurfacePalette,
  createAccentPalette
} from './accentPalette'

type ThemePreference = 'System' | 'Light' | 'Dark'
type AccentColorSource = 'System' | 'Custom'

interface ApplicationSettings {
  Theme?: ThemePreference
  AccentColor?: { R: number; G: number; B: number } | null
  AccentColorSource?: AccentColorSource
  ApplyAccentColorToTheme?: boolean
}

const ACCENT_SOURCE_STORAGE_KEY = 'udt.accent-source'
const ACCENT_COLOR_STORAGE_KEY = 'udt.accent'
export const ACCENT_TINTS_STORAGE_KEY = 'udt.accent-tints'
const DEFAULT_SYSTEM_ACCENT_HEX = '#0078d4'

/**
 * Renderer-authoritative accent preference. The host application scope may lag
 * behind (async save) or drop unknown fields, so the choice made in the
 * appearance settings is persisted locally and wins over the host value.
 */
export function storeAccentPreference(source: 'System' | 'Custom', hex?: string): void {
  try {
    if (source === 'Custom' && hex) {
      localStorage.setItem(ACCENT_SOURCE_STORAGE_KEY, 'Custom')
      localStorage.setItem(ACCENT_COLOR_STORAGE_KEY, hex)
    } else if (source === 'System') {
      localStorage.setItem(ACCENT_SOURCE_STORAGE_KEY, 'System')
      localStorage.removeItem(ACCENT_COLOR_STORAGE_KEY)
    } else {
      localStorage.removeItem(ACCENT_SOURCE_STORAGE_KEY)
      localStorage.removeItem(ACCENT_COLOR_STORAGE_KEY)
    }
  } catch {
    // localStorage is unavailable; Host settings remain the only source.
  }
}

export function storeAccentTintsPreference(enabled: boolean): void {
  try {
    localStorage.setItem(ACCENT_TINTS_STORAGE_KEY, String(enabled))
  } catch {
    // localStorage is unavailable; Host settings remain the only source.
  }
}

function storedAccentTintsPreference(): boolean {
  try {
    const stored = localStorage.getItem(ACCENT_TINTS_STORAGE_KEY)
    if (stored === 'false') return false
    if (stored === 'true') return true
  } catch {
    // ignore
  }
  return true
}

function storedAccentPreference(): { source: 'System' | 'Custom'; hex?: string } | null {
  try {
    const source = localStorage.getItem(ACCENT_SOURCE_STORAGE_KEY)
    if (source === 'Custom') {
      const hex = localStorage.getItem(ACCENT_COLOR_STORAGE_KEY)
      if (hex && /^#[0-9a-f]{6}$/i.test(hex)) return { source: 'Custom', hex }
      return null
    }
    if (source === 'System') return { source: 'System' }
  } catch {
    // ignore
  }
  return null
}

function systemPrefersDark(): boolean {
  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

function rgbToHex(color: { R: number; G: number; B: number }): string {
  const toHex = (value: number): string => value.toString(16).padStart(2, '0')
  return `#${toHex(color.R)}${toHex(color.G)}${toHex(color.B)}`
}

export interface ThemeController {
  themeMode: ThemeMode
  themePreference: 'system' | 'light' | 'dark'
  stylePreference: StylePreference
  colorPrimary?: string
  uiScale: number
  setThemeMode: (mode: ThemeMode) => void
  setThemePreference: (preference: 'system' | 'light' | 'dark') => void
  setStylePreference: (preference: StylePreference) => void
  setAccent: (color?: string) => void
  setUiScale: (scale: number) => void
}

export function useTheme(): ThemeController {
  const themeMode = useThemeStore((s) => s.themeMode)
  const themePreference = useThemeStore((s) => s.themePreference)
  // 风格偏好是纯本地状态，不参与 host settings 同步。
  const stylePreference = useThemeStore((s) => s.stylePreference)
  const setStylePreference = useThemeStore((s) => s.setStylePreference)
  const colorPrimary = useThemeStore((s) => s.colorPrimary)
  const uiScale = useThemeStore((s) => s.uiScale)
  const uiScalePreference = useThemeStore((s) => s.uiScalePreference)
  const accentTintsSurfaces = useThemeStore((s) => s.accentTintsSurfaces)
  const setThemeMode = useThemeStore((s) => s.setThemeMode)
  const setThemePreference = useThemeStore((s) => s.setThemePreference)
  const setAccent = useThemeStore((s) => s.setAccent)
  const setUiScale = useThemeStore((s) => s.setUiScale)
  const applyComputedUiScale = useThemeStore((s) => s.applyComputedUiScale)
  const setAccentTintsSurfaces = useThemeStore((s) => s.setAccentTintsSurfaces)

  // Keep the document scaling in sync with the store (the store also applies
  // the initial scale at module load so the whole app is scaled from launch).
  useEffect(() => {
    applyUiScale(uiScale)
  }, [uiScale])

  /**
   * Single application point for the accent-tinted surface palette. Recomputes
   * whenever the mode, accent or the ApplyAccentColorToTheme gate changes, so
   * light/dark switches (manual or system-following) always retint surfaces
   * with the mode-appropriate variant.
   */
  useEffect(() => {
    const hex = colorPrimary ?? DEFAULT_SYSTEM_ACCENT_HEX
    if (accentTintsSurfaces) {
      applyAccentSurfacePalette(createAccentPalette(hex, themeMode === 'dark'))
    } else {
      clearAccentSurfacePalette()
    }
  }, [themeMode, colorPrimary, accentTintsSurfaces])

  useEffect(() => {
    let disposed = false
    let media: MediaQueryList | null = null
    let systemAccentHex = DEFAULT_SYSTEM_ACCENT_HEX

    // Follow the OS light/dark while the preference is 'system'.
    const onSystemChange = (): void => {
      if (disposed || themePreference !== 'system') return
      setThemeMode(systemPrefersDark() ? 'dark' : 'light')
    }

    const syncSystemListener = (): void => {
      media?.removeEventListener('change', onSystemChange)
      media = null
      if (themePreference === 'system') {
        media = window.matchMedia('(prefers-color-scheme: dark)')
        media.addEventListener('change', onSystemChange)
      }
    }

    // Electron ThemeManager.SetColor always applies the resolved accent.
    // ApplyAccentColorToTheme only gates the tinted surface palette (style
    // preset), not --udt-accent / selection rings / Ant Design colorPrimary.
    const resolveAccent = (settings?: ApplicationSettings): string => {
      // Renderer choice wins over the host value (async host reads may be stale
      // or drop the accent fields, which previously reset the picked color).
      const local = storedAccentPreference()
      if (local) {
        return local.source === 'Custom' && local.hex ? local.hex : systemAccentHex
      }
      if (settings?.AccentColorSource === 'Custom' && settings.AccentColor) {
        return rgbToHex(settings.AccentColor)
      }
      return systemAccentHex
    }

    const resolveAccentTints = (settings?: ApplicationSettings): boolean => {
      if (settings?.ApplyAccentColorToTheme !== undefined) {
        return settings.ApplyAccentColorToTheme
      }
      return storedAccentTintsPreference()
    }

    const apply = (settings?: ApplicationSettings): void => {
      // themePreference is the renderer-authoritative source (Settings page
      // writes it to the store + localStorage; host value may be stale).
      syncSystemListener()
      const mode: ThemeMode =
        themePreference === 'dark'
          ? 'dark'
          : themePreference === 'light'
            ? 'light'
            : systemPrefersDark()
              ? 'dark'
              : 'light'
      setThemeMode(mode)
      const accentHex = resolveAccent(settings)
      setAccent(accentHex)
      // Electron ThemeManager: the accent itself always applies; the palette
      // effect above tints surfaces when ApplyAccentColorToTheme is enabled.
      setAccentTintsSurfaces(resolveAccentTints(settings))
    }

    const load = (): void => {
      settingsApi
        .get('application')
        .then((res) => {
          if (!disposed) apply(res.value as ApplicationSettings)
        })
        .catch(() => undefined)
    }

    apply()
    systemApi
      .getAccentColor()
      .then((color) => {
        if (disposed) return
        systemAccentHex = rgbToHex({ R: color.r, G: color.g, B: color.b })
        load()
      })
      .catch(() => {
        load()
      })

    const offChanged = settingsApi.onChanged((data) => {
      if (data.scope !== 'application') return
      load()
    })

    return () => {
      disposed = true
      media?.removeEventListener('change', onSystemChange)
      offChanged()
    }
  }, [themePreference, setAccent, setThemeMode, setAccentTintsSurfaces])

  useEffect(() => {
    if (uiScalePreference !== UI_SCALE_AUTO) return undefined
    let lastWidth = readLayoutWidth()
    const applyAuto = (): void => {
      const width = readLayoutWidth()
      lastWidth = width
      applyComputedUiScale(computeAutoUiScale(width))
    }
    applyAuto()
    let debounceId: ReturnType<typeof setTimeout> | undefined
    const onResize = (): void => {
      // zoomFactor / CSS zoom fire resize while outerWidth stays put.
      if (!layoutWidthChanged(lastWidth, readLayoutWidth())) return
      if (debounceId !== undefined) clearTimeout(debounceId)
      debounceId = setTimeout(applyAuto, 100)
    }
    window.addEventListener('resize', onResize)
    return () => {
      if (debounceId !== undefined) clearTimeout(debounceId)
      window.removeEventListener('resize', onResize)
    }
  }, [uiScalePreference, applyComputedUiScale])

  return {
    themeMode,
    themePreference,
    colorPrimary,
    uiScale,
    stylePreference,
    setThemeMode,
    setThemePreference,
    setStylePreference,
    setAccent,
    setUiScale
  }
}

