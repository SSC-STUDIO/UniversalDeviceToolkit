import { invokeObject } from '../../../shared/bridge/bridge'

import type { DashboardGroup } from '../../../shared/settings/dashboardLayout'
export type { DashboardGroup, DashboardGroupType, DashboardItem } from '../../../shared/settings/dashboardLayout'

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
