import { create } from 'zustand'

/**
 * Loading session coordinator — port of Electron Controls/Loading
 * (LoadSession / LoadState / LoadStateCoordinator).
 *
 * Multiple sessions may run concurrently (e.g. page load + background refresh);
 * the coordinator merges them and presents the most recently started active
 * session as the global loading state.
 */

export interface LoadingSession {
  id: string
  /** Human-readable operation label shown as the overlay message. */
  label: string
  /** Optional progress detail (e.g. "Reading file 12/50"). */
  message?: string
  /** Progress 0..100; undefined/null means indeterminate. */
  progress: number | null
  /** Whether the UI shows a cancel action for this session. */
  canCancel: boolean
  /** Page-owned loading chrome: tracked but never shown in the global overlay. */
  silent: boolean
  cancel?: () => void
  /** Set when the session ended with an error (message carries the error text). */
  failed: boolean
  startedAt: number
}

export interface LoadingStore {
  sessions: LoadingSession[]
  /** Active session presented by the overlay; null when nothing is loading. */
  active: LoadingSession | null
  /**
   * Start a loading session. `silent` sessions (pages that own their loading
   * chrome — Electron LoadingChromeOwnership.Page) track progress but never surface
   * the global overlay; they keep the page's own skeleton visible instead.
   */
  start: (
    label: string,
    options?: { canCancel?: boolean; cancel?: () => void; silent?: boolean }
  ) => string
  /** Update progress/message of a session (no-op when the session no longer exists). */
  report: (id: string, message?: string, progress?: number | null) => void
  /** Successfully finish a session. */
  finish: (id: string) => void
  /** Finish a session with an error; the overlay keeps showing the message. */
  fail: (id: string, message: string) => void
  /** Request cancellation of a session (invokes its cancel handler, then removes it). */
  cancel: (id: string) => void
  clear: () => void
}

function pickActive(sessions: LoadingSession[]): LoadingSession | null {
  // Silent sessions (pages owning their loading chrome) never surface the
  // global overlay; only non-silent sessions drive it.
  const overlaySessions = sessions.filter((s) => !s.silent)
  const activeSessions = overlaySessions.filter((s) => !s.failed)
  if (activeSessions.length === 0) {
    const failed = overlaySessions.filter((s) => s.failed)
    return failed.length > 0 ? failed[failed.length - 1] : null
  }
  return activeSessions[activeSessions.length - 1]
}

export const useLoadingStore = create<LoadingStore>()((set, get) => ({
  sessions: [],
  active: null,

  start(label, options) {
    const session: LoadingSession = {
      id: crypto.randomUUID(),
      label,
      progress: null,
      canCancel: options?.canCancel ?? false,
      cancel: options?.cancel,
      silent: options?.silent ?? false,
      failed: false,
      startedAt: Date.now()
    }
    set((state) => {
      const sessions = [...state.sessions, session]
      return { sessions, active: pickActive(sessions) }
    })
    return session.id
  },

  report(id, message, progress) {
    set((state) => {
      if (!state.sessions.some((s) => s.id === id)) return state
      const sessions = state.sessions.map((s) =>
        s.id === id
          ? { ...s, message: message ?? s.message, progress: progress !== undefined ? progress : s.progress }
          : s
      )
      return { sessions, active: pickActive(sessions) }
    })
  },

  finish(id) {
    set((state) => {
      const sessions = state.sessions.filter((s) => s.id !== id)
      return { sessions, active: pickActive(sessions) }
    })
  },

  fail(id, message) {
    set((state) => {
      if (!state.sessions.some((s) => s.id === id)) return state
      const sessions = state.sessions.map((s) => (s.id === id ? { ...s, message, failed: true } : s))
      return { sessions, active: pickActive(sessions) }
    })
  },

  cancel(id) {
    const session = get().sessions.find((s) => s.id === id)
    session?.cancel?.()
    get().finish(id)
  },

  clear() {
    set({ sessions: [], active: null })
  }
}))
