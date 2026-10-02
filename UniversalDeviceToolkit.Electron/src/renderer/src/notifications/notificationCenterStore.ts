import { create } from 'zustand'

/**
 * Notification center store — port of Electron Controls/Shell/AppNotificationHost
 * + AppNotificationItemViewModel: right-corner stacked toasts with per-item
 * auto-close timers (hover pause/resume), same-key merging with a ×N badge,
 * optional progress bar and a 30-item hard cap.
 */

/** Wire shape of the host `notifications.changed` event. */
export interface AppNotificationRequest {
  title: string
  message?: string
  severity: 'Success' | 'Info' | 'Warning' | 'Error'
  isPersistent?: boolean
  progressPercent?: number
}

export type NotificationSeverity = 'Success' | 'Info' | 'Warning' | 'Error'

export interface NotificationItem {
  id: string
  severity: NotificationSeverity
  title: string
  message?: string
  progressPercent?: number
  mergeCount: number
  isPersistent: boolean
  /** Absolute auto-close deadline (ms epoch); Infinity = never auto-close. */
  deadline: number
  /** Remaining ms captured while hover-paused. */
  remainingMs: number
  timer: number | undefined
  createdAt: number
}

export interface NotificationSettings {
  duration: 'Short' | 'Normal' | 'Long'
  /** Electron AutoClose durations (seconds); Success uses the shorter ladder. */
}

export const MAX_NOTIFICATIONS = 30

/** Electron AppNotificationHost.SuccessAutoClose / duration ladder. */
export function resolveAutoCloseMs(request: AppNotificationRequest, settings: NotificationSettings): number {
  if (request.isPersistent) return Infinity
  if (typeof request.progressPercent === 'number' && request.progressPercent < 100) return Infinity
  if (typeof request.progressPercent === 'number' && request.progressPercent >= 100) return 2000
  const isSuccess = request.severity === 'Success'
  switch (settings.duration) {
    case 'Short':
      return 3000
    case 'Long':
      return (isSuccess ? 8 : 10) * 1000
    default:
      return (isSuccess ? 5 : 5) * 1000
  }
}

function nextId(): string {
  return `udt-notification-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

interface NotificationCenterState {
  items: NotificationItem[]
  push: (request: AppNotificationRequest, settings: NotificationSettings) => void
  /** Hover pause: stop the auto-close timer, remember the remaining time. */
  pause: (id: string) => void
  /** Hover resume: restart the auto-close timer with the remaining time. */
  resume: (id: string) => void
  /** Close button: remove immediately. */
  dismiss: (id: string) => void
  remove: (id: string) => void
  clear: () => void
}

function mergeKey(request: AppNotificationRequest): string {
  return `${request.severity}\u001f${request.title}`
}

function dropOverflow(items: NotificationItem[]): void {
  while (items.length > MAX_NOTIFICATIONS) {
    const dropped = items.shift()
    if (dropped?.timer !== undefined) window.clearTimeout(dropped.timer)
  }
}

function scheduleAutoClose(items: NotificationItem[], id: string): NotificationItem[] {
  return items.map((item) => {
    if (item.id !== id) return item
    if (item.timer !== undefined) window.clearTimeout(item.timer)
    if (item.deadline === Infinity) return { ...item, timer: undefined, remainingMs: 0 }
    const remaining = Math.max(0, item.deadline - Date.now())
    const timer = window.setTimeout(() => {
      useNotificationCenter.getState().remove(id)
    }, remaining)
    return { ...item, timer, remainingMs: remaining }
  })
}

export const useNotificationCenter = create<NotificationCenterState>((set, get) => ({
  items: [],

  push(request, settings) {
    const key = mergeKey(request)
    const existing = [...get().items]
      .reverse()
      .find((item) => mergeKey({ title: item.title, severity: item.severity } as AppNotificationRequest) === key)

    if (existing !== undefined && existing.progressPercent === undefined) {
      // Same-key merge: bump the count and reset the auto-close deadline
      // (Electron ApplyMerge + ResetAutoCloseTimer).
      const next = {
        ...existing,
        title: request.title || existing.title,
        message: request.message || existing.message,
        mergeCount: Math.max(existing.mergeCount + 1, 2),
        deadline: resolveAutoCloseMs(request, settings)
      }
      const items = scheduleAutoClose(
        get().items.map((item) => (item.id === existing.id ? next : item)),
        existing.id
      )
      set({ items })
      return
    }

    const id = nextId()
    const item: NotificationItem = {
      id,
      severity: request.severity,
      title: request.title,
      message: request.message,
      progressPercent: request.progressPercent,
      mergeCount: 1,
      isPersistent: request.isPersistent === true,
      deadline: resolveAutoCloseMs(request, settings),
      remainingMs: 0,
      timer: undefined,
      createdAt: Date.now()
    }
    const items = [...get().items, item]
    // Electron TrimIfNeeded: hard cap on live toasts.
    dropOverflow(items)
    set({ items: scheduleAutoClose(items, id) })
  },

  pause(id) {
    set((state) => ({
      items: state.items.map((item) => {
        if (item.id !== id || item.timer === undefined) return item
        const remaining = Math.max(0, item.deadline - Date.now())
        window.clearTimeout(item.timer)
        return { ...item, timer: undefined, remainingMs: remaining }
      })
    }))
  },

  resume(id) {
    set((state) => {
      const item = state.items.find((candidate) => candidate.id === id)
      if (item === undefined || item.deadline === Infinity) return state
      const deadline = item.timer === undefined && item.remainingMs > 0 ? Date.now() + item.remainingMs : item.deadline
      const next = { ...item, deadline, remainingMs: 0 }
      const items = get().items.map((candidate) => (candidate.id === id ? next : candidate))
      return { items: scheduleAutoClose(items, id) }
    })
  },

  dismiss(id) {
    const item = get().items.find((candidate) => candidate.id === id)
    if (item !== undefined && item.timer !== undefined) window.clearTimeout(item.timer)
    set((state) => ({ items: state.items.filter((candidate) => candidate.id !== id) }))
  },

  remove(id) {
    get().dismiss(id)
  },

  clear() {
    for (const item of get().items) {
      if (item.timer !== undefined) window.clearTimeout(item.timer)
    }
    set({ items: [] })
  }
}))
