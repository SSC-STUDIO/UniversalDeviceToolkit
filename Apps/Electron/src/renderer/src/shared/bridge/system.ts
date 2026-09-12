import { invokeObject } from './bridge'

/**
 * Machine information — mirror of the host `system.info` handler
 * (UniversalDeviceToolkit.Host → Compatibility.GetMachineInformationAsync).
 * The Electron MachineInformation exposes a serial number too, but the host does
 * not currently forward it; consumers fall back to "-".
 */
export interface SystemInfo {
  vendor?: string | null
  model?: string | null
  machineType?: string | null
  biosVersion?: string | null
  serialNumber?: string | null
  isCompatible?: boolean
}

/** Mirror of Lib PowerAdapterStatus (Power.IsPowerAdapterConnectedAsync). */
export type PowerAdapterStatus = 'Connected' | 'ConnectedLowWattage' | 'Disconnected'

/** RGB accent color from host SystemTheme helpers. */
export interface SystemAccentColor {
  r: number
  g: number
  b: number
}

export const systemApi = {
  info: (): Promise<SystemInfo> => invokeObject<SystemInfo>('system.info'),
  /** Power adapter connection state (PowerModeControl warning parity). */
  powerAdapterStatus: (): Promise<{ status: PowerAdapterStatus }> =>
    invokeObject<{ status: PowerAdapterStatus }>('system.powerAdapterStatus'),
  /** Current Windows accent (SystemTheme.GetAccentColor). */
  getAccentColor: (): Promise<SystemAccentColor> =>
    invokeObject<SystemAccentColor>('system.accentColor.get'),
  /** Write Windows accent when ApplyAccentColorToSystem is enabled. */
  setAccentColor: (color: SystemAccentColor): Promise<{ applied: boolean }> =>
    invokeObject<{ applied: boolean }>('system.accentColor.set', color)
}
