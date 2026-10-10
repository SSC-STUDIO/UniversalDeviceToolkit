/**
 * Single source of truth for renderer zoom.
 *
 * effectiveZoom = platform base density x user "Interface scale" setting.
 * The renderer pushes its persisted uiScale over IPC at startup and on change;
 * the main process applies the product to every surface (main window, OSD,
 * status window, tray popup) via
 * webContents.setZoomFactor. Zoom factor - unlike CSS zoom - keeps @media,
 * @container and window.devicePixelRatio consistent with each other.
 */
import { app, webContents, type WebContents } from 'electron'

/**
 * Windows display scale vs. original client DPI correction. Electron renders
 * in DIPs while Chromium applies the Windows display scale to CSS; the 5/6
 * factor keeps the renderer at the original client's physical density.
 * Linux/macOS map one CSS px to one DIP, so the base stays 1 there.
 */
export const PLATFORM_BASE_ZOOM = process.platform === 'win32' ? 5 / 6 : 1

/** Selectable range guard; the settings UI offers 0.9 / 1 / 1.1 / 1.25 / 1.5. */
const MIN_UI_SCALE = 0.75
const MAX_UI_SCALE = 1.5

let uiScale = 1

export function effectiveZoom(): number {
  return PLATFORM_BASE_ZOOM * uiScale
}

/** Clamps, stores and applies the user scale. Returns the applied value. */
export function setUiScale(scale: number): number {
  if (!Number.isFinite(scale) || scale <= 0) return uiScale
  uiScale = Math.min(MAX_UI_SCALE, Math.max(MIN_UI_SCALE, scale))
  applyZoomToAllSurfaces()
  return uiScale
}

/** Applies the current effective zoom to one webContents. */
export function applyZoomTo(contents: WebContents): void {
  if (contents.isDestroyed()) return
  const zoom = effectiveZoom()
  if (Math.abs(contents.getZoomFactor() - zoom) > 0.0001) {
    contents.setZoomFactor(zoom)
  }
}

/** Re-applies the effective zoom to every window. */
export function applyZoomToAllSurfaces(): void {
  for (const contents of webContents.getAllWebContents()) {
    const type = contents.getType()
    if (type === 'window' || type === 'webview') {
      applyZoomTo(contents)
    }
  }
}

/**
 * Keeps newly created windows at the effective
 * zoom. dom-ready re-applies after each navigation because Chromium tracks
 * zoom per origin and data:/file: loads can reset it.
 */
export function installZoomAutoApply(): void {
  app.on('web-contents-created', (_event, contents) => {
    const type = contents.getType()
    if (type !== 'window' && type !== 'webview') return
    applyZoomTo(contents)
    contents.on('dom-ready', () => applyZoomTo(contents))
  })
}
