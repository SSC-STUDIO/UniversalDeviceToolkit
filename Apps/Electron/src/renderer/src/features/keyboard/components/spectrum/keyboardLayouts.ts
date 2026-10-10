/**
 * Spectrum keyboard layout geometry — port of the WPF
 * SpectrumKeyboardANSIControl/ISOControl/JisControl.xaml zone tables
 * (Width x Height @ KeyCode, 2px margins; null code = non-selectable spacer).
 *
 * WPF uses a 660x244 two-column grid:
 *   row 0: function keys (left 0x01-0x10, right 0x11-0x14 above numpad)
 *   row 1: main keyboard block (col 0) + 4x5 numpad grid (col 1)
 */

export interface SpectrumZone {
  w: number
  h: number
  code: number | null
}

export interface SpectrumPlacedZone extends SpectrumZone {
  row: number
  col: number
  rowSpan?: number
  colSpan?: number
}

export interface SpectrumKeyboardLayout {
  functionRow: { left: SpectrumZone[]; right: SpectrumZone[] }
  mainRows: SpectrumZone[][]
  numpad: SpectrumPlacedZone[]
}

/** WPF inner grid size (Viewbox content, before deck padding). */
export const SPECTRUM_KEYBOARD_DESIGN = {
  width: 660,
  /** Legacy default; prefer getSpectrumLayoutBounds() per layout. */
  height: 244,
  rowGap: 4,
  /** WPF numpad grid cell stride (32px key + 4px gap). */
  numpadCell: 36,
  /** Function-key row height (24px key + 2px margins). */
  fnRowHeight: 28,
  /** Main-body row stride when every row is 32px tall. */
  bodyRowStride: 36
} as const

export const KEYBOARD_STAGE_PADDING = 6

const NUMPAD: SpectrumPlacedZone[] = [
  { row: 0, col: 0, w: 32, h: 32, code: 0x26 },
  { row: 0, col: 1, w: 32, h: 32, code: 0x27 },
  { row: 0, col: 2, w: 32, h: 32, code: 0x28 },
  { row: 0, col: 3, w: 32, h: 32, code: 0x29 },
  { row: 1, col: 0, w: 32, h: 32, code: 0x4f },
  { row: 1, col: 1, w: 32, h: 32, code: 0x50 },
  { row: 1, col: 2, w: 32, h: 32, code: 0x51 },
  { row: 1, col: 3, rowSpan: 2, w: 32, h: 68, code: 0x68 },
  { row: 2, col: 0, w: 32, h: 32, code: 0x79 },
  { row: 2, col: 1, w: 32, h: 32, code: 0x7b },
  { row: 2, col: 2, w: 32, h: 32, code: 0x7c },
  { row: 3, col: 0, w: 32, h: 32, code: 0x8e },
  { row: 3, col: 1, w: 32, h: 32, code: 0x90 },
  { row: 3, col: 2, w: 32, h: 32, code: 0x92 },
  { row: 3, col: 3, rowSpan: 2, w: 32, h: 68, code: 0xa7 },
  { row: 4, col: 0, colSpan: 2, w: 68, h: 32, code: 0xa3 },
  { row: 4, col: 2, w: 32, h: 32, code: 0xa5 }
]

const FN_LEFT: SpectrumZone[] = [
  { w: 28, h: 24, code: 0x01 },
  { w: 28, h: 24, code: 0x02 },
  { w: 28, h: 24, code: 0x03 },
  { w: 28, h: 24, code: 0x04 },
  { w: 28, h: 24, code: 0x05 },
  { w: 28, h: 24, code: 0x06 },
  { w: 28, h: 24, code: 0x07 },
  { w: 28, h: 24, code: 0x08 },
  { w: 28, h: 24, code: 0x09 },
  { w: 28, h: 24, code: 0x0a },
  { w: 28, h: 24, code: 0x0b },
  { w: 28, h: 24, code: 0x0c },
  { w: 28, h: 24, code: 0x0d },
  { w: 28, h: 24, code: 0x0e },
  { w: 28, h: 24, code: 0x0f },
  { w: 32, h: 24, code: 0x10 }
]

const FN_RIGHT: SpectrumZone[] = [
  { w: 32, h: 24, code: 0x11 },
  { w: 32, h: 24, code: 0x12 },
  { w: 32, h: 24, code: 0x13 },
  { w: 32, h: 24, code: 0x14 }
]

const ANSI_MAIN: SpectrumZone[][] = [
  [
    { w: 24, h: 32, code: 0x16 },
    { w: 32, h: 32, code: 0x17 },
    { w: 32, h: 32, code: 0x18 },
    { w: 32, h: 32, code: 0x19 },
    { w: 32, h: 32, code: 0x1a },
    { w: 32, h: 32, code: 0x1b },
    { w: 32, h: 32, code: 0x1c },
    { w: 32, h: 32, code: 0x1d },
    { w: 32, h: 32, code: 0x1e },
    { w: 32, h: 32, code: 0x1f },
    { w: 32, h: 32, code: 0x20 },
    { w: 32, h: 32, code: 0x21 },
    { w: 32, h: 32, code: 0x22 },
    { w: 52, h: 32, code: 0x38 }
  ],
  [
    { w: 44, h: 32, code: 0x40 },
    { w: 32, h: 32, code: 0x42 },
    { w: 32, h: 32, code: 0x43 },
    { w: 32, h: 32, code: 0x44 },
    { w: 32, h: 32, code: 0x45 },
    { w: 32, h: 32, code: 0x46 },
    { w: 32, h: 32, code: 0x47 },
    { w: 32, h: 32, code: 0x48 },
    { w: 32, h: 32, code: 0x49 },
    { w: 32, h: 32, code: 0x4a },
    { w: 32, h: 32, code: 0x4b },
    { w: 32, h: 32, code: 0x4c },
    { w: 32, h: 32, code: 0x4d },
    { w: 32, h: 32, code: 0x4e }
  ],
  [
    { w: 56, h: 32, code: 0x55 },
    { w: 32, h: 32, code: 0x6d },
    { w: 32, h: 32, code: 0x6e },
    { w: 32, h: 32, code: 0x58 },
    { w: 32, h: 32, code: 0x59 },
    { w: 32, h: 32, code: 0x5a },
    { w: 32, h: 32, code: 0x71 },
    { w: 32, h: 32, code: 0x72 },
    { w: 32, h: 32, code: 0x5b },
    { w: 32, h: 32, code: 0x5c },
    { w: 32, h: 32, code: 0x5d },
    { w: 32, h: 32, code: 0x5f },
    { w: 56, h: 32, code: 0x77 }
  ],
  [
    { w: 74, h: 32, code: 0x6a },
    { w: 32, h: 32, code: 0x82 },
    { w: 32, h: 32, code: 0x83 },
    { w: 32, h: 32, code: 0x6f },
    { w: 32, h: 32, code: 0x70 },
    { w: 32, h: 32, code: 0x87 },
    { w: 32, h: 32, code: 0x88 },
    { w: 32, h: 32, code: 0x73 },
    { w: 32, h: 32, code: 0x74 },
    { w: 32, h: 32, code: 0x75 },
    { w: 32, h: 32, code: 0x76 },
    { w: 74, h: 32, code: 0x8d }
  ],
  [
    { w: 38, h: 32, code: 0x7f },
    { w: 32, h: 32, code: 0x80 },
    { w: 32, h: 32, code: 0x96 },
    { w: 32, h: 32, code: 0x97 },
    { w: 176, h: 32, code: 0x98 },
    { w: 32, h: 32, code: 0x9a },
    { w: 32, h: 32, code: 0x9b },
    { w: 32, h: 32, code: null },
    { w: 32, h: 32, code: 0x9d },
    { w: 32, h: 32, code: null }
  ],
  [
    { w: 398, h: 32, code: null },
    { w: 32, h: 32, code: 0x9c },
    { w: 32, h: 32, code: 0x9f },
    { w: 32, h: 32, code: 0xa1 }
  ]
]

export const SPECTRUM_KEYBOARD_LAYOUTS: Record<'Ansi' | 'Iso' | 'Jis', SpectrumKeyboardLayout> = {
  Ansi: {
    functionRow: { left: FN_LEFT, right: FN_RIGHT },
    mainRows: ANSI_MAIN,
    numpad: NUMPAD
  },
  Iso: {
    functionRow: { left: FN_LEFT, right: FN_RIGHT },
    mainRows: [
      ANSI_MAIN[0],
      ANSI_MAIN[1],
      [
        { w: 44, h: 32, code: 0x40 },
        { w: 32, h: 32, code: 0x42 },
        { w: 32, h: 32, code: 0x43 },
        { w: 32, h: 32, code: 0x44 },
        { w: 32, h: 32, code: 0x45 },
        { w: 32, h: 32, code: 0x46 },
        { w: 32, h: 32, code: 0x47 },
        { w: 32, h: 32, code: 0x48 },
        { w: 32, h: 32, code: 0x49 },
        { w: 32, h: 32, code: 0x4a },
        { w: 32, h: 32, code: 0x4b },
        { w: 32, h: 32, code: 0x4c },
        { w: 36, h: 32, code: 0x4d }
      ],
      [
        { w: 56, h: 32, code: 0x55 },
        { w: 32, h: 32, code: 0x6d },
        { w: 32, h: 32, code: 0x6e },
        { w: 32, h: 32, code: 0x58 },
        { w: 32, h: 32, code: 0x59 },
        { w: 32, h: 32, code: 0x5a },
        { w: 32, h: 32, code: 0x71 },
        { w: 32, h: 32, code: 0x72 },
        { w: 32, h: 32, code: 0x5b },
        { w: 32, h: 32, code: 0x5c },
        { w: 32, h: 32, code: 0x5d },
        { w: 32, h: 32, code: 0x5f },
        { w: 24, h: 32, code: 0xa8 }
      ],
      [
        { w: 28, h: 68, code: 0x77 },
        { w: 38, h: 32, code: 0x6a },
        { w: 32, h: 32, code: 0x4e },
        { w: 32, h: 32, code: 0x82 },
        { w: 32, h: 32, code: 0x83 },
        { w: 32, h: 32, code: 0x6f },
        { w: 32, h: 32, code: 0x70 },
        { w: 32, h: 32, code: 0x87 },
        { w: 32, h: 32, code: 0x88 },
        { w: 32, h: 32, code: 0x73 },
        { w: 32, h: 32, code: 0x74 },
        { w: 32, h: 32, code: 0x75 },
        { w: 32, h: 32, code: 0x76 },
        { w: 74, h: 32, code: 0x8d }
      ],
      ANSI_MAIN[4],
      ANSI_MAIN[5]
    ],
    numpad: NUMPAD
  },
  Jis: {
    functionRow: { left: FN_LEFT, right: FN_RIGHT },
    mainRows: [
      [
        { w: 24, h: 32, code: 0x16 },
        { w: 32, h: 32, code: 0x17 },
        { w: 32, h: 32, code: 0x18 },
        { w: 32, h: 32, code: 0x19 },
        { w: 32, h: 32, code: 0x1a },
        { w: 32, h: 32, code: 0x1b },
        { w: 32, h: 32, code: 0x1c },
        { w: 32, h: 32, code: 0x1d },
        { w: 32, h: 32, code: 0x1e },
        { w: 32, h: 32, code: 0x1f },
        { w: 32, h: 32, code: 0x20 },
        { w: 32, h: 32, code: 0x21 },
        { w: 32, h: 32, code: 0x22 },
        { w: 24, h: 32, code: 0xa8 },
        { w: 24, h: 32, code: 0x38 }
      ],
      [
        { w: 44, h: 32, code: 0x40 },
        { w: 32, h: 32, code: 0x42 },
        { w: 32, h: 32, code: 0x43 },
        { w: 32, h: 32, code: 0x44 },
        { w: 32, h: 32, code: 0x45 },
        { w: 32, h: 32, code: 0x46 },
        { w: 32, h: 32, code: 0x47 },
        { w: 32, h: 32, code: 0x48 },
        { w: 32, h: 32, code: 0x49 },
        { w: 32, h: 32, code: 0x4a },
        { w: 32, h: 32, code: 0x4b },
        { w: 32, h: 32, code: 0x60 },
        { w: 36, h: 32, code: 0x4c }
      ],
      [
        { w: 56, h: 32, code: 0x55 },
        { w: 32, h: 32, code: 0x6d },
        { w: 32, h: 32, code: 0x6e },
        { w: 32, h: 32, code: 0x58 },
        { w: 32, h: 32, code: 0x59 },
        { w: 32, h: 32, code: 0x5a },
        { w: 32, h: 32, code: 0x71 },
        { w: 32, h: 32, code: 0x72 },
        { w: 32, h: 32, code: 0x5b },
        { w: 32, h: 32, code: 0x5c },
        { w: 32, h: 32, code: 0x5d },
        { w: 32, h: 32, code: 0x5f },
        { w: 24, h: 32, code: 0x4d }
      ],
      [
        { w: 28, h: 68, code: 0x77 },
        { w: 38, h: 32, code: 0x6a },
        { w: 32, h: 32, code: 0x4e },
        { w: 32, h: 32, code: 0x82 },
        { w: 32, h: 32, code: 0x83 },
        { w: 32, h: 32, code: 0x6f },
        { w: 32, h: 32, code: 0x70 },
        { w: 32, h: 32, code: 0x87 },
        { w: 32, h: 32, code: 0x88 },
        { w: 32, h: 32, code: 0x73 },
        { w: 32, h: 32, code: 0x74 },
        { w: 32, h: 32, code: 0x75 },
        { w: 32, h: 32, code: 0x76 },
        { w: 74, h: 32, code: 0x8d }
      ],
      [
        { w: 38, h: 32, code: 0x7f },
        { w: 32, h: 32, code: 0x80 },
        { w: 32, h: 32, code: 0x96 },
        { w: 32, h: 32, code: 0x97 },
        { w: 32, h: 32, code: 0xa9 },
        { w: 104, h: 32, code: 0x98 },
        { w: 32, h: 32, code: 0xaa },
        { w: 32, h: 32, code: 0xab },
        { w: 32, h: 32, code: 0x9b },
        { w: 32, h: 32, code: null },
        { w: 32, h: 32, code: 0x9d },
        { w: 32, h: 32, code: null }
      ],
      ANSI_MAIN[5]
    ],
    numpad: NUMPAD
  }
}

export type SpectrumKeyboardLayoutName = keyof typeof SPECTRUM_KEYBOARD_LAYOUTS

export function normalizeKeyboardLayout(name: string): SpectrumKeyboardLayoutName {
  const lowered = name.toLowerCase()
  if (lowered.includes('iso')) return 'Iso'
  if (lowered.includes('jis')) return 'Jis'
  return 'Ansi'
}

function rowWidth(zones: SpectrumZone[]): number {
  if (zones.length === 0) return 0
  return zones.reduce((sum, zone) => sum + zone.w, 0) + (zones.length - 1) * SPECTRUM_KEYBOARD_DESIGN.rowGap
}

/** Width of the main keyboard column (widest main row). */
export function getMainColumnWidth(layout: SpectrumKeyboardLayout): number {
  return layout.mainRows.reduce((max, row) => Math.max(max, rowWidth(row)), 0)
}

export interface SpectrumZonePlacement {
  zone: SpectrumZone
  x: number
  y: number
}

function placeRow(zones: SpectrumZone[], x: number, y: number): SpectrumZonePlacement[] {
  const placed: SpectrumZonePlacement[] = []
  let cursor = x
  for (const zone of zones) {
    placed.push({ zone, x: cursor, y })
    cursor += zone.w + SPECTRUM_KEYBOARD_DESIGN.rowGap
  }
  return placed
}

function fnRowHeight(layout: SpectrumKeyboardLayout): number {
  const left = layout.functionRow.left.reduce((max, zone) => Math.max(max, zone.h), 0)
  const right = layout.functionRow.right.reduce((max, zone) => Math.max(max, zone.h), 0)
  return Math.max(left, right, 24)
}

function rowHeight(row: SpectrumZone[]): number {
  if (row.length === 0) return 0
  return row.reduce((max, zone) => Math.max(max, zone.h), 0)
}

/** Absolute zone positions inside the WPF keyboard grid (no deck padding). */
export function enumerateSpectrumZones(layout: SpectrumKeyboardLayout): SpectrumZonePlacement[] {
  const { rowGap, numpadCell } = SPECTRUM_KEYBOARD_DESIGN
  const mainColWidth = getMainColumnWidth(layout)
  const numpadX = mainColWidth
  const fnH = fnRowHeight(layout)
  const bodyTop = fnH + rowGap
  const placed: SpectrumZonePlacement[] = []

  placed.push(...placeRow(layout.functionRow.left, 0, 0))
  placed.push(...placeRow(layout.functionRow.right, numpadX, 0))

  let mainY = bodyTop
  for (const row of layout.mainRows) {
    placed.push(...placeRow(row, 0, mainY))
    mainY += rowHeight(row) + rowGap
  }

  for (const zone of layout.numpad) {
    if (zone.code !== null) {
      placed.push({
        zone,
        x: numpadX + zone.col * numpadCell,
        y: bodyTop + zone.row * numpadCell
      })
    }
  }

  return placed
}

/** Content bounds (660px design width, height varies by layout — ISO/JIS enter key). */
export function getSpectrumLayoutBounds(layout: SpectrumKeyboardLayout): { width: number; height: number } {
  const zones = enumerateSpectrumZones(layout)
  let maxX = 0
  let maxY = 0
  for (const { zone, x, y } of zones) {
    maxX = Math.max(maxX, x + zone.w)
    maxY = Math.max(maxY, y + zone.h)
  }
  return {
    width: maxX,
    height: maxY
  }
}

/** All selectable key codes defined in a keyboard layout table. */
export function getSpectrumLayoutKeyCodes(layout: SpectrumKeyboardLayout): number[] {
  return enumerateSpectrumZones(layout)
    .map((entry) => entry.zone.code)
    .filter((code): code is number => code !== null)
}
