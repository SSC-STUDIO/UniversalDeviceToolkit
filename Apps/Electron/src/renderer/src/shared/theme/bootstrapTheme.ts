import type { ThemeMode, ThemePreference } from './themeStore'
import { applyAppFont, getStoredAppFont } from '../format/fonts'

const THEME_STORAGE_KEY = 'udt.theme'

function readStoredThemePreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY)
    if (stored === 'light' || stored === 'dark' || stored === 'system') return stored
  } catch {
    /* ignore */
  }
  return 'system'
}

function resolveThemeMode(preference: ThemePreference): ThemeMode {
  if (preference === 'dark') return 'dark'
  if (preference === 'light') return 'light'
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

/**
 * Apply theme attributes and custom font before the first React paint so browser
 * dev / cold start does not flash an unstyled theme or mismatched typography.
 */
export function bootstrapThemeDocument(): void {
  const root = document.documentElement
  const mode = resolveThemeMode(readStoredThemePreference())
  root.setAttribute('data-theme', mode)
  root.dataset.backdrop = 'none'
  root.style.colorScheme = mode
  applyAppFont(getStoredAppFont())
}
