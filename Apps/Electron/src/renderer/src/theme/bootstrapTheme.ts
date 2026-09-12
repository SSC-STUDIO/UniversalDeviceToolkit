import type { StylePreference, ThemeMode, ThemePreference } from '../stores/themeStore'
import { applyThemeStyle } from '../stores/themeStore'
import { applyAppFont, getStoredAppFont } from '../utils/fonts'

const THEME_STORAGE_KEY = 'udt.theme'
const STYLE_STORAGE_KEY = 'udt.theme-style'

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

function readStoredStylePreference(): StylePreference {
  try {
    const stored = localStorage.getItem(STYLE_STORAGE_KEY)
    if (stored === 'default' || stored === 'focus' || stored === 'neubrutalism') return stored
  } catch {
    /* ignore */
  }
  return 'default'
}

/**
 * Apply theme attributes and custom font before the first React paint so browser
 * dev / cold start does not flash an unstyled theme or mismatched typography.
 */
export function bootstrapThemeDocument(): void {
  const root = document.documentElement
  const mode = resolveThemeMode(readStoredThemePreference())
  root.setAttribute('data-theme', mode)
  // 首帧前同步写 data-style（含显式 default），与 data-theme 一起避免闪变。
  applyThemeStyle(readStoredStylePreference())
  root.dataset.backdrop = 'none'
  root.style.colorScheme = mode
  applyAppFont(getStoredAppFont())
}
