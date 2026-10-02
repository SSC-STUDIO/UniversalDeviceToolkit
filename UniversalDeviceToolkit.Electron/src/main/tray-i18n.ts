/**
 * Main-process tray strings (mirrors Electron Resource.* used by TrayHelper).
 * Full renderer i18n lives in the renderer; tray only needs these labels.
 */

export interface TrayStrings {
  dashboard: string
  keyboard: string
  automation: string
  macro: string
  windowsOptimization: string
  open: string
  close: string
  unnamed: string
  deactivateGpu: string
  powerMode: string
  quiet: string
  balance: string
  performance: string
  custom: string
}

const ZH_CN: TrayStrings = {
  dashboard: '控制台',
  keyboard: '键盘',
  automation: '自动化',
  macro: '自定义宏',
  windowsOptimization: '系统优化',
  open: '打开',
  close: '关闭',
  unnamed: '未命名',
  deactivateGpu: '停用 GPU',
  powerMode: '电源模式',
  quiet: '安静',
  balance: '平衡',
  performance: '野兽',
  custom: '自定义'
}

const EN_US: TrayStrings = {
  dashboard: 'Dashboard',
  keyboard: 'Keyboard',
  automation: 'Actions',
  macro: 'Macro',
  windowsOptimization: 'System optimization',
  open: 'Open',
  close: 'Close',
  unnamed: 'Unnamed',
  deactivateGpu: 'Deactivate GPU',
  powerMode: 'Power Mode',
  quiet: 'Quiet',
  balance: 'Balance',
  performance: 'Performance',
  custom: 'Custom'
}

const ZH_HANT: TrayStrings = {
  ...ZH_CN,
  dashboard: '控制台',
  macro: '自訂巨集',
  windowsOptimization: '系統最佳化',
  open: '開啟',
  close: '關閉',
  unnamed: '未命名',
  deactivateGpu: '強制休眠獨顯',
  powerMode: '電源模式',
  quiet: '安靜',
  balance: '平衡',
  performance: '效能',
  custom: '自訂'
}

const BY_LANG: Record<string, TrayStrings> = {
  'zh-CN': ZH_CN,
  'zh-Hans': ZH_CN,
  zh: ZH_CN,
  'zh-Hant': ZH_HANT,
  'zh-TW': ZH_HANT,
  'zh-HK': ZH_HANT,
  'en-US': EN_US,
  en: EN_US
}

const DEACTIVATE_GPU_STABLE = '__udt.quickAction.deactivateGpu'

let currentLang = 'zh-CN'

export function setTrayLanguage(lang: string | null | undefined): void {
  if (!lang || typeof lang !== 'string') return
  currentLang = lang
}

export function trayStrings(): TrayStrings {
  if (BY_LANG[currentLang]) return BY_LANG[currentLang]
  const base = currentLang.split('-')[0] ?? currentLang
  return BY_LANG[base] ?? (base === 'zh' ? ZH_CN : EN_US)
}

/** Port of PipelineNameLocalizer.LocalizeStoredName for the default GPU quick action. */
export function localizePipelineName(storedName: string | null | undefined): string {
  const s = trayStrings()
  if (!storedName || storedName.trim().length === 0) return s.unnamed
  if (storedName === DEACTIVATE_GPU_STABLE) return s.deactivateGpu
  // Legacy baked Chinese/English titles still resolve to the localized title.
  if (
    storedName === '停用 GPU' ||
    storedName === 'Deactivate GPU' ||
    storedName === '強制休眠獨顯' ||
    storedName === '停用GPU'
  ) {
    return s.deactivateGpu
  }
  return storedName
}
