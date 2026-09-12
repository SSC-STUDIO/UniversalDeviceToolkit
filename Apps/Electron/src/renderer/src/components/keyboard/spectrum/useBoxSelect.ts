import { useEffect, useRef, useState } from 'react'
import type { SpectrumZoneCenter } from './deviceLayouts'

/**
 * Drag-box selection — port of Electron SelectableControl (Controls/SelectableControl.cs)
 * with the SpectrumKeyboardBacklightControl.SelectableControl_Selected logic:
 * mouse-down starts a selection rectangle, mouse-up collects the key codes
 * whose center falls inside the rectangle. Moves under 4px are treated as
 * clicks (no selection).
 *
 * `keyCenters` are the selectable key centers in host pixels (already scaled),
 * matching `getBoundingClientRect` of the host; the overlay rectangle is
 * rendered by the caller in the same host-pixel space.
 */

export interface BoxRect {
  left: number
  top: number
  width: number
  height: number
}

export interface UseBoxSelectResult {
  /** Current selection rectangle in host px (null when not dragging). */
  selection: BoxRect | null
  /** Ref the caller can check in onClick handlers to suppress click vs drag. */
  didDragRef: React.MutableRefObject<boolean>
  onMouseDown: (e: React.MouseEvent) => void
}

/** Electron SelectableControl has no threshold; click/drag is told apart here. */
const CLICK_THRESHOLD_PX = 4

export function useBoxSelect(
  hostRef: React.RefObject<HTMLDivElement | null>,
  keyCenters: SpectrumZoneCenter[],
  onBoxSelect?: (codes: number[]) => void,
  enabled = true
): UseBoxSelectResult {
  const [selection, setSelection] = useState<BoxRect | null>(null)
  const [dragging, setDragging] = useState(false)
  const startRef = useRef<{ x: number; y: number } | null>(null)
  const didDragRef = useRef(false)
  const centersRef = useRef<SpectrumZoneCenter[]>([])
  const onSelectRef = useRef(onBoxSelect)
  onSelectRef.current = onBoxSelect

  useEffect(() => {
    centersRef.current = keyCenters
  }, [keyCenters])

  const hostPosition = (e: MouseEvent | React.MouseEvent): { x: number; y: number } | null => {
    const host = hostRef.current
    if (!host) return null
    const rect = host.getBoundingClientRect()
    return { x: e.clientX - rect.left, y: e.clientY - rect.top }
  }

  const onMouseDown = (e: React.MouseEvent): void => {
    if (!enabled) return
    if (e.button !== 0) return
    const pos = hostPosition(e)
    if (!pos) return
    startRef.current = pos
    didDragRef.current = false
    setDragging(true)
    setSelection({ left: pos.x, top: pos.y, width: 0, height: 0 })
  }

  useEffect(() => {
    if (!dragging) return

    const onMove = (e: MouseEvent): void => {
      const start = startRef.current
      const pos = hostPosition(e)
      if (!start || !pos) return
      if (Math.hypot(pos.x - start.x, pos.y - start.y) >= CLICK_THRESHOLD_PX) {
        didDragRef.current = true
      }
      if (!didDragRef.current) return
      setSelection({
        left: Math.min(start.x, pos.x),
        top: Math.min(start.y, pos.y),
        width: Math.abs(pos.x - start.x),
        height: Math.abs(pos.y - start.y)
      })
    }

    const onUp = (e: MouseEvent): void => {
      const start = startRef.current
      const pos = hostPosition(e)
      startRef.current = null
      setDragging(false)
      setSelection(null)
      if (!start || !pos) return
      if (Math.hypot(pos.x - start.x, pos.y - start.y) < CLICK_THRESHOLD_PX) return
      didDragRef.current = true

      const minX = Math.min(start.x, pos.x)
      const minY = Math.min(start.y, pos.y)
      const maxX = Math.max(start.x, pos.x)
      const maxY = Math.max(start.y, pos.y)
      const codes = centersRef.current
        .filter((center) => center.x >= minX && center.x <= maxX && center.y >= minY && center.y <= maxY)
        .map((center) => center.code)
      if (codes.length > 0) onSelectRef.current?.(codes)
    }

    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
    }
  }, [dragging, hostRef])

  return { selection, didDragRef, onMouseDown }
}
