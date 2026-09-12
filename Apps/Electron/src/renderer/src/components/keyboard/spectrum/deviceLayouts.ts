/**
 * Spectrum device front-panel geometry — port of the Electron
 * SpectrumDeviceFullControl / FullAlternativeControl / KeyboardAndFrontControl
 * XAML zone tables (extracted with a XAML parser: Grid.Row/Column, KeyCode,
 * Margin, Width/Height). Electron uses an Auto-sized Grid; positions here are the
 * resolved pixel layout on the device canvas:
 *
 *  - the keyboard stage is 660x252 (6px padding, 4px row gaps) — see
 *    SpectrumKeyboard.tsx for the same constants;
 *  - device columns/rows are sized from the keyboard's internal row heights
 *    (24px function-key row, 32px rows) plus 2px zone margins;
 *  - zones that span the keyboard width (rear vents, front strip) are split
 *    evenly across the 6 keyboard columns (660 / 6 = 110px).
 */

import {
  SPECTRUM_KEYBOARD_DESIGN,
  SPECTRUM_KEYBOARD_LAYOUTS,
  KEYBOARD_STAGE_PADDING,
  enumerateSpectrumZones,
  getSpectrumLayoutBounds,
  type SpectrumKeyboardLayoutName
} from './keyboardLayouts'

/** Front panel / device zone: absolute rect on the device canvas. */
export interface SpectrumDeviceZone {
  x: number
  y: number
  w: number
  h: number
  code: number
}

export interface SpectrumDeviceLayout {
  name: SpectrumDevicePanelLayout
  /** Device canvas size in design px (before zoom). */
  width: number
  height: number
  /** Where the nested SpectrumKeyboard sits on the canvas. */
  keyboard: { x: number; y: number; w: number; h: number }
  zones: SpectrumDeviceZone[]
}

export type SpectrumDeviceLayoutName =
  | 'KeyboardOnly'
  | 'KeyboardAndFront'
  | 'Full'
  | 'FullAlternative'

export type SpectrumDevicePanelLayout = Exclude<SpectrumDeviceLayoutName, 'KeyboardOnly'>

/** 6 even keyboard columns of the Electron grid (keyboard spans cols 1-6). */
const COL_WIDTH = 110
/** Side columns next to the keyboard (28px zone + 2px margins each side). */
const SIDE_COL_WIDTH = 32
const SIDE_ZONE_W = 28

/** Deck padding around the WPF keyboard grid (imported from keyboardLayouts). */

const keyboardBox = (
  x: number,
  y: number,
  keyboardLayout: SpectrumKeyboardLayoutName = 'Ansi'
): { x: number; y: number; w: number; h: number } => {
  const bounds = getSpectrumLayoutBounds(SPECTRUM_KEYBOARD_LAYOUTS[keyboardLayout])
  return {
    x,
    y,
    w: bounds.width,
    h: bounds.height + KEYBOARD_STAGE_PADDING * 2
  }
}

/** Zone on a 24px Electron row (logo / rear vents / front strip): y + margin 2. */
const slimZone = (col: number, y: number, code: number, w = COL_WIDTH - 4): SpectrumDeviceZone => ({
  x: 2 + col * COL_WIDTH,
  y: y + 2,
  w,
  h: 20,
  code
})

/** Side zone aligned to a keyboard row inside the keyboard box. */
const sideZone = (x: number, kbY: number, rowTop: number, rowH: number, code: number): SpectrumDeviceZone => ({
  x,
  y: kbY + rowTop,
  w: SIDE_ZONE_W,
  h: rowH,
  code
})

/**
 * Keyboard rows (top offsets within the padded stage): row 0 is the 24px
 * function-key row, rows 1-6 are 32px each with 4px gaps.
 */
const KEYBOARD_ROWS: ReadonlyArray<{ top: number; h: number }> = [
  { top: KEYBOARD_STAGE_PADDING, h: 24 },
  { top: KEYBOARD_STAGE_PADDING + 28, h: 32 },
  { top: KEYBOARD_STAGE_PADDING + 64, h: 32 },
  { top: KEYBOARD_STAGE_PADDING + 100, h: 32 },
  { top: KEYBOARD_STAGE_PADDING + 136, h: 32 },
  { top: KEYBOARD_STAGE_PADDING + 172, h: 32 },
  { top: KEYBOARD_STAGE_PADDING + 208, h: 32 }
]

const rearVentsFull = (y: number): SpectrumDeviceZone[] => [
  slimZone(1, y, 0x03eb),
  slimZone(2, y, 0x03ec),
  slimZone(5, y, 0x03ed),
  slimZone(6, y, 0x03ee)
]

const rearVentsFullAlternative = (y: number): SpectrumDeviceZone[] => [
  slimZone(1, y, 0x03ea),
  slimZone(2, y, 0x03eb),
  slimZone(3, y, 0x03ec),
  slimZone(4, y, 0x03ed),
  slimZone(5, y, 0x03ee),
  slimZone(6, y, 0x03ef)
]

const frontStrip = (y: number, codes: number[]): SpectrumDeviceZone[] =>
  codes.map((code, col) => slimZone(col, y, code))

const sideStrip = (
  x: number,
  kbY: number,
  zones: Array<{ row: number; code: number }>
): SpectrumDeviceZone[] =>
  zones.map((zone) => {
    const row = KEYBOARD_ROWS[zone.row]
    return sideZone(x, kbY, row.top, row.h, zone.code)
  })

const logo = (): SpectrumDeviceZone => ({ x: 617, y: 0, w: 36, h: 32, code: 0x05dd })

/** Full = keyboard + rear vents + side strips + front strip + panel logo. */
const FULL: SpectrumDeviceLayout = {
  name: 'Full',
  width: 660 + SIDE_COL_WIDTH * 2,
  height: 356,
  keyboard: keyboardBox(SIDE_COL_WIDTH, 80),
  zones: [
    logo(),
    ...rearVentsFull(56),
    ...sideStrip(2, 80, [
      { row: 0, code: 0x03ea },
      { row: 1, code: 0x03e9 },
      { row: 4, code: 0x01f5 },
      { row: 5, code: 0x01f6 }
    ]),
    ...sideStrip(660 + SIDE_COL_WIDTH + 2, 80, [
      { row: 0, code: 0x03ef },
      { row: 1, code: 0x03f0 },
      { row: 4, code: 0x01fe },
      { row: 5, code: 0x01fd }
    ]),
    ...frontStrip(332, [0x01f7, 0x01f8, 0x01f9, 0x01fa, 0x01fb, 0x01fc])
  ]
}

/** FullAlternative = six rear vents + side strips (short top zones) + front. */
const FULL_ALTERNATIVE: SpectrumDeviceLayout = {
  name: 'FullAlternative',
  width: 660 + SIDE_COL_WIDTH * 2,
  height: 356,
  keyboard: keyboardBox(SIDE_COL_WIDTH, 80),
  zones: [
    logo(),
    ...rearVentsFullAlternative(56),
    // Top side zones are 24px and top-aligned (explicit Height="24" in XAML).
    ...sideStrip(2, 80, [
      { row: 0, code: 0x03e9 },
      { row: 4, code: 0x01f5 },
      { row: 5, code: 0x01f6 }
    ]).map((zone, index) => (index === 0 ? { ...zone, h: 24 } : zone)),
    ...sideStrip(660 + SIDE_COL_WIDTH + 2, 80, [
      { row: 0, code: 0x03f0 },
      { row: 4, code: 0x01fe },
      { row: 5, code: 0x01fd }
    ]).map((zone, index) => (index === 0 ? { ...zone, h: 24 } : zone)),
    ...frontStrip(332, [0x01f7, 0x01f8, 0x01f9, 0x01fa, 0x01fb, 0x01fc])
  ]
}

/** KeyboardAndFront = keyboard on top, six front zones below. */
const KEYBOARD_AND_FRONT: SpectrumDeviceLayout = {
  name: 'KeyboardAndFront',
  width: 660,
  height: 276,
  keyboard: keyboardBox(0, 0),
  zones: frontStrip(SPECTRUM_KEYBOARD_DESIGN.height + KEYBOARD_STAGE_PADDING * 2, [
    0x01f5, 0x01f6, 0x01f7, 0x01f8, 0x01f9, 0x01fa
  ])
}

export const SPECTRUM_DEVICE_LAYOUTS: Record<SpectrumDevicePanelLayout, SpectrumDeviceLayout> = {
  Full: FULL,
  FullAlternative: FULL_ALTERNATIVE,
  KeyboardAndFront: KEYBOARD_AND_FRONT
}

export function normalizeSpectrumLayout(name: string): SpectrumDeviceLayoutName {
  const lowered = name.toLowerCase()
  if (lowered.includes('fullalternative')) return 'FullAlternative'
  if (lowered.includes('full')) return 'Full'
  if (lowered.includes('keyboardandfront')) return 'KeyboardAndFront'
  return 'KeyboardOnly'
}

export interface SpectrumZoneCenter {
  code: number
  /** Center in "canvas px" (keyboard stage px, unscaled by the device zoom). */
  x: number
  y: number
}

/**
 * Center points of every key zone in the given keyboard layout, computed from
 * the stage geometry (6px padding, 4px row gaps, zone w/h). Used for
 * box-select hit testing. `offsetX/offsetY` map the keyboard onto a device
 * canvas (the nested keyboard renders at zoom 1 inside the device stage).
 */
export function getKeyboardZoneCenters(
  layout: SpectrumKeyboardLayoutName,
  scale: number,
  offsetX = 0,
  offsetY = 0
): SpectrumZoneCenter[] {
  const keyboardLayout = SPECTRUM_KEYBOARD_LAYOUTS[layout]
  return enumerateSpectrumZones(keyboardLayout)
    .filter((entry): entry is typeof entry & { zone: { code: number } } => entry.zone.code !== null)
    .map((entry) => ({
      code: entry.zone.code,
      x: (offsetX + KEYBOARD_STAGE_PADDING + entry.x + entry.zone.w / 2) * scale,
      y: (offsetY + KEYBOARD_STAGE_PADDING + entry.y + entry.zone.h / 2) * scale
    }))
}
