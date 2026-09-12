import { invokeObject } from './bridge'

export interface HostCapabilityMap {
  settings: boolean
  sensors: boolean
  sensorsWrite: boolean
  dashboard: boolean
  autorun: boolean
  systemInfo: boolean
  features: boolean
  automation: boolean
  optimization: boolean
  godMode: boolean
  keyboard: boolean
  rgb: boolean
  spectrum: boolean
  bootLogo: boolean
  network: boolean
  ai: boolean
  driver: boolean
  cleanup: boolean
  macro: boolean
  updates: boolean
  fps: boolean
  accentColor: boolean
  gpuManagement: boolean
  fanControl: boolean
  keyboardBacklight: boolean
  batteryManagement: boolean
  displayControl: boolean
  powerProfile: boolean
  systemTelemetry: boolean
}

export interface HostBackendMap {
  platformServices: boolean
  deviceAdapter: boolean
  sensorBackend: boolean
  gpuBackend: boolean
  powerProfile: boolean
  autorun: boolean
  configuration: boolean
}

export interface HostDeviceIdentity {
  platform: string
  architecture: string
  vendor?: string | null
  model?: string | null
  productName?: string | null
  biosVersion?: string | null
  serialNumber?: string | null
  machineType?: string | null
  source: string
  supportLevel: string
}

export interface HostCapabilities {
  platform: 'windows' | 'linux' | 'macos' | string
  portable: boolean
  vendorHardware: boolean
  capabilities: Partial<HostCapabilityMap>
  backends: Partial<HostBackendMap>
  powerProfiles?: {
    available: boolean
    profiles: string[]
    active: string | null
  }
  device?: HostDeviceIdentity | null
  implementedMethods: string[]
  unsupportedMethods: string[]
}

function isBooleanRecord(value: unknown): value is Record<string, boolean> {
  if (value == null || typeof value !== 'object' || Array.isArray(value)) return false
  return Object.values(value).every((entry) => typeof entry === 'boolean')
}

function isStringArray(value: unknown): value is string[] {
  return Array.isArray(value) && value.every((entry) => typeof entry === 'string')
}

export function normalizeHostCapabilities(value: unknown): HostCapabilities {
  if (value == null || typeof value !== 'object' || Array.isArray(value)) {
    throw new Error('host.getCapabilities returned an invalid payload')
  }
  const record = value as Record<string, unknown>
  if (typeof record.platform !== 'string' || typeof record.portable !== 'boolean') {
    throw new Error('host.getCapabilities returned an invalid payload')
  }
  if (!isBooleanRecord(record.capabilities) || !isBooleanRecord(record.backends)) {
    throw new Error('host.getCapabilities returned an invalid payload')
  }
  if (!isStringArray(record.implementedMethods) || !isStringArray(record.unsupportedMethods)) {
    throw new Error('host.getCapabilities returned an invalid payload')
  }

  return value as HostCapabilities
}

export async function getHostCapabilities(): Promise<HostCapabilities> {
  return normalizeHostCapabilities(await invokeObject('host.getCapabilities'))
}
