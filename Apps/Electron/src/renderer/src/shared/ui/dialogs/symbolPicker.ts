import { create } from 'zustand'

/**
 * Port of Electron SymbolRegularPicker: an icon picker with a search filter
 * (debounced like the Electron DebounceDispatcher) over a grid of symbol buttons.
 * Returns the selected icon name, or null for "Default".
 */

export interface SymbolPickerRequest {
  id: number
}

let requestSeq = 0

let pendingResolve: ((icon: string | null) => void) | null = null

export interface SymbolPickerState {
  request: SymbolPickerRequest | null
  show: () => void
  settle: (icon: string | null) => void
}

export const useSymbolPickerStore = create<SymbolPickerState>((set) => ({
  request: null,
  show: () => set({ request: { id: ++requestSeq } }),
  settle: (icon) => {
    pendingResolve?.(icon)
    pendingResolve = null
    set({ request: null })
  }
}))

export function openSymbolPicker(): Promise<string | null> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useSymbolPickerStore.getState().show()
  })
}
