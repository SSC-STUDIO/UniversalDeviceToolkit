import { create } from 'zustand'

/**
 * Port of Electron StatusWindow (tray status popup): power mode + God Mode preset,
 * CPU/memory/SSD sensor summaries, discrete GPU state and battery overview,
 * plus an update-available indicator. Opened via `tray:status` bridge event
 * (hover tooltip / explicit callers; not part of the original tray context menu).
 */

export interface StatusRequest {
  id: number
}

export let requestSeq = 0

export let pendingResolve: (() => void) | null = null

export interface StatusState {
  request: StatusRequest | null
  show: () => void
  settle: () => void
}

export const useStatusStore = create<StatusState>((set) => ({
  request: null,
  show: () => set({ request: { id: ++requestSeq } }),
  settle: () => {
    pendingResolve?.()
    pendingResolve = null
    set({ request: null })
  }
}))

export function openStatusModal(): Promise<void> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useStatusStore.getState().show()
  })
}
