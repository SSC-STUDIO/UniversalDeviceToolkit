/**
 * Font management — port of Electron Utils/AppFontManager.cs.
 * The CSS variable --udt-font-family (global.css) mirrors the Electron
 * AppFontFamily intent; this module keeps the JS-side copy.
 *
 * NOTE: unlike WPF, Chromium resolves fonts PER CHARACTER down the list.
 * A serif CJK face (SimSun) must never appear before the CJK sans faces,
 * otherwise Chinese text renders in 宋体 while Latin stays Segoe - which
 * was the reported bug. SimSun therefore only lives at the very end of the
 * stylesheet fallback, never ahead of Microsoft YaHei / PingFang.
 */
export const AppFontStack = [
  'Segoe UI Variable Text',
  'Segoe UI Variable',
  'Segoe UI',
  'Microsoft YaHei UI',
  'Microsoft YaHei',
  'PingFang SC',
  'Hiragino Sans GB',
  'Noto Sans SC',
  'sans-serif'
].join(', ')

export const FONT_PRESETS: { value: string; labelKey: string; defaultLabel: string; stack: string }[] = [
  {
    value: 'system',
    labelKey: 'settings.appearance.fontPresets.system',
    defaultLabel: 'System Default (系统默认)',
    stack: AppFontStack
  },
  {
    value: 'yahei',
    labelKey: 'settings.appearance.fontPresets.yahei',
    defaultLabel: 'Microsoft YaHei UI (微软雅黑)',
    stack: "'Microsoft YaHei UI', 'Microsoft YaHei', 'Segoe UI', sans-serif"
  },
  {
    value: 'segoe',
    labelKey: 'settings.appearance.fontPresets.segoe',
    defaultLabel: 'Segoe UI Variable',
    stack: "'Segoe UI Variable Text', 'Segoe UI Variable', 'Segoe UI', 'Microsoft YaHei UI', sans-serif"
  },
  {
    value: 'noto',
    labelKey: 'settings.appearance.fontPresets.noto',
    defaultLabel: 'Noto Sans SC (思源黑体 / 苹方)',
    stack: "'Noto Sans SC', 'Noto Sans CJK SC', 'PingFang SC', 'Microsoft YaHei UI', sans-serif"
  },
  {
    value: 'harmony',
    labelKey: 'settings.appearance.fontPresets.harmony',
    defaultLabel: 'HarmonyOS Sans (鸿蒙黑体)',
    stack: "'HarmonyOS Sans SC', 'HarmonyOS Sans', 'Microsoft YaHei UI', sans-serif"
  },
  {
    value: 'cascadia',
    labelKey: 'settings.appearance.fontPresets.cascadia',
    defaultLabel: 'Cascadia Code / Consolas (等宽极客)',
    stack: "'Cascadia Code', Consolas, 'Courier New', 'Microsoft YaHei UI', monospace"
  }
]

export const FONT_STORAGE_KEY = 'udt.font-family'

export function resolveFontStack(override?: string): string {
  if (!override || override.trim() === '' || override === 'system') {
    return AppFontStack
  }
  const matched = FONT_PRESETS.find((p) => p.value === override)
  if (matched) {
    return matched.stack
  }
  return `${override}, ${AppFontStack}`
}

export function applyAppFont(fontValue?: string): void {
  if (typeof document === 'undefined' || !document.documentElement) return
  const fontStack = resolveFontStack(fontValue)
  if (typeof document.documentElement.style?.setProperty === 'function') {
    document.documentElement.style.setProperty('--udt-font-family', fontStack)
  } else if (document.documentElement.style) {
    ;(document.documentElement.style as unknown as Record<string, unknown>)['--udt-font-family'] = fontStack
  }
  try {
    if (fontValue && fontValue !== 'system') {
      localStorage.setItem(FONT_STORAGE_KEY, fontValue)
    } else {
      localStorage.removeItem(FONT_STORAGE_KEY)
    }
  } catch {
    /* ignore */
  }
}

export function getStoredAppFont(): string {
  try {
    return localStorage.getItem(FONT_STORAGE_KEY) ?? 'system'
  } catch {
    return 'system'
  }
}
