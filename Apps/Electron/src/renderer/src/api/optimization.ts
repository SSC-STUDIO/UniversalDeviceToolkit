import { invoke, invokeObject } from './bridge'

export type NetworkAccelerationMode = 'Off' | 'SystemProxy' | 'Hosts' | 'DiagnosticsOnly'

export interface OptimizationActionDefinition {
  key: string
  title: string
  description: string
  recommended: boolean
  /** applied state: true = applied, false = not applied, null = unknown */
  applied: boolean | null
}

export interface OptimizationCategoryDefinition {
  key: string
  title: string
  description: string
  hasSettings: boolean
  actions: OptimizationActionDefinition[]
}

export interface NetworkDomainSubItem {
  id: string
  displayName: string
  domain: string
  enabled: boolean
  isBeta: boolean
}

export interface NetworkDomainGroup {
  id: string
  displayName: string
  enabled: boolean
  isFavorite: boolean
  domains: string[]
  subItems: NetworkDomainSubItem[]
  iconKey: string | null
  description: string | null
}

export interface NetworkRecoverySnapshotMetadata {
  capturedAtUtc: string | null
  snapshotPath: string | null
  hadSystemProxy: boolean
  hadHostsBlock: boolean
  hadPacPath: boolean
  notes: string | null
}

export interface NetworkAccelerationConfig {
  accelerationEnabled: boolean
  mode: NetworkAccelerationMode
  listenPort: number
  domainGroups: NetworkDomainGroup[]
  dnsServer: string | null
  dohUrl: string | null
  certificateFingerprintSha256: string | null
  lastRecoverySnapshot: NetworkRecoverySnapshotMetadata | null
  showInNavigation: boolean
}

export interface NetworkAccelerationStatus {
  config: NetworkAccelerationConfig
  isBackendReady: boolean
  isRunning: boolean
  statusText: string
}

/** Custom cleanup rule (WindowsOptimizationPage.Cleanup.cs → CustomCleanupRule). */
export interface CustomCleanupRule {
  directoryPath: string
  recursive: boolean
  extensions: string[]
}

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

/** Traffic snapshot (NetworkProxyTrafficSnapshot). */
export interface NetworkTrafficSnapshot {
  bytesUploaded: number
  bytesDownloaded: number
  activeConnections: number
  totalConnections: number
}

/** A single proxied connection (NetworkProxyConnectionSnapshot). */
export interface NetworkConnectionSnapshot {
  host: string
  port: number
  state: string
  connectLatencyMs: number | null
}

/** Per-destination statistics (NetworkProxyDestinationSnapshot). */
export interface NetworkDestinationSnapshot {
  host: string
  port: number
  totalConnections: number
  activeConnections: number
  lastConnectLatencyMs: number | null
}

/** Runtime snapshot (NetworkProxyRuntimeSnapshot). */
export interface NetworkRuntimeSnapshot {
  healthStatus: string
  traffic: NetworkTrafficSnapshot
  connections: NetworkConnectionSnapshot[]
  destinations: NetworkDestinationSnapshot[]
}

/** NAT detection result (NatTypeDetector). */
export interface NetworkNatDetectionResult {
  natType: 'OpenInternet' | 'Nat' | 'UdpBlocked' | 'Unknown'
  localIp: string | null
  publicIp: string | null
  internetAvailable: boolean
  error: string | null
}

/** DNS probe result (DnsProbeResult). */
export interface NetworkDnsDetectionResult {
  success: boolean
  elapsedMs: number
  addresses: string[]
  error: string | null
}

/** IPv6 detection result (Ipv6Detector). */
export interface NetworkIpv6DetectionResult {
  supported: boolean
  address: string | null
  error: string | null
}

export interface OptimizationApi {
  getCategories(): Promise<{ categories: OptimizationCategoryDefinition[] }>
  apply(actionKeys: string[]): Promise<{ applied: boolean }>
  revert(actionKeys: string[]): Promise<{ reverted: boolean }>
  applyRecommended(): Promise<{ applied: boolean }>
  getActionStatus(actionKey: string): Promise<{ applied: boolean | null }>
  estimateCleanup(actionKeys: string[]): Promise<{ bytes: number }>
  runCleanup(actionKeys: string[]): Promise<{ done: boolean }>
  networkGetStatus(): Promise<NetworkAccelerationStatus>
  networkSaveConfig(config: NetworkAccelerationConfig): Promise<{ saved: boolean }>
  networkStart(): Promise<{ ok: boolean }>
  networkStop(): Promise<{ ok: boolean }>
  networkGetTrafficSnapshot(): Promise<NetworkTrafficSnapshot | null>
  networkGetRuntimeSnapshot(): Promise<NetworkRuntimeSnapshot | null>
  networkRestore(): Promise<{ ok: boolean }>
  networkDetectNat(stunServer: string): Promise<NetworkNatDetectionResult>
  networkDetectDns(params: {
    domain: string
    dnsServer?: string
    dohEnabled: boolean
    dohUrl?: string
  }): Promise<NetworkDnsDetectionResult>
  networkDetectIpv6(): Promise<NetworkIpv6DetectionResult>

  // Custom cleanup rules
  getCustomCleanupRules(): Promise<{ rules: CustomCleanupRule[] }>
  saveCustomCleanupRules(rules: CustomCleanupRule[]): Promise<{ saved: boolean }>
  /** Native folder picker; null when cancelled. */
  selectFolder(): Promise<string | null>
  /** Open a path in the system explorer. */
  openPath(path: string): Promise<{ ok: boolean }>
  /** Open an http(s) URL in the default browser. */
  openUrl(url: string): Promise<{ ok: boolean }>

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

export const optimizationApi: OptimizationApi = {
  async getCategories() {
    return invokeObject<{ categories: OptimizationCategoryDefinition[] }>('optimization.getCategories', {})
  },

  async apply(actionKeys) {
    return invokeObject<{ applied: boolean }>('optimization.apply', { actionKeys })
  },

  async revert(actionKeys) {
    return invokeObject<{ reverted: boolean }>('optimization.revert', { actionKeys })
  },

  async applyRecommended() {
    return invokeObject<{ applied: boolean }>('optimization.applyRecommended', {})
  },

  async getActionStatus(actionKey) {
    return invokeObject<{ applied: boolean | null }>('optimization.getActionStatus', { actionKey })
  },

  async estimateCleanup(actionKeys) {
    return invokeObject<{ bytes: number }>('cleanup.estimate', { actionKeys })
  },

  async runCleanup(actionKeys) {
    return invokeObject<{ done: boolean }>('cleanup.run', { actionKeys })
  },

  async networkGetStatus() {
    return invokeObject<NetworkAccelerationStatus>('network.getStatus', {})
  },

  async networkSaveConfig(config) {
    return invokeObject<{ saved: boolean }>('network.saveConfig', { config })
  },

  async networkStart() {
    return invokeObject<{ ok: boolean }>('network.start', {})
  },

  async networkStop() {
    return invokeObject<{ ok: boolean }>('network.stop', {})
  },

  async networkGetTrafficSnapshot() {
    return invoke<NetworkTrafficSnapshot | null>('network.getTrafficSnapshot', {})
  },

  async networkGetRuntimeSnapshot() {
    return invoke<NetworkRuntimeSnapshot | null>('network.getRuntimeSnapshot', {})
  },

  async networkRestore() {
    return invokeObject<{ ok: boolean }>('network.restore', {})
  },

  async networkDetectNat(stunServer) {
    return invokeObject<NetworkNatDetectionResult>('network.detectNat', { stunServer })
  },

  async networkDetectDns(params) {
    return invokeObject<NetworkDnsDetectionResult>('network.detectDns', params)
  },

  async networkDetectIpv6() {
    return invokeObject<NetworkIpv6DetectionResult>('network.detectIpv6', {})
  },

  async getCustomCleanupRules() {
    return invokeObject<{ rules: CustomCleanupRule[] }>('cleanup.getCustomRules', {})
  },

  async saveCustomCleanupRules(rules) {
    return invokeObject<{ saved: boolean }>('cleanup.saveCustomRules', { rules })
  },

  async selectFolder() {
    return invoke<string | null>('dialog:select-folder', {})
  },

  async openPath(path) {
    return invokeObject<{ ok: boolean }>('dialog:open-path', { path })
  },

  async openUrl(url) {
    return invokeObject<{ ok: boolean }>('dialog:open-url', { url })
  },

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
