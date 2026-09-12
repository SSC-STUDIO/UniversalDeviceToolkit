import { create } from 'zustand'
import { networkApi, type NetworkAccelerationConfig, type NetworkAccelerationStatus, type NetworkRuntimeSnapshot, type NetworkTrafficSnapshot } from '../api/network'

interface NetworkStore {
  sampledAt: number | null
  uploadSamples: number[]
  downloadSamples: number[]
  uploadRate: number
  downloadRate: number
  networkStatus: NetworkAccelerationStatus | null
  trafficSnapshot: NetworkTrafficSnapshot | null
  runtimeSnapshot: NetworkRuntimeSnapshot | null
  error: string | null
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

export const useNetworkStore = create<NetworkStore>((set, get) => ({
  sampledAt: null, uploadSamples: [], downloadSamples: [], uploadRate: 0, downloadRate: 0,
  networkStatus: null,
  trafficSnapshot: null,
  runtimeSnapshot: null,
  error: null,

  async loadNetwork() {
    try {
      const status = await networkApi.networkGetStatus()
      set({ networkStatus: status })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  async saveNetworkConfig(config) {
    try {
      const res = await networkApi.networkSaveConfig(config)
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
      const res = await networkApi.networkStart()
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
      const res = await networkApi.networkStop()
      if (!res.ok) return false
      set({ trafficSnapshot: null, runtimeSnapshot: null, sampledAt: null, uploadSamples: [], downloadSamples: [], uploadRate: 0, downloadRate: 0 })
      await get().loadNetwork()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async loadTraffic() {
    try {
      const snapshot = await networkApi.networkGetTrafficSnapshot()
      const at = Date.now()
      const previous = get()
      const elapsed = previous.sampledAt === null ? 0 : Math.max(0.25, (at - previous.sampledAt) / 1000)
      const priorSnapshot = previous.trafficSnapshot
      const reset = snapshot === null || priorSnapshot === null || snapshot.bytesUploaded < priorSnapshot.bytesUploaded
      const uploadRate = reset || elapsed === 0 ? 0 : Math.max(0, (snapshot.bytesUploaded - priorSnapshot.bytesUploaded) / elapsed)
      const downloadRate = reset || elapsed === 0 ? 0 : Math.max(0, (snapshot.bytesDownloaded - priorSnapshot.bytesDownloaded) / elapsed)
      set({
        trafficSnapshot: snapshot, sampledAt: at, uploadRate, downloadRate,
        uploadSamples: [...(reset ? [] : previous.uploadSamples), uploadRate / 1024].slice(-60),
        downloadSamples: [...(reset ? [] : previous.downloadSamples), downloadRate / 1024].slice(-60)
      })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  async loadRuntime() {
    try {
      const snapshot = await networkApi.networkGetRuntimeSnapshot()
      set({ runtimeSnapshot: snapshot })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  async restoreNetwork() {
    try {
      const res = await networkApi.networkRestore()
      set({ trafficSnapshot: null, runtimeSnapshot: null, sampledAt: null, uploadSamples: [], downloadSamples: [], uploadRate: 0, downloadRate: 0 })
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
}))
