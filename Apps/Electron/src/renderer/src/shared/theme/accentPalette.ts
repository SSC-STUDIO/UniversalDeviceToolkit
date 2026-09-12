/**
 * Accent-derived surface palette, ported from the WPF ThemeManager
 * (CreateAccentPalette / ApplyStylePreset in UniversalDeviceToolkit.WPF).
 *
 * When the "Apply the theme style when adjusting the global interface color"
 * setting (ApplyAccentColorToTheme) is enabled, the accent color tints the
 * surface layer: window background, cards, controls, strokes and secondary
 * text all get a restrained hue from the same family. Disabling the setting
 * clears the overrides so the neutral surfaces from global.css apply.
 */

export interface RgbColor {
  r: number
  g: number
  b: number
}

export interface AccentPalette {
  applicationBackground: string
  controlFillDefault: string
  controlFillSecondary: string
  controlFillTertiary: string
  controlStrokeDefault: string
  controlStrokeSecondary: string
  controlElevationBorder: string
  cardStroke: string
  textSecondary: string
  snackbarShadow: string
}

function clampByte(value: number): number {
  return Math.max(0, Math.min(255, Math.round(value)))
}

/** WPF ThemeManager.BlendToward: linear interpolation toward the accent. */
function blendToward(from: RgbColor, to: RgbColor, amount: number): RgbColor {
  const t = Math.max(0, Math.min(1, amount))
  return {
    r: clampByte(from.r + (to.r - from.r) * t),
    g: clampByte(from.g + (to.g - from.g) * t),
    b: clampByte(from.b + (to.b - from.b) * t)
  }
}

function hexToRgb(hex: string): RgbColor {
  const value = hex.replace('#', '')
  const parsed = Number.parseInt(value, 16)
  if (!/^[0-9a-f]{6}$/i.test(value) || Number.isNaN(parsed)) {
    return { r: 255, g: 33, b: 33 }
  }
  return { r: (parsed >> 16) & 0xff, g: (parsed >> 8) & 0xff, b: parsed & 0xff }
}

function rgbToHex(color: RgbColor): string {
  const toHex = (value: number): string => value.toString(16).padStart(2, '0')
  return `#${toHex(color.r)}${toHex(color.g)}${toHex(color.b)}`
}

function rgba(color: RgbColor, alpha: number): string {
  return `rgba(${color.r}, ${color.g}, ${color.b}, ${alpha})`
}

/** WPF ThemeManager.CreateAccentPalette port with identical blend targets. */
export function createAccentPalette(accentHex: string, isDark: boolean): AccentPalette {
  const accent = hexToRgb(accentHex)

  if (isDark) {
    const shadow = blendToward({ r: 8, g: 10, b: 14 }, accent, 0.35)
    return {
      applicationBackground: rgbToHex(blendToward({ r: 18, g: 20, b: 24 }, accent, 0.1)),
      controlFillDefault: rgbToHex(blendToward({ r: 27, g: 29, b: 34 }, accent, 0.14)),
      controlFillSecondary: rgbToHex(blendToward({ r: 35, g: 38, b: 45 }, accent, 0.18)),
      controlFillTertiary: rgbToHex(blendToward({ r: 44, g: 48, b: 57 }, accent, 0.22)),
      controlStrokeDefault: rgbToHex(blendToward({ r: 72, g: 77, b: 88 }, accent, 0.45)),
      controlStrokeSecondary: rgbToHex(blendToward({ r: 94, g: 99, b: 112 }, accent, 0.38)),
      controlElevationBorder: rgbToHex(blendToward({ r: 80, g: 86, b: 98 }, accent, 0.5)),
      cardStroke: rgbToHex(blendToward({ r: 89, g: 95, b: 108 }, accent, 0.44)),
      textSecondary: rgbToHex(blendToward({ r: 185, g: 189, b: 197 }, accent, 0.18)),
      snackbarShadow: rgba(shadow, 160 / 255)
    }
  }

  const lightShadow = blendToward({ r: 40, g: 45, b: 55 }, accent, 0.35)
  return {
    applicationBackground: rgbToHex(blendToward({ r: 248, g: 249, b: 251 }, accent, 0.06)),
    controlFillDefault: rgbToHex(blendToward({ r: 240, g: 243, b: 247 }, accent, 0.1)),
    controlFillSecondary: rgbToHex(blendToward({ r: 232, g: 236, b: 242 }, accent, 0.14)),
    controlFillTertiary: rgbToHex(blendToward({ r: 222, g: 228, b: 236 }, accent, 0.18)),
    controlStrokeDefault: rgbToHex(blendToward({ r: 165, g: 173, b: 185 }, accent, 0.38)),
    controlStrokeSecondary: rgbToHex(blendToward({ r: 133, g: 143, b: 159 }, accent, 0.42)),
    controlElevationBorder: rgbToHex(blendToward({ r: 150, g: 160, b: 177 }, accent, 0.35)),
    cardStroke: rgbToHex(blendToward({ r: 126, g: 139, b: 158 }, accent, 0.42)),
    textSecondary: rgbToHex(blendToward({ r: 75, g: 81, b: 92 }, accent, 0.18)),
    snackbarShadow: rgba(lightShadow, 72 / 255)
  }
}

/** CSS custom properties the palette tints (removed to restore global.css defaults).
 *  The navigation surface stays neutral on purpose: the nav pane must remain
 *  visually distinct from the accent-tinted content area in both themes.
 *  Strokes stay on the global.css tokens too: blending them toward the accent
 *  turns feature cards into colored boxes, especially with Windows blue. */
const SURFACE_VARIABLES: ReadonlyArray<{ slot: keyof AccentPalette; variable: string }> = [
  { slot: 'applicationBackground', variable: '--udt-surface-window' },
  { slot: 'controlFillDefault', variable: '--udt-surface-card' },
  { slot: 'controlFillDefault', variable: '--udt-control-fill-default' },
  { slot: 'controlFillSecondary', variable: '--udt-control-fill-secondary' },
  { slot: 'controlFillTertiary', variable: '--udt-control-fill-tertiary' },
  { slot: 'textSecondary', variable: '--udt-text-secondary' }
]

/** Applies the tinted palette to the document surface variables. */
export function applyAccentSurfacePalette(palette: AccentPalette): void {
  const root = document.documentElement
  for (const { slot, variable } of SURFACE_VARIABLES) {
    root.style.setProperty(variable, palette[slot])
  }
}

/** Removes the tint overrides so the neutral default surfaces take over. */
export function clearAccentSurfacePalette(): void {
  const root = document.documentElement
  for (const { variable } of SURFACE_VARIABLES) {
    root.style.removeProperty(variable)
  }
}
