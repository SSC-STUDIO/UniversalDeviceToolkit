import { useEffect, useMemo, useRef, useState } from 'react'
import type { SpectrumKeyboardLayoutName } from './keyboardLayouts'
import {
  getKeyboardZoneCenters,
  SPECTRUM_DEVICE_LAYOUTS,
  type SpectrumDevicePanelLayout
} from './deviceLayouts'
import { useBoxSelect } from './useBoxSelect'
import SpectrumKeyboard from './SpectrumKeyboard'

export interface SpectrumDevicePanelProps {
  /** Device visualization layout (keyboard + front panel arrangement). */
  layout: SpectrumDevicePanelLayout
  keyboardLayout: SpectrumKeyboardLayoutName
  /** Key codes present on this device; others render dimmed and non-interactive. */
  deviceKeys: number[]
  /** Currently selected key codes (per effect). */
  selected: Set<number>
  onToggleKey?: (code: number) => void
  /** Optional per-key paint color (effect preview / animation). */
  keyColors?: Map<number, string>
  /** Called with every selected key code when a drag-box selection ends. */
  onBoxSelect?: (codes: number[]) => void
}

const ZOOM_MIN = 0.5
const ZOOM_MAX = 1.5

/**
 * Spectrum device visualization — port of Electron SpectrumDeviceControl: the
 * keyboard (reused SpectrumKeyboard at zoom 1) plus the clickable front-panel
 * zones from the device layout XAMLs, all on one zoomable canvas. The whole
 * canvas supports drag-box selection like the Electron SelectableControl wrapper.
 */
export default function SpectrumDevicePanel({
  layout,
  keyboardLayout,
  deviceKeys,
  selected,
  onToggleKey,
  keyColors,
  onBoxSelect
}: SpectrumDevicePanelProps): React.JSX.Element {
  const hostRef = useRef<HTMLDivElement | null>(null)
  const [fitScale, setFitScale] = useState(1)
  const deviceLayout = SPECTRUM_DEVICE_LAYOUTS[layout]

  useEffect(() => {
    const host = hostRef.current
    if (!host) return
    const update = (): void => {
      const width = host.clientWidth
      if (width > 0) setFitScale(Math.max(ZOOM_MIN, Math.min(width / deviceLayout.width, ZOOM_MAX)))
    }
    update()
    const observer = new ResizeObserver(update)
    observer.observe(host)
    return () => observer.disconnect()
  }, [deviceLayout.width])

  const scale = fitScale
  const deviceSet = useMemo(() => new Set(deviceKeys), [deviceKeys])

  const keyCenters = useMemo(() => {
    const present = new Set(deviceKeys)
    const kb = deviceLayout.keyboard
    const centers = getKeyboardZoneCenters(keyboardLayout, scale, kb.x, kb.y)
    deviceLayout.zones.forEach((zone) => {
      if (present.has(zone.code)) {
        centers.push({
          code: zone.code,
          x: (zone.x + zone.w / 2) * scale,
          y: (zone.y + zone.h / 2) * scale
        })
      }
    })
    return centers
  }, [deviceLayout, keyboardLayout, scale, deviceKeys])

  const { selection, didDragRef, onMouseDown } = useBoxSelect(hostRef, keyCenters, onBoxSelect)

  return (
    <div className="udt-spectrum-device" ref={hostRef} onMouseDown={onMouseDown}>
      {/* CSS zoom keeps the scaled size in layout, like the Electron ScaleTransform. */}
      <div
        className="udt-spectrum-device__stage"
        style={{ zoom: scale, width: deviceLayout.width, height: deviceLayout.height }}
      >
        <div
          className="udt-spectrum-device__keyboard"
          style={{
            left: deviceLayout.keyboard.x,
            top: deviceLayout.keyboard.y,
            width: deviceLayout.keyboard.w,
            height: deviceLayout.keyboard.h
          }}
        >
          <SpectrumKeyboard
            layout={keyboardLayout}
            deviceKeys={deviceKeys}
            selected={selected}
            onToggleKey={onToggleKey}
            keyColors={keyColors}
            fixedScale={1}
            boxSelectable={false}
            clickSuppressRef={didDragRef}
          />
        </div>
        {deviceLayout.zones.map((zone) => {
          const available = deviceSet.has(zone.code)
          const isSelected = selected.has(zone.code)
          const color = keyColors?.get(zone.code)
          const style: React.CSSProperties = {
            left: zone.x,
            top: zone.y,
            width: zone.w,
            height: zone.h
          }
          if (color) style.backgroundColor = color
          const zoneStyle = [
            'udt-spectrum-device__zone',
            !available && 'udt-spectrum-device__zone--unavailable',
            isSelected && 'udt-spectrum-device__zone--selected',
            color && 'udt-spectrum-device__zone--colored'
          ]
            .filter(Boolean)
            .join(' ')
          return (
            <button
              key={zone.code}
              type="button"
              className={zoneStyle}
              style={style}
              aria-pressed={isSelected}
              aria-disabled={!available}
              title={available ? `0x${zone.code.toString(16).toUpperCase()}` : undefined}
              onClick={() => {
                if (didDragRef.current) return
                if (!available) return
                onToggleKey?.(zone.code)
              }}
            />
          )
        })}
      </div>
      {selection !== null && (
        <div
          className="udt-spectrum-device__selection"
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
