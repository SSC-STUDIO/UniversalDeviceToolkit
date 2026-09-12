import { create } from 'zustand'

/**
 * Port of Electron UpdateWindow: shows the newest version and its release notes
 * and offers to download/install it.
 *
 * The main process downloads the Electron installer from the same public
 * application release the Host selected (stable or prerelease) and launches
 * the recorded verified path with NSIS `/S`, quitting the app.
 */

export interface UpdateModalOptions {
  version?: string | null
  releaseNotes?: string | null
  releaseDate?: string | null
}

export interface UpdateRequest {
  id: number
  options: UpdateModalOptions
}

export let requestSeq = 0

export let pendingResolve: ((downloaded: boolean) => void) | null = null

export interface UpdateState {
  request: UpdateRequest | null
  show: (options: UpdateModalOptions) => void
  settle: (downloaded: boolean) => void
}

export const useUpdateStore = create<UpdateState>((set) => ({
  request: null,
  show: (options) => set({ request: { id: ++requestSeq, options } }),
  settle: (downloaded) => {
    pendingResolve?.(downloaded)
    pendingResolve = null
    set({ request: null })
  }
}))

export function openUpdateModal(options: UpdateModalOptions): Promise<boolean> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useUpdateStore.getState().show(options)
  })
}
