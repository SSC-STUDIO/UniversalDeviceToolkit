import { create } from 'zustand'
import { getHostCapabilities, type HostCapabilities } from '../api/hostCapabilities'
import { on } from '../api/bridge'

interface HostCapabilitiesState {
  capabilities: HostCapabilities | null
  loading: boolean
  error: Error | null
  load: () => Promise<void>
}

let latestLoadGeneration = 0
let storeSet: (patch: Partial<HostCapabilitiesState>) => void

export const useHostCapabilitiesStore = create<HostCapabilitiesState>()((set) => {
  storeSet = set
  return { capabilities: null, loading: false, error: null, load }
})

async function load(): Promise<void> {
  const generation = ++latestLoadGeneration
  storeSet?.({ loading: true })
  try {
    const capabilities = await getHostCapabilities()
    if (generation === latestLoadGeneration) {
      storeSet?.({ capabilities, loading: false, error: null })
    }
  } catch (error) {
    if (generation === latestLoadGeneration) {
      storeSet?.({
        capabilities: null,
        loading: false,
        error: error instanceof Error ? error : new Error(String(error))
      })
    }
  }
}

/** Refresh cached capabilities on startup and every time the Host becomes ready. */
export function initHostCapabilitiesSync(): () => void {
  void useHostCapabilitiesStore.getState().load()
  return on('host.ready', () => {
    void useHostCapabilitiesStore.getState().load()
  })
}
