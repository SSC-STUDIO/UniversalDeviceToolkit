import './colorPicker.css'
import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { Button, Input, Popover, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'

// Port of Electron Controls/ColorPickerControl: round color swatch button that opens a
// popup with a square HSV picker (circular hue ring + saturation/value square,
// PixiEditor ColorPicker SquarePicker), hex/RGB text fields and an OK button.
// onChangeContinuous maps to ColorChangedContinuous (drag), onChangeDelayed maps
// to ColorChangedDelayed (mouse up + 300ms debounced text edits).

export interface ColorPickerProps {
  value?: string
  onChangeContinuous?: (hex: string) => void
  onChangeDelayed?: (hex: string) => void
  size?: number
  children?: ReactNode
  tooltip?: string
  disabled?: boolean
}

const DEFAULT_COLOR = '#00ffff'
const PICKER_SIZE = 200
const SQUARE_SIZE = 112
const RING_OUTER_RADIUS = PICKER_SIZE / 2
const RING_INNER_RADIUS = PICKER_SIZE * (0.22 + 0.56)
const RING_CENTER_RADIUS = (RING_INNER_RADIUS + RING_OUTER_RADIUS) / 2
const HANDLE_SIZE = 14
const HEX_PATTERN = /^#[0-9a-f]{6}$/i
const DEBOUNCE_MS = 300

interface HsvColor {
  h: number
  s: number
  v: number
}

interface RgbColor {
  r: number
  g: number
  b: number
}

interface ChannelTexts {
  r: string
  g: string
  b: string
}

function normalizeHex(hex: string): string {
  return HEX_PATTERN.test(hex) ? hex : DEFAULT_COLOR
}

function hexToRgb(hex: string): RgbColor {
  const value = normalizeHex(hex).slice(1)
  return {
    r: parseInt(value.slice(0, 2), 16),
    g: parseInt(value.slice(2, 4), 16),
    b: parseInt(value.slice(4, 6), 16)
  }
}

function rgbToHex(r: number, g: number, b: number): string {
  const toHex = (v: number): string => Math.round(v).toString(16).padStart(2, '0')
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`
}

function rgbToHsv({ r, g, b }: RgbColor): HsvColor {
  const rn = r / 255
  const gn = g / 255
  const bn = b / 255
  const max = Math.max(rn, gn, bn)
  const min = Math.min(rn, gn, bn)
  const delta = max - min
  let h = 0
  if (delta !== 0) {
    if (max === rn) h = 60 * (((gn - bn) / delta) % 6)
    else if (max === gn) h = 60 * ((bn - rn) / delta + 2)
    else h = 60 * ((rn - gn) / delta + 4)
  }
  if (h < 0) h += 360
  return { h, s: max === 0 ? 0 : (delta / max) * 100, v: max * 100 }
}

function hsvToRgb({ h, s, v }: HsvColor): RgbColor {
  const hh = ((h % 360) + 360) % 360
  const c = (v / 100) * (s / 100)
  const x = c * (1 - Math.abs(((hh / 60) % 2) - 1))
  const m = v / 100 - c
  let r = 0
  let g = 0
  let b = 0
  if (hh < 60) {
    r = c
    g = x
  } else if (hh < 120) {
    r = x
    g = c
  } else if (hh < 180) {
    g = c
    b = x
  } else if (hh < 240) {
    g = x
    b = c
  } else if (hh < 300) {
    r = x
    b = c
  } else {
    r = c
    b = x
  }
  return { r: (r + m) * 255, g: (g + m) * 255, b: (b + m) * 255 }
}

function hsvToHex(hsv: HsvColor): string {
  const { r, g, b } = hsvToRgb(hsv)
  return rgbToHex(r, g, b)
}

function toByte(text: string): number {
  const parsed = parseInt(text, 10)
  if (Number.isNaN(parsed)) return 0
  return Math.min(255, Math.max(0, parsed))
}

export default function ColorPicker({
  value = DEFAULT_COLOR,
  onChangeContinuous,
  onChangeDelayed,
  size = 38,
  children,
  tooltip,
  disabled = false
}: ColorPickerProps): React.JSX.Element {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<HsvColor>(() => rgbToHsv(hexToRgb(value)))
  const [hexText, setHexText] = useState(() => normalizeHex(value))
  const [channels, setChannels] = useState<ChannelTexts>(() => {
    const { r, g, b } = hexToRgb(value)
    return { r: String(r), g: String(g), b: String(b) }
  })
  const [focused, setFocused] = useState({ hex: false, r: false, g: false, b: false })
  const debounceRef = useRef<number | null>(null)

  useEffect(() => {
    setDraft(rgbToHsv(hexToRgb(value)))
  }, [value])

  useEffect(() => {
    const rgb = hsvToRgb(draft)
    if (!focused.hex) setHexText(hsvToHex(draft))
    if (!focused.r) setChannels((prev) => ({ ...prev, r: String(Math.round(rgb.r)) }))
    if (!focused.g) setChannels((prev) => ({ ...prev, g: String(Math.round(rgb.g)) }))
    if (!focused.b) setChannels((prev) => ({ ...prev, b: String(Math.round(rgb.b)) }))
  }, [draft, focused])

  useEffect(
    () => () => {
      if (debounceRef.current != null) window.clearTimeout(debounceRef.current)
    },
    []
  )

  const schedule = (fn: () => void): void => {
    if (debounceRef.current != null) window.clearTimeout(debounceRef.current)
    debounceRef.current = window.setTimeout(() => {
      debounceRef.current = null
      fn()
    }, DEBOUNCE_MS)
  }

  const applyHex = (hex: string): void => {
    setDraft(rgbToHsv(hexToRgb(hex)))
    onChangeDelayed?.(hex)
  }

  const applyRgb = (next: ChannelTexts): void => {
    const r = toByte(next.r)
    const g = toByte(next.g)
    const b = toByte(next.b)
    const hex = rgbToHex(r, g, b)
    setDraft(rgbToHsv({ r, g, b }))
    setHexText(hex)
    onChangeDelayed?.(hex)
  }

  const emitContinuous = (hsv: HsvColor): void => {
    onChangeContinuous?.(hsvToHex(hsv))
  }

  const emitDelayed = (): void => {
    onChangeDelayed?.(hsvToHex(draft))
  }

  const updateSquare = (e: React.PointerEvent<HTMLDivElement>): void => {
    const rect = e.currentTarget.getBoundingClientRect()
    const x = Math.min(1, Math.max(0, (e.clientX - rect.left) / rect.width))
    const y = Math.min(1, Math.max(0, (e.clientY - rect.top) / rect.height))
    const next: HsvColor = { h: draft.h, s: x * 100, v: (1 - y) * 100 }
    setDraft(next)
    emitContinuous(next)
  }

  const handleSquarePointerDown = (e: React.PointerEvent<HTMLDivElement>): void => {
    e.preventDefault()
    e.currentTarget.setPointerCapture(e.pointerId)
    updateSquare(e)
  }

  const handleSquarePointerMove = (e: React.PointerEvent<HTMLDivElement>): void => {
    if (e.currentTarget.hasPointerCapture(e.pointerId)) updateSquare(e)
  }

  const handleSquarePointerUp = (e: React.PointerEvent<HTMLDivElement>): void => {
    if (!e.currentTarget.hasPointerCapture(e.pointerId)) return
    e.currentTarget.releasePointerCapture(e.pointerId)
    emitDelayed()
  }

  const updateRing = (e: React.PointerEvent<HTMLDivElement>): void => {
    const rect = e.currentTarget.getBoundingClientRect()
    const cx = rect.left + rect.width / 2
    const cy = rect.top + rect.height / 2
    const degrees = (Math.atan2(e.clientY - cy, e.clientX - cx) * 180) / Math.PI + 90
    const next: HsvColor = { h: ((degrees % 360) + 360) % 360, s: draft.s, v: draft.v }
    setDraft(next)
    emitContinuous(next)
  }

  const handleRingPointerDown = (e: React.PointerEvent<HTMLDivElement>): void => {
    e.preventDefault()
    e.currentTarget.setPointerCapture(e.pointerId)
    updateRing(e)
  }

  const handleRingPointerMove = (e: React.PointerEvent<HTMLDivElement>): void => {
    if (e.currentTarget.hasPointerCapture(e.pointerId)) updateRing(e)
  }

  const handleRingPointerUp = (e: React.PointerEvent<HTMLDivElement>): void => {
    if (!e.currentTarget.hasPointerCapture(e.pointerId)) return
    e.currentTarget.releasePointerCapture(e.pointerId)
    emitDelayed()
  }

  const ringAngle = ((draft.h - 90) * Math.PI) / 180
  const ringCx = RING_OUTER_RADIUS + RING_CENTER_RADIUS * Math.cos(ringAngle)
  const ringCy = RING_OUTER_RADIUS + RING_CENTER_RADIUS * Math.sin(ringAngle)
  const squareSx = (draft.s / 100) * SQUARE_SIZE
  const squareSy = (1 - draft.v / 100) * SQUARE_SIZE

  const handleHexChange = (text: string): void => {
    setHexText(text)
    if (HEX_PATTERN.test(text)) schedule(() => applyHex(text))
  }

  const handleChannelChange = (channel: 'r' | 'g' | 'b', text: string): void => {
    const next = { ...channels, [channel]: text }
    setChannels(next)
    schedule(() => applyRgb(next))
  }

  const panel = (
    <div className="udt-color-picker__panel">
      <div className="udt-color-picker__sv" style={{ width: PICKER_SIZE, height: PICKER_SIZE }}>
        <div
          className="udt-color-picker__ring"
          onPointerDown={handleRingPointerDown}
          onPointerMove={handleRingPointerMove}
          onPointerUp={handleRingPointerUp}
          onPointerCancel={handleRingPointerUp}
        />
        <div
          className="udt-color-picker__ring-handle"
          style={{ left: ringCx - HANDLE_SIZE / 2, top: ringCy - HANDLE_SIZE / 2 }}
        />
        <div
          className="udt-color-picker__square"
          style={{
            width: SQUARE_SIZE,
            height: SQUARE_SIZE,
            backgroundImage: `linear-gradient(to top, rgba(0,0,0,1), rgba(0,0,0,0)), linear-gradient(to right, #ffffff, hsl(${draft.h}, 100%, 50%))`
          }}
          onPointerDown={handleSquarePointerDown}
          onPointerMove={handleSquarePointerMove}
          onPointerUp={handleSquarePointerUp}
          onPointerCancel={handleSquarePointerUp}
        />
        <div
          className="udt-color-picker__square-handle"
          style={{ left: squareSx - HANDLE_SIZE / 2, top: squareSy - HANDLE_SIZE / 2 }}
        />
      </div>

      <div className="udt-color-picker__fields">
        <label className="udt-color-picker__field">
          <span>{t('colorPicker.hex')}</span>
          <Input
            className="udt-color-picker__hex"
            value={hexText}
            spellCheck={false}
            onFocus={() => setFocused((prev) => ({ ...prev, hex: true }))}
            onBlur={() => setFocused((prev) => ({ ...prev, hex: false }))}
            onChange={(e) => handleHexChange(e.target.value)}
          />
        </label>
        <label className="udt-color-picker__field">
          <span>{t('colorPicker.red')}</span>
          <Input
            className="udt-color-picker__channel"
            inputMode="numeric"
            value={channels.r}
            onFocus={() => setFocused((prev) => ({ ...prev, r: true }))}
            onBlur={() => setFocused((prev) => ({ ...prev, r: false }))}
            onChange={(e) => handleChannelChange('r', e.target.value)}
          />
        </label>
        <label className="udt-color-picker__field">
          <span>{t('colorPicker.green')}</span>
          <Input
            className="udt-color-picker__channel"
            inputMode="numeric"
            value={channels.g}
            onFocus={() => setFocused((prev) => ({ ...prev, g: true }))}
            onBlur={() => setFocused((prev) => ({ ...prev, g: false }))}
            onChange={(e) => handleChannelChange('g', e.target.value)}
          />
        </label>
        <label className="udt-color-picker__field">
          <span>{t('colorPicker.blue')}</span>
          <Input
            className="udt-color-picker__channel"
            inputMode="numeric"
            value={channels.b}
            onFocus={() => setFocused((prev) => ({ ...prev, b: true }))}
            onBlur={() => setFocused((prev) => ({ ...prev, b: false }))}
            onChange={(e) => handleChannelChange('b', e.target.value)}
          />
        </label>
      </div>

      <Button type="primary" className="udt-color-picker__ok" onClick={() => setOpen(false)}>
        {t('colorPicker.ok')}
      </Button>
    </div>
  )

  return (
    <Popover
      open={open}
      onOpenChange={(next) => setOpen(next && !disabled)}
      trigger="click"
      placement="bottom"
      arrow={false}
      content={panel}
      classNames={{ root: 'udt-color-picker__popover' }}
    >
      <Tooltip title={tooltip}>
        <Button
          className="udt-color-picker__button"
          style={{ width: size, height: size, background: open ? hsvToHex(draft) : value }}
          disabled={disabled}
        >
          {children}
        </Button>
      </Tooltip>
    </Popover>
  )
}
