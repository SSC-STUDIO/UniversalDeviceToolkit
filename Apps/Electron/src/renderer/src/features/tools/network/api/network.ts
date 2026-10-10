import { invoke, invokeObject } from '../../../../shared/bridge/bridge'

export type NetworkAccelerationMode = 'Off' | 'SystemProxy' | 'Hosts' | 'DiagnosticsOnly'

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

export interface NetworkApi {
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
}

export const networkApi: NetworkApi = {
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
  }
}
