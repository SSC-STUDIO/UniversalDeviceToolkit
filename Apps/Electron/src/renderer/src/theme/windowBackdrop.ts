import { settingsApi } from '../api/settings'

export type WindowBackdropStyle = 'Windows' | 'macOS' | 'Off'
type BackgroundMaterial = 'mica' | 'acrylic' | 'none'

const MATERIAL_BY_STYLE: Record<WindowBackdropStyle, BackgroundMaterial> = {
  Windows: 'mica',
  macOS: 'acrylic',
  Off: 'none'
}

/** Bridge exposes the platform (win32 | darwin | linux). */
export function currentBackdropPlatform(): string {
  return window.bridge?.platform ?? 'win32'
}

export function normalizeWindowBackdropStyle(value: unknown): WindowBackdropStyle {
  return value === 'macOS' || value === 'Off' ? value : 'Windows'
}

export function applyWindowBackdrop(style: WindowBackdropStyle): void {
  const platform = currentBackdropPlatform()
  const canUseNativeMaterial =
    typeof window.bridge?.setBackgroundMaterial === 'function' &&
    platform !== 'web'

  if (!canUseNativeMaterial) {
    document.documentElement.dataset.backdrop = 'none'
    return
  }

  const material = MATERIAL_BY_STYLE[style]

  if (platform === 'darwin') {
    document.documentElement.dataset.backdrop = material === 'none' ? 'none' : 'acrylic'
    return
  }

  // Electron backgroundMaterial (mica/acrylic) is a Windows DWM API. On Linux
  // keep data-backdrop so CSS can paint an opaque mica/acrylic approximation
  // (light sage chrome / dark #202020 fills). Never request a native material
  // and never punch chrome transparent over the BrowserWindow color.
  if (platform === 'linux') {
    document.documentElement.dataset.backdrop = material
    void window.bridge?.setBackgroundMaterial?.('none')
    return
  }

  document.documentElement.dataset.backdrop = material
  void window.bridge?.setBackgroundMaterial(material)
}

export async function loadWindowBackdrop(): Promise<void> {
  const result = await settingsApi.get('application')
  const application = result.value as Record<string, unknown> | null
  applyWindowBackdrop(normalizeWindowBackdropStyle(application?.['WindowBackdropStyle']))
}
