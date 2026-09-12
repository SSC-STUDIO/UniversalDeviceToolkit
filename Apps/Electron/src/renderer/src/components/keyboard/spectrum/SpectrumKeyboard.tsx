import { useEffect, useMemo, useRef, useState } from 'react'
import {
  KEYBOARD_STAGE_PADDING,
  SPECTRUM_KEYBOARD_LAYOUTS,
  enumerateSpectrumZones,
  getSpectrumLayoutBounds,
  type SpectrumKeyboardLayoutName,
  type SpectrumZone
} from './keyboardLayouts'
import { getKeyboardZoneCenters } from './deviceLayouts'
import { useBoxSelect } from './useBoxSelect'

export interface SpectrumKeyboardProps {
  layout: SpectrumKeyboardLayoutName
  deviceKeys: number[]
  selected: Set<number>
  onToggleKey?: (code: number) => void
  keyColors?: Map<number, string>
  onBoxSelect?: (codes: number[]) => void
  fixedScale?: number
  boxSelectable?: boolean
  clickSuppressRef?: React.MutableRefObject<boolean>
}

const ZOOM_MIN = 0.5
const ZOOM_MAX = 1.5

interface ZoneButtonProps {
  zone: SpectrumZone
  style?: React.CSSProperties
  deviceSet: Set<number>
  selected: Set<number>
  keyColors?: Map<number, string>
  dragRef: React.MutableRefObject<boolean>
  onToggleKey?: (code: number) => void
}

function SpectrumZoneButton({
  zone,
  style,
  deviceSet,
  selected,
  keyColors,
  dragRef,
  onToggleKey
}: ZoneButtonProps): React.JSX.Element {
  if (zone.code === null) {
    return (
      <span
        className="udt-spectrum-keyboard__zone udt-spectrum-keyboard__zone--spacer"
        style={style}
        aria-hidden="true"
      />
    )
  }

  const code = zone.code
  const available = deviceSet.has(code)
  const isSelected = selected.has(code)
  const color = keyColors?.get(code)
  const zoneStyle: React.CSSProperties = { ...style }
  if (color) zoneStyle.backgroundColor = color
  const className = [
    'udt-spectrum-keyboard__zone',
    !available && 'udt-spectrum-keyboard__zone--unavailable',
    isSelected && 'udt-spectrum-keyboard__zone--selected',
    color && 'udt-spectrum-keyboard__zone--colored'
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <button
      type="button"
      className={className}
      style={zoneStyle}
      aria-pressed={isSelected}
      aria-disabled={!available}
      title={available ? `0x${code.toString(16).toUpperCase()}` : undefined}
      onClick={() => {
        if (dragRef.current) return
        if (!available) return
        onToggleKey?.(code)
      }}
    />
  )
}

/**
 * Spectrum keyboard — WPF Viewbox parity: absolute zone layout from XAML tables,
 * uniform scale-to-fit, dynamic height for ISO/JIS tall Enter keys.
 */
export default function SpectrumKeyboard({
  layout,
  deviceKeys,
  selected,
  onToggleKey,
  keyColors,
  onBoxSelect,
  fixedScale,
  boxSelectable = true,
  clickSuppressRef
}: SpectrumKeyboardProps): React.JSX.Element {
  const hostRef = useRef<HTMLDivElement | null>(null)
  const [fitScale, setFitScale] = useState(1)

  const keyboardLayout = SPECTRUM_KEYBOARD_LAYOUTS[layout]
  const bounds = useMemo(() => getSpectrumLayoutBounds(keyboardLayout), [keyboardLayout])
  const stageWidth = bounds.width + KEYBOARD_STAGE_PADDING * 2
  const stageHeight = bounds.height + KEYBOARD_STAGE_PADDING * 2

  const placements = useMemo(
    () => enumerateSpectrumZones(keyboardLayout),
    [keyboardLayout]
  )

  useEffect(() => {
    if (fixedScale !== undefined) return
    const host = hostRef.current
    if (!host) return
    const update = (): void => {
      const width = host.clientWidth
      const height = host.clientHeight
      if (width <= 0) return
      const scaleW = width / stageWidth
      const scaleH = height > 0 ? height / stageHeight : scaleW
      setFitScale(Math.max(ZOOM_MIN, Math.min(scaleW, scaleH, ZOOM_MAX)))
    }
    update()
    const observer = new ResizeObserver(update)
    observer.observe(host)
    return () => observer.disconnect()
  }, [fixedScale, stageWidth, stageHeight])

  const scale = fixedScale ?? fitScale
  const deviceSet = useMemo(() => new Set(deviceKeys), [deviceKeys])

  const keyCenters = useMemo(() => {
    const present = new Set(deviceKeys)
    return getKeyboardZoneCenters(layout, scale).filter((center) => present.has(center.code))
  }, [layout, scale, deviceKeys])

  const { selection, didDragRef, onMouseDown } = useBoxSelect(
    hostRef,
    boxSelectable ? keyCenters : [],
    onBoxSelect,
    boxSelectable
  )
  const dragRef = clickSuppressRef ?? didDragRef

  const zoneProps = {
    deviceSet,
    selected,
    keyColors,
    dragRef,
    onToggleKey
  }

  return (
    <div className="udt-spectrum-keyboard" ref={hostRef} onMouseDown={onMouseDown}>
      <div
        className="udt-spectrum-keyboard__viewport"
        style={{ width: stageWidth * scale, height: stageHeight * scale }}
      >
        <div
          className="udt-spectrum-keyboard__stage"
          style={{
            width: stageWidth,
            height: stageHeight,
            transform: `scale(${scale})`
          }}
        >
          <div
            className="udt-spectrum-keyboard__canvas"
            style={{
              width: bounds.width,
              height: bounds.height,
              margin: KEYBOARD_STAGE_PADDING
            }}
          >
            {placements.map(({ zone, x, y }, index) => (
              <SpectrumZoneButton
                key={`${zone.code ?? 'spacer'}-${index}`}
                zone={zone}
                style={{
                  position: 'absolute',
                  left: x,
                  top: y,
                  width: zone.w,
                  height: zone.h
                }}
                {...zoneProps}
              />
            ))}
          </div>
        </div>
      </div>
      {boxSelectable && selection !== null && (
        <div
          className="udt-spectrum-keyboard__selection"
          style={{
            left: selection.left,
            top: selection.top,
            width: selection.width,
            height: selection.height
          }}
        />
      )}
    </div>
  )
}
