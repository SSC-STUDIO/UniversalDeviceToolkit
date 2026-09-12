/**
 * Interface scale preference and the Auto mapping from window size.
 *
 * Manual values are the settings steps (0.9 / 1 / 1.1 / 1.25 / 1.5). Auto
 * interpolates from 110% at the design minimum width to 136% at a typical
 * full-HD window in 1% steps, so window resizing tracks almost continuously.
 * Layout width is taken from outerWidth so zoomFactor (or CSS zoom) cannot
 * feed back into the computed scale.
 */

export const UI_SCALE_AUTO = 'auto' as const
export const UI_SCALE_OPTIONS = [0.9, 1, 1.1, 1.25, 1.5] as const
export type UiScale = (typeof UI_SCALE_OPTIONS)[number]
export type UiScalePreference = typeof UI_SCALE_AUTO | UiScale

/** Matches the main-window design minimum (content CSS px at scale 1). */
export const UI_SCALE_AUTO_MIN_WIDTH = 1024
/** Width at which Auto reaches the ceiling (typical FHD work area). */
export const UI_SCALE_AUTO_MAX_WIDTH = 1920
/** Auto floor: former 100% baseline, raised by about 10%. */
export const UI_SCALE_MIN = 1.1
/** Auto ceiling, about 10% above the old 125% cap. */
export const UI_SCALE_MAX = 1.36
/** Auto snaps to 1% so the curve stays fine-grained while resizing. */
export const UI_SCALE_AUTO_STEP = 0.01
/**
 * Ignore outerWidth jitter below one Auto step (~34 CSS px). Zoom-factor
 * apply and window-frame rounding can move outerWidth by a few pixels
 * without a user resize; treating those as real width changes retriggers
 * Auto and feeds back into setZoomFactor / CSS zoom.
 */
export const UI_SCALE_AUTO_WIDTH_DEADZONE = 8

const MIN_LAYOUT_WIDTH = 200

export function isUiScaleOption(value: number): value is UiScale {
  return (UI_SCALE_OPTIONS as readonly number[]).includes(value)
}

export function parseUiScalePreference(stored: string | null): UiScalePreference | null {
  if (stored === UI_SCALE_AUTO) return UI_SCALE_AUTO
  if (stored == null) return null
  const parsed = Number(stored)
  return isUiScaleOption(parsed) ? parsed : null
}

function snapAutoUiScale(value: number): number {
  const snapped = Math.round(value / UI_SCALE_AUTO_STEP) * UI_SCALE_AUTO_STEP
  const rounded = Math.round(snapped * 100) / 100
  return Math.min(UI_SCALE_MAX, Math.max(UI_SCALE_MIN, rounded))
}

/**
 * Maps a DIP / outer window width onto the Auto scale. Invalid widths fall
 * back to the 110% floor so a hidden or not-yet-measured window does not
 * collapse the UI.
 */
export function computeAutoUiScale(layoutWidth: number): number {
  if (!Number.isFinite(layoutWidth) || layoutWidth < MIN_LAYOUT_WIDTH) return UI_SCALE_MIN
  const span = UI_SCALE_AUTO_MAX_WIDTH - UI_SCALE_AUTO_MIN_WIDTH
  const t = (layoutWidth - UI_SCALE_AUTO_MIN_WIDTH) / span
  const raw = UI_SCALE_MIN + t * (UI_SCALE_MAX - UI_SCALE_MIN)
  return snapAutoUiScale(raw)
}

export function readLayoutWidth(): number {
  if (typeof window === 'undefined') return UI_SCALE_AUTO_MIN_WIDTH
  const outer = window.outerWidth
  if (Number.isFinite(outer) && outer >= MIN_LAYOUT_WIDTH) return outer
  // Never fall back to innerWidth: zoomFactor and CSS zoom shrink it, so
  // Auto would recompute from the already-scaled viewport and loop.
  return UI_SCALE_AUTO_MIN_WIDTH
}

/** True when the new outer width is a real user resize, not zoom jitter. */
export function layoutWidthChanged(previous: number, next: number): boolean {
  return Math.abs(next - previous) >= UI_SCALE_AUTO_WIDTH_DEADZONE
}

export function resolveUiScale(preference: UiScalePreference): number {
  if (preference === UI_SCALE_AUTO) return computeAutoUiScale(readLayoutWidth())
  return preference
}
