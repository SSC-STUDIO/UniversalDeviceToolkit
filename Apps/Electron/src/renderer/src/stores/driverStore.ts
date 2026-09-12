import { create } from 'zustand'
import {
  optimizationApi,
  type DriverDownloadSettings,
  type DriverPackageDefinition,
  type DriverSourceType
} from '../api/optimization'

export interface DriverScanParams {
  machineType: string
  os: string
  source: DriverSourceType
}

export interface DriverStoreState {
  settings: DriverDownloadSettings | null
  packages: DriverPackageDefinition[]
  selectedIds: string[]
  loadingSettings: boolean
  scanning: boolean
  error: string | null
  isAnyRunning: boolean
}

export interface DriverStoreActions {
  loadSettings: () => Promise<void>
  scan: (params: DriverScanParams) => Promise<boolean>
  /** Refresh package statuses from the host (polled while any package is running). */
  pollStatuses: () => Promise<void>
  setOnlyShowUpdates: (enabled: boolean) => Promise<void>
  setDownloadPath: (path: string) => Promise<void>
  hidePackages: (packageIds: string[]) => Promise<void>
  showHiddenPackages: () => Promise<void>
  toggleSelected: (packageId: string) => void
  selectRecommended: () => void
  clearSelection: () => void
  startPackage: (packageId: string) => Promise<void>
  pausePackage: (packageId: string) => Promise<void>
  installPackage: (packageId: string) => Promise<void>
  uninstallPackage: (packageId: string) => Promise<void>
  startSelected: () => Promise<void>
  pauseSelected: () => Promise<void>
}

export type DriverStore = DriverStoreState & DriverStoreActions

function isRunningStatus(packageItem: DriverPackageDefinition): boolean {
  return packageItem.status === 'Downloading' || packageItem.status === 'Installing'
}

function mergeStatuses(
  packages: DriverPackageDefinition[],
  statuses: DriverPackageDefinition[]
): DriverPackageDefinition[] {
  const byId = new Map(statuses.map((p) => [p.id, p]))
  return packages.map((p) => byId.get(p.id) ?? p)
}

export const useDriverStore = create<DriverStore>((set, get) => {
  const applyPackageResult = (ok: boolean): void => {
    if (!ok) return
    void get().pollStatuses()
  }

  const applySelectedResult = async (ok: boolean): Promise<void> => {
    if (!ok) return
    await get().pollStatuses()
  }

  return {
    settings: null,
    packages: [],
    selectedIds: [],
    loadingSettings: false,
    scanning: false,
    error: null,
    isAnyRunning: false,

    async loadSettings() {
      if (get().loadingSettings) return
      set({ loadingSettings: true, error: null })
      try {
        const settings = await optimizationApi.driverGetSettings()
        set({ settings })
      } catch (error) {
        set({ error: (error as Error).message })
      } finally {
        set({ loadingSettings: false })
      }
    },

    async scan(params) {
      set({ scanning: true, error: null })
      try {
        const { packages } = await optimizationApi.driverGetPackages(params)
        set({
          packages,
          selectedIds: [],
          isAnyRunning: packages.some(isRunningStatus)
        })
        return true
      } catch (error) {
        set({ error: (error as Error).message })
        return false
      } finally {
        set({ scanning: false })
      }
    },

    async pollStatuses() {
      const { packages, selectedIds } = get()
      if (packages.length === 0) return
      const ids = [...new Set([...selectedIds, ...packages.map((p) => p.id)])]
      try {
        const { packages: statuses } = await optimizationApi.driverGetPackageStatuses(ids)
        set({
          packages: mergeStatuses(packages, statuses),
          isAnyRunning: statuses.some(isRunningStatus)
        })
      } catch (error) {
        set({ error: (error as Error).message })
      }
    },

    async setOnlyShowUpdates(enabled) {
      try {
        await optimizationApi.driverSetOnlyShowUpdates(enabled)
        set((state) => ({
          settings: state.settings ? { ...state.settings, onlyShowUpdates: enabled } : null
        }))
      } catch (error) {
        set({ error: (error as Error).message })
      }
    },

    async setDownloadPath(path) {
      if (!path) return
      try {
        await optimizationApi.driverSetDownloadPath(path)
        set((state) => ({
          settings: state.settings ? { ...state.settings, downloadPath: path } : null
        }))
      } catch (error) {
        set({ error: (error as Error).message })
      }
    },

    async hidePackages(packageIds) {
      const settings = get().settings
      if (!settings) return
      const hiddenPackageIds = [...new Set([...settings.hiddenPackageIds, ...packageIds])]
      set({
        settings: { ...settings, hiddenPackageIds },
        selectedIds: get().selectedIds.filter((id) => !packageIds.includes(id))
      })
      try {
        await optimizationApi.driverSetHiddenPackageIds(hiddenPackageIds)
      } catch (error) {
        set({ error: (error as Error).message })
      }
    },

    async showHiddenPackages() {
      const settings = get().settings
      if (!settings) return
      set({ settings: { ...settings, hiddenPackageIds: [] } })
      try {
        await optimizationApi.driverSetHiddenPackageIds([])
      } catch (error) {
        set({ error: (error as Error).message })
      }
    },

    toggleSelected(packageId) {
      const selectedIds = get().selectedIds
      set({
        selectedIds: selectedIds.includes(packageId)
          ? selectedIds.filter((id) => id !== packageId)
          : [...selectedIds, packageId]
      })
    },

    selectRecommended() {
      const recommendedIds = get()
        .packages.filter((p) => p.isRecommended && p.status !== 'Completed')
        .map((p) => p.id)
      set({ selectedIds: [...new Set([...get().selectedIds, ...recommendedIds])] })
    },

    clearSelection() {
      set({ selectedIds: [] })
    },

    async startPackage(packageId) {
      try {
        const res = await optimizationApi.driverStartPackage(packageId)
        applyPackageResult(res.ok)
      } catch (error) {
        set({ error: (error as Error).message })
      }
    },

    async pausePackage(packageId) {
      try {
        const res = await optimizationApi.driverPausePackage(packageId)
        applyPackageResult(res.ok)
      } catch (error) {
        set({ error: (error as Error).message })
      }
    },

    async installPackage(packageId) {
      try {
        const res = await optimizationApi.driverInstallPackage(packageId)
        applyPackageResult(res.ok)
      } catch (error) {
        set({ error: (error as Error).message })
      }
    },

    async uninstallPackage(packageId) {
      try {
        const res = await optimizationApi.driverUninstallPackage(packageId)
        applyPackageResult(res.ok)
      } catch (error) {
        set({ error: (error as Error).message })
      }
    },

    async startSelected() {
      const { selectedIds } = get()
      for (const id of selectedIds) {
        const packageItem = get().packages.find((p) => p.id === id)
        if (!packageItem || packageItem.status === 'Completed') continue
        try {
          const res = await optimizationApi.driverStartPackage(id)
          if (res.ok) await get().pollStatuses()
        } catch (error) {
          set({ error: (error as Error).message })
        }
      }
      await applySelectedResult(true)
    },

    async pauseSelected() {
      const { selectedIds } = get()
      for (const id of selectedIds) {
        const packageItem = get().packages.find((p) => p.id === id)
        if (!packageItem || !isRunningStatus(packageItem)) continue
        try {
          const res = await optimizationApi.driverPausePackage(id)
          if (res.ok) await get().pollStatuses()
        } catch (error) {
          set({ error: (error as Error).message })
        }
      }
      await applySelectedResult(true)
    }
  }
})
