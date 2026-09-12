/**
 * Tracks which UI surfaces are visible so the main process can pause Host
 * polling and apply background QoS when the app is tray-only.
 *
 * Auxiliary windows expire after five minutes of idle time. The main window
 * stays hidden in the tray so restoring it preserves the current page and drafts.
 */

export type UiSurface = 'main' | 'osd' | 'trayPopup' | 'status'

const IDLE_DESTROY_MS = 5 * 60_000

const visible = new Set<UiSurface>()
const idleTimers = new Map<string, ReturnType<typeof setTimeout>>()

let lastActive: boolean | null = null
let onChange: ((active: boolean) => void) | null = null

export function setUiActivityHandler(handler: (active: boolean) => void): void {
  onChange = handler
}

export function isUiActive(): boolean {
  return visible.size > 0
}

export function setSurfaceVisible(surface: UiSurface, isVisible: boolean): void {
  if (isVisible) {
    visible.add(surface)
  } else {
    visible.delete(surface)
  }
  const active = visible.size > 0
  if (active === lastActive) return
  lastActive = active
  onChange?.(active)
}

export function scheduleIdleDestroy(key: string, destroy: () => void, delayMs = IDLE_DESTROY_MS): void {
  cancelIdleDestroy(key)
  idleTimers.set(
    key,
    setTimeout(() => {
      idleTimers.delete(key)
      destroy()
    }, delayMs)
  )
}

export function cancelIdleDestroy(key: string): void {
  const timer = idleTimers.get(key)
  if (timer == null) return
  clearTimeout(timer)
  idleTimers.delete(key)
}

export function cancelAllIdleDestroys(): void {
  for (const timer of idleTimers.values()) {
    clearTimeout(timer)
  }
  idleTimers.clear()
}
