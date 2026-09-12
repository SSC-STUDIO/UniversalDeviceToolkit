import { create } from 'zustand'

/**
 * Port of Electron UnsupportedWindow: shown at startup for machines the app has not
 * been tested on. The Continue button is gated by a 5-second countdown; Exit
 * quits the whole application (app:quit bridge).
 */

export interface UnsupportedDeviceOptions {
  vendor?: string | null
  model?: string | null
  machineType?: string | null
}

export interface UnsupportedDeviceRequest {
  id: number
  options: UnsupportedDeviceOptions
}

export let requestSeq = 0

export let pendingResolve: ((shouldContinue: boolean) => void) | null = null

export interface UnsupportedDeviceState {
  request: UnsupportedDeviceRequest | null
  show: (options: UnsupportedDeviceOptions) => void
  settle: (shouldContinue: boolean) => void
}

export const useUnsupportedDeviceStore = create<UnsupportedDeviceState>((set) => ({
  request: null,
  show: (options) => set({ request: { id: ++requestSeq, options } }),
  settle: (shouldContinue) => {
    pendingResolve?.(shouldContinue)
    pendingResolve = null
    set({ request: null })
  }
}))

export function openUnsupportedDevice(options: UnsupportedDeviceOptions): Promise<boolean> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useUnsupportedDeviceStore.getState().show(options)
  })
}
