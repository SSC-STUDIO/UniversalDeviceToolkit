import { invokeObject } from '../../../../shared/bridge/bridge'

/** Driver package source (PackageDownloaderFactory.Type). */
export type DriverSourceType = 'Vantage' | 'PCSupport'

/** Package lifecycle mirroring PackageControl.PackageStatus. */
export type DriverPackageStatus =
  | 'NotStarted'
  | 'Queued'
  | 'Downloading'
  | 'Installing'
  | 'Completed'
  | 'Error'

export type DriverSortMode = 'name' | 'category' | 'date'

/** Reboot requirement of an update package (RebootType). */
export type DriverRebootType = 'None' | 'Delayed' | 'Requested' | 'Forced' | 'ForcedPowerOff'

/** Driver package list item (Package). */
export interface DriverPackageDefinition {
  id: string
  title: string
  description: string
  category: string
  /** Searchable index text (title + category + keywords). */
  index: string
  isRecommended: boolean
  isUpdate: boolean
  /** ISO date string, null when unknown. */
  releaseDate: string | null
  version: string | null
  fileSize: string | null
  fileName: string | null
  readmeUrl: string | null
  reboot: DriverRebootType
  status: DriverPackageStatus
  /** 0..1 progress of the current download/install. */
  progress: number
  error: string | null
}

/** Persisted driver download settings (PackageDownloaderSettings.Store). */
export interface DriverDownloadSettings {
  machineType: string
  os: string
  osOptions: string[]
  downloadPath: string
  onlyShowUpdates: boolean
  hiddenPackageIds: string[]
}

export interface DriverApi {
  // Driver download
  driverGetSettings(): Promise<DriverDownloadSettings>
  driverGetPackages(params: {
    machineType: string
    os: string
    source: DriverSourceType
  }): Promise<{ packages: DriverPackageDefinition[] }>
  driverGetPackageStatuses(packageIds: string[]): Promise<{
    packages: DriverPackageDefinition[]
  }>
  driverStartPackage(packageId: string): Promise<{ ok: boolean }>
  driverPausePackage(packageId: string): Promise<{ ok: boolean }>
  driverInstallPackage(packageId: string): Promise<{ ok: boolean }>
  driverUninstallPackage(packageId: string): Promise<{ ok: boolean }>
  driverSetDownloadPath(path: string): Promise<{ saved: boolean }>
  driverSetOnlyShowUpdates(enabled: boolean): Promise<{ saved: boolean }>
  driverSetHiddenPackageIds(packageIds: string[]): Promise<{ saved: boolean }>
}

export const driverApi: DriverApi = {
  async driverGetSettings() {
    return invokeObject<DriverDownloadSettings>('driver.getSettings', {})
  },

  async driverGetPackages(params) {
    return invokeObject<{ packages: DriverPackageDefinition[] }>('driver.getPackages', params)
  },

  async driverGetPackageStatuses(packageIds) {
    return invokeObject<{ packages: DriverPackageDefinition[] }>('driver.getPackageStatuses', {
      packageIds
    })
  },

  async driverStartPackage(packageId) {
    return invokeObject<{ ok: boolean }>('driver.start', { packageId })
  },

  async driverPausePackage(packageId) {
    return invokeObject<{ ok: boolean }>('driver.pause', { packageId })
  },

  async driverInstallPackage(packageId) {
    return invokeObject<{ ok: boolean }>('driver.install', { packageId })
  },

  async driverUninstallPackage(packageId) {
    return invokeObject<{ ok: boolean }>('driver.uninstall', { packageId })
  },

  async driverSetDownloadPath(path) {
    return invokeObject<{ saved: boolean }>('driver.setDownloadPath', { path })
  },

  async driverSetOnlyShowUpdates(enabled) {
    return invokeObject<{ saved: boolean }>('driver.setOnlyShowUpdates', { enabled })
  },

  async driverSetHiddenPackageIds(packageIds) {
    return invokeObject<{ saved: boolean }>('driver.setHiddenPackageIds', { packageIds })
  }
}
