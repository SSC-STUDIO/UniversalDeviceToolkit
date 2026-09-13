/**
 * Main-process tray strings (mirrors Electron Resource.* used by TrayHelper).
 * Full renderer i18n lives in the renderer; tray only needs these labels.
 */

import { nativeLabels, type TrayStrings } from '../shared/i18n/nativeLabels'
export type { TrayStrings } from '../shared/i18n/nativeLabels'

const DEACTIVATE_GPU_STABLE = '__udt.quickAction.deactivateGpu'

let currentLang = 'zh-CN'

export function setTrayLanguage(lang: string | null | undefined): void {
  if (!lang || typeof lang !== 'string') return
  currentLang = lang
}

export function trayStrings(): TrayStrings {
  return nativeLabels(currentLang)
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
