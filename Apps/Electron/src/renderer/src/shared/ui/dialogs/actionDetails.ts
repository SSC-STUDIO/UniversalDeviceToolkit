import { create } from 'zustand'

/**
 * Port of Electron ActionDetailsWindow: shows the action title/description plus the
 * technical implementation details (commands, registry tweaks or service
 * management entries) for a Windows optimization action key.
 *
 * The details mapping mirrors ActionDetailsWindow.GetActionImplementationDetails
 * and its helper methods — keyed by the host action key, with the resource
 * strings resolved from the `wpf.*` i18n block.
 */

export interface ActionDetailsOptions {
  actionKey: string
  title: string
  description?: string
}

export interface ActionDetailsRequest {
  id: number
  options: ActionDetailsOptions
}

let requestSeq = 0

let pendingResolve: (() => void) | null = null

export interface ActionDetailsState {
  request: ActionDetailsRequest | null
  show: (options: ActionDetailsOptions) => void
  settle: () => void
}

export const useActionDetailsStore = create<ActionDetailsState>((set) => ({
  request: null,
  show: (options) => set({ request: { id: ++requestSeq, options } }),
  settle: () => {
    pendingResolve?.()
    pendingResolve = null
    set({ request: null })
  }
}))

export function openActionDetails(options: ActionDetailsOptions): Promise<void> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useActionDetailsStore.getState().show(options)
  })
}
