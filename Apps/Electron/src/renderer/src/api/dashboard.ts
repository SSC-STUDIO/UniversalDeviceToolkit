import { invokeObject } from './bridge'

export type DashboardGroupType = 'Power' | 'Graphics' | 'Display' | 'Other' | 'Custom'

export type DashboardItem =
  | 'PowerMode'
  | 'BatteryMode'
  | 'BatteryNightChargeMode'
  | 'AlwaysOnUsb'
  | 'InstantBoot'
  | 'HybridMode'
  | 'DiscreteGpu'
  | 'OverclockDiscreteGpu'
  | 'PanelLogoBacklight'
  | 'PortsBacklight'
  | 'Resolution'
  | 'RefreshRate'
  | 'DpiScale'
  | 'Hdr'
  | 'OverDrive'
  | 'TurnOffMonitors'
  | 'Microphone'
  | 'FlipToStart'
  | 'TouchpadLock'
  | 'FnLock'
  | 'WinKeyLock'
  | 'WhiteKeyboardBacklight'
  | 'ItsMode'

export interface DashboardGroup {
  type: DashboardGroupType
  customName?: string | null
  items: DashboardItem[]
}

export interface DashboardConfig {
  showSensors: boolean
  sensorsRefreshIntervalSeconds: number
  groups: DashboardGroup[] | null
}

export interface DashboardApi {
  getConfig(): Promise<DashboardConfig>
  saveConfig(config: DashboardConfig): Promise<{ saved: boolean }>
}

export const dashboardApi: DashboardApi = {
  async getConfig() {
    return invokeObject<DashboardConfig>('dashboard.getConfig', {})
  },

  async saveConfig(config) {
    const result = await invokeObject<{ saved: boolean }>('dashboard.saveConfig', { config })
    if (result.saved !== true) {
      throw new Error('Dashboard configuration was not saved.')
    }
    return result
  },
}
