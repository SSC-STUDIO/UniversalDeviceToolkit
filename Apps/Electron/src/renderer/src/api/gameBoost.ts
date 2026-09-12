import { invoke, on } from './bridge'

export interface GameBoostConfig {
  autoGameBoost: boolean
  boostGamePriority: boolean
  optimizeCpuAffinity: boolean
  suppressBackgroundProcesses: boolean
  muteNotifications: boolean
  gamePowerPlanGuid: string | null
  customGameProcesses: string[]
  backgroundWhitelist: string[]
}

export interface GameBoostStatus {
  isBoosting: boolean
  activeGameProcess: string | null
  activeGameProcessId: number | null
  boostedProcesses: string[]
  suppressedProcessesCount: number
}

export const DEFAULT_GAME_BOOST_CONFIG: GameBoostConfig = {
  autoGameBoost: true,
  boostGamePriority: true,
  optimizeCpuAffinity: true,
  suppressBackgroundProcesses: true,
  muteNotifications: false,
  gamePowerPlanGuid: null,
  customGameProcesses: [],
  backgroundWhitelist: [
    'obs64',
    'obs32',
    'discord',
    'steam',
    'steamwebhelper',
    'epicgameslauncher',
    'voicemeeter',
    'voicemeeterpro',
    'voicemeeter8',
    'spotify',
    'devenv',
    'rider64',
    'code',
    'UniversalDeviceToolkit',
    'UniversalDeviceToolkit.Host',
    'UniversalDeviceToolkit.Electron'
  ]
}

export const gameBoostApi = {
  getStatus(): Promise<GameBoostStatus> {
    return invoke<GameBoostStatus>('gameBoost.getStatus', {})
  },

  getConfig(): Promise<GameBoostConfig> {
    return invoke<GameBoostConfig>('gameBoost.getConfig', {})
  },

  saveConfig(config: GameBoostConfig): Promise<{ saved: boolean }> {
    return invoke<{ saved: boolean }>('gameBoost.saveConfig', { config })
  },

  boostNow(): Promise<{ success: boolean; status: GameBoostStatus }> {
    return invoke<{ success: boolean; status: GameBoostStatus }>('gameBoost.boostNow', {})
  },

  revertNow(): Promise<{ success: boolean; status: GameBoostStatus }> {
    return invoke<{ success: boolean; status: GameBoostStatus }>('gameBoost.revertNow', {})
  },

  onStatusChanged(callback: (status: GameBoostStatus) => void): () => void {
    return on('gameBoost.statusChanged', (data) => {
      if (data && typeof data === 'object') {
        callback(data as unknown as GameBoostStatus)
      }
    })
  }
}
