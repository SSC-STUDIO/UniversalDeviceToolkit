import { create } from 'zustand'
import {
  optimizationApi,
  type NetworkAccelerationConfig,
  type NetworkAccelerationStatus,
  type NetworkRuntimeSnapshot,
  type NetworkTrafficSnapshot,
  type OptimizationCategoryDefinition
} from '../api/optimization'

export interface OptimizationStoreState {
  categories: OptimizationCategoryDefinition[]
  networkStatus: NetworkAccelerationStatus | null
  trafficSnapshot: NetworkTrafficSnapshot | null
  runtimeSnapshot: NetworkRuntimeSnapshot | null
  loading: boolean
  error: string | null
}

export interface OptimizationStoreActions {
  load: () => Promise<void>
  refresh: () => Promise<void>
  apply: (keys: string[]) => Promise<boolean>
  revert: (keys: string[]) => Promise<boolean>
  applyRecommended: () => Promise<boolean>
  estimate: (keys: string[]) => Promise<number>
  runCleanup: (keys: string[]) => Promise<boolean>
  loadNetwork: () => Promise<void>
  saveNetworkConfig: (config: NetworkAccelerationConfig) => Promise<boolean>
  startNetwork: () => Promise<boolean>
  stopNetwork: () => Promise<boolean>
  loadTraffic: () => Promise<void>
  loadRuntime: () => Promise<void>
  restoreNetwork: () => Promise<boolean>
  setNetworkGroupEnabled: (groupId: string, enabled: boolean) => Promise<boolean>
  setNetworkSubItemEnabled: (groupId: string, subItemId: string, enabled: boolean) => Promise<boolean>
}

export type OptimizationStore = OptimizationStoreState & OptimizationStoreActions

export const useOptimizationStore = create<OptimizationStore>((set, get) => {
  let categoryRequestId = 0

  const loadCategories = async (force: boolean): Promise<void> => {
    if (!force && get().loading) return
    const requestId = ++categoryRequestId
    set({ loading: true, error: null })
    try {
      const { categories } = await optimizationApi.getCategories()
      if (requestId === categoryRequestId) set({ categories })
    } catch (error) {
      if (requestId === categoryRequestId) set({ error: (error as Error).message })
    } finally {
      if (requestId === categoryRequestId) set({ loading: false })
    }
  }

  return {
    categories: [],
    networkStatus: null,
    trafficSnapshot: null,
    runtimeSnapshot: null,
    loading: false,
    error: null,

    load: () => loadCategories(false),
    refresh: () => loadCategories(true),

  async apply(keys) {
    if (keys.length === 0) return true
    try {
      const res = await optimizationApi.apply(keys)
      if (!res.applied) return false
      await get().load()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async revert(keys) {
    if (keys.length === 0) return true
    try {
      const res = await optimizationApi.revert(keys)
      if (!res.reverted) return false
      await get().load()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async applyRecommended() {
    try {
      const res = await optimizationApi.applyRecommended()
      if (!res.applied) return false
      await get().load()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async estimate(keys) {
    if (keys.length === 0) return 0
    try {
      const res = await optimizationApi.estimateCleanup(keys)
      return res.bytes
    } catch (error) {
      set({ error: (error as Error).message })
      return 0
    }
  },

  async runCleanup(keys) {
    if (keys.length === 0) return true
    try {
      const res = await optimizationApi.runCleanup(keys)
      if (!res.done) return false
      await get().load()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async loadNetwork() {
    try {
      const status = await optimizationApi.networkGetStatus()
      set({ networkStatus: status })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  async saveNetworkConfig(config) {
    try {
      const res = await optimizationApi.networkSaveConfig(config)
      if (!res.saved) return false
      await get().loadNetwork()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async startNetwork() {
    try {
      const res = await optimizationApi.networkStart()
      if (!res.ok) return false
      await get().loadNetwork()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async stopNetwork() {
    try {
      const res = await optimizationApi.networkStop()
      if (!res.ok) return false
      set({ trafficSnapshot: null, runtimeSnapshot: null })
      await get().loadNetwork()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async loadTraffic() {
    try {
      const snapshot = await optimizationApi.networkGetTrafficSnapshot()
      set({ trafficSnapshot: snapshot })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  async loadRuntime() {
    try {
      const snapshot = await optimizationApi.networkGetRuntimeSnapshot()
      set({ runtimeSnapshot: snapshot })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  async restoreNetwork() {
    try {
      const res = await optimizationApi.networkRestore()
      set({ trafficSnapshot: null, runtimeSnapshot: null })
      await get().loadNetwork()
      return res.ok
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async setNetworkGroupEnabled(groupId, enabled) {
    const status = get().networkStatus
    if (!status) return false
    const config: NetworkAccelerationConfig = {
      ...status.config,
      domainGroups: status.config.domainGroups.map((group) => {
        if (!group.id || group.id.toLowerCase() !== groupId.toLowerCase()) return group
        return {
          ...group,
          enabled,
          subItems: group.subItems.map((sub) => ({ ...sub, enabled }))
        }
      })
    }
    return get().saveNetworkConfig(config)
  },

  async setNetworkSubItemEnabled(groupId, subItemId, enabled) {
    const status = get().networkStatus
    if (!status) return false
    const config: NetworkAccelerationConfig = {
      ...status.config,
      domainGroups: status.config.domainGroups.map((group) => {
        if (!group.id || group.id.toLowerCase() !== groupId.toLowerCase()) return group
        const subItems = group.subItems.map((sub) =>
          sub.id === subItemId ? { ...sub, enabled } : sub
        )
        let groupEnabled = group.enabled
        if (enabled) {
          groupEnabled = true
        } else if ((group.domains?.length ?? 0) === 0 && !subItems.some((sub) => sub.enabled)) {
          groupEnabled = false
        }
        return { ...group, enabled: groupEnabled, subItems }
      })
    }
    return get().saveNetworkConfig(config)
  }
  }
})
