import { invokeObject, on } from './bridge'

export interface SensorsInfo {
  cpuName?: string | null
  gpuName?: string | null
  gpuIsIntegrated?: boolean
}

export interface SensorsCpu {
  temperature?: number | null
  usage?: number | null
  fanSpeed?: number | null
  power?: number | null
  powerCores?: number | null
  powerMemory?: number | null
  powerPlatform?: number | null
  voltage?: number | null
  coreClockMax?: number | null
  coreClockAvg?: number | null
  pCoreClock?: number | null
  eCoreClock?: number | null
}

export interface SensorsGpu {
  usage?: number | null
  temperature?: number | null
  coreClock?: number | null
  memoryClock?: number | null
  power?: number | null
  voltage?: number | null
  vramTemperature?: number | null
  hotSpotTemperature?: number | null
  vramUtilization?: number | null
  /** Dedicated / shared GPU memory used, in MiB. */
  vramUsedMb?: number | null
  /** Dedicated / shared GPU memory total, in MiB. */
  vramTotalMb?: number | null
  pcieRxThroughput?: number | null
  pcieTxThroughput?: number | null
  fanSpeed?: number | null
}

export interface SensorsMemory {
  usage?: number | null
  /** Physical memory used, in MiB. */
  usedMb?: number | null
  /** Physical memory total, in MiB. */
  totalMb?: number | null
  highestTemperature?: number | null
}

export interface SensorsBattery {
  chargeLevel?: number | null
  health?: number | null
  temperature?: number | null
  /** Session average battery temperature (°C) when Host tracks samples. */
  avgTemperature?: number | null
  chargeRate?: number | null
  /** Session min discharge/charge rate in mW (Host Battery.MinDischargeRate). */
  minDischargeRate?: number | null
  /** Session max discharge/charge rate in mW (Host Battery.MaxDischargeRate). */
  maxDischargeRate?: number | null
  voltage?: number | null
  designCapacity?: number | null
  fullChargeCapacity?: number | null
  cycleCount?: number | null
  /** ISO date string yyyy-MM-dd from Host. */
  manufactureDate?: string | null
  /** ISO date string yyyy-MM-dd from Host. */
  firstUseDate?: string | null
  isCharging?: boolean
  isLowBattery?: boolean
  isLowPowerAdapter?: boolean
  modelName?: string | null
}

export interface SensorSnapshot {
  ts: string
  source: 'LibreHardwareMonitor' | 'vendor' | 'mixed' | 'platform'
  initialized: boolean
  isHybrid?: boolean
  info?: SensorsInfo
  cpu?: SensorsCpu
  gpu?: SensorsGpu
  memory?: SensorsMemory
  battery?: SensorsBattery
  motherboard?: { highestTemperature?: number | null }
  storage?: { temperatures?: (number | null)[] }
}

export interface SensorsStatus {
  initialized: boolean
  isHybrid?: boolean
  cpuName?: string | null
  gpuName?: string | null
  gpuIsIntegrated?: boolean
  initialState?: string
}

export interface FpsData {
  process?: string | null
  fps?: number | null
  lowFps?: number | null
  frameTimeMs?: number | null
}

export interface SensorsSettings {
  enableHardwareSensors?: boolean
  osdRefreshIntervalSec?: number
  selectedGpuIsIgpu?: boolean
  showCpuAverageFrequency?: boolean
  displayMemoryInGigabytes?: boolean
  visibleSections?: string[]
  sectionOrder?: string[]
}

export const sensorsApi = {
  getStatus: (): Promise<SensorsStatus> => invokeObject<SensorsStatus>('sensors.getStatus'),
  getSnapshot: (): Promise<SensorSnapshot> => invokeObject<SensorSnapshot>('sensors.getSnapshot'),
  getDetailed: (): Promise<SensorSnapshot> => invokeObject<SensorSnapshot>('sensors.getDetailed'),
  subscribe: (intervalSec = 1): Promise<{ subscribed: boolean; effectiveIntervalSec: number }> =>
    invokeObject('sensors.subscribe', { intervalSec, subscriberId: 'dashboard' }),
  unsubscribe: (): Promise<{ unsubscribed: boolean }> =>
    invokeObject('sensors.unsubscribe', { subscriberId: 'dashboard' }),
  getFps: (): Promise<FpsData> => invokeObject<FpsData>('sensors.getFps'),
  subscribeFps: (blacklist?: string[]): Promise<{ monitoring: boolean }> =>
    invokeObject('sensors.subscribeFps', blacklist ? { blacklist } : {}),
  unsubscribeFps: (): Promise<{ monitoring: boolean }> => invokeObject('sensors.unsubscribeFps'),
  getSettings: (): Promise<SensorsSettings> => invokeObject<SensorsSettings>('sensors.getSettings'),
  setSettings: (partial: SensorsSettings): Promise<{ saved: boolean }> =>
    invokeObject('sensors.setSettings', partial),
  onUpdated: (callback: (snapshot: SensorSnapshot) => void): (() => void) =>
    on<SensorSnapshot>('sensors.updated', callback),
  onFpsUpdated: (callback: (data: FpsData) => void): (() => void) =>
    on<FpsData>('sensors.fpsUpdated', callback)
}
