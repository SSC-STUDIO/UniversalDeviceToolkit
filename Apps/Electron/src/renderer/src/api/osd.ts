import { settingsApi } from './settings'

/**
 * "osd" settings scope (osd.json) — mirrors OsdSettingsStore in
 * UniversalDeviceToolkit.Lib. The host serializes the store with camelCase
 * keys; defaults match the Electron OsdSettings.cs store initializers.
 */
export interface OsdSettingsStore {
  showOsd: boolean
  osdRefreshInterval: number
  selectedStyleIndex: number
  items: string[]
  backgroundOpacity: number
  backgroundColor: string
  fontSize: number
  cornerRadiusTop: number
  cornerRadiusBottom: number
  isLocked: boolean
  panelPositionX: number | null
  panelPositionY: number | null
  barPositionX: number | null
  barPositionY: number | null
  tempThresholdWarning: number
  tempThresholdCritical: number
  usageThresholdWarning: number
  usageThresholdCritical: number
  fpsThresholdCritical: number
  lowFpsDeltaThreshold: number
  categoryColor: string
  labelColor: string
  valueColor: string
  warningColor: string
  criticalColor: string
  separatorColor: string
  snapThreshold: number
}

/** OsdItem enum names (Enums.cs) — persisted verbatim in osd.json. */
export const OSD_ITEMS = [
  'Fps',
  'LowFps',
  'FrameTime',
  'CpuFrequency',
  'CpuPCoreFrequency',
  'CpuECoreFrequency',
  'CpuUtilization',
  'CpuTemperature',
  'CpuPower',
  'CpuFan',
  'GpuFrequency',
  'GpuUtilization',
  'GpuTemperature',
  'GpuVramUtilization',
  'GpuVramTemperature',
  'GpuPower',
  'GpuFan',
  'MemoryUtilization',
  'MemoryTemperature',
  'Disk1Temperature',
  'Disk2Temperature',
  'PchTemperature',
  'PchFan'
] as const

export type OsdItemName = (typeof OSD_ITEMS)[number]

export const DEFAULT_OSD_SETTINGS: OsdSettingsStore = {
  showOsd: false,
  osdRefreshInterval: 1,
  selectedStyleIndex: 0,
  items: [...OSD_ITEMS],
  backgroundOpacity: 0.6,
  backgroundColor: '#1E1E1E',
  fontSize: 12,
  cornerRadiusTop: 6,
  cornerRadiusBottom: 6,
  isLocked: false,
  panelPositionX: null,
  panelPositionY: null,
  barPositionX: null,
  barPositionY: null,
  tempThresholdWarning: 75,
  tempThresholdCritical: 90,
  usageThresholdWarning: 70,
  usageThresholdCritical: 90,
  fpsThresholdCritical: 30,
  lowFpsDeltaThreshold: 30,
  categoryColor: '#2196F3',
  labelColor: '#ADFF2F',
  valueColor: '#FFFFFF',
  warningColor: '#FFFF00',
  criticalColor: '#FF0000',
  separatorColor: '#555555',
  snapThreshold: 20
}

/** camelCase model → PascalCase host store shape (settings stores are
 *  serialized with their .NET property names). */
function toHostStore(store: OsdSettingsStore): Record<string, unknown> {
  const pascal = (key: string): string => key.charAt(0).toUpperCase() + key.slice(1)
  const result: Record<string, unknown> = {}
  for (const [key, value] of Object.entries(store)) {
    result[pascal(key)] = value
  }
  return result
}

export const osdApi = {
  async get(): Promise<OsdSettingsStore> {
    const result = await settingsApi.get('osd')
    if (result.value != null && (typeof result.value !== 'object' || Array.isArray(result.value))) {
      throw new Error('osd settings payload is invalid')
    }
    const raw = (result.value ?? {}) as Record<string, unknown>
    const merged: OsdSettingsStore = { ...DEFAULT_OSD_SETTINGS }
    const pascal = (key: string): string => key.charAt(0).toUpperCase() + key.slice(1)
    for (const key of Object.keys(DEFAULT_OSD_SETTINGS) as (keyof OsdSettingsStore)[]) {
      const value = raw[pascal(key)]
      if (value !== undefined && value !== null) {
        ;(merged as unknown as Record<string, unknown>)[key] = value
      }
    }
    if (Array.isArray(raw['Items'])) {
      merged.items = (raw['Items'] as unknown[]).filter(
        (item): item is OsdItemName =>
          typeof item === 'string' && (OSD_ITEMS as readonly string[]).includes(item)
      )
    }
    return merged
  },

  /**
   * settings.set replaces the whole store, so always send the complete store
   * (the host copies every property of the deserialized value).
   */
  async save(store: OsdSettingsStore): Promise<void> {
    await settingsApi.set('osd', toHostStore(store))
    await settingsApi.save(['osd'])
  }
}
