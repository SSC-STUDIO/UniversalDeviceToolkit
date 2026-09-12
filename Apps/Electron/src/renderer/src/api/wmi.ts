import { invokeObject } from './bridge'

/**
 * WMI Lenovo feature-value bridge — mirror of the host `wmi.*` handlers
 * (UniversalDeviceToolkit.Host → WMI.LenovoOtherMethod.Get/SetFeatureValueAsync).
 * Currently used for the GodModeFnQSwitchable capability (SettingsPowerControl parity).
 */
export interface GodModeFnQStatus {
  /** Capability probe result (MachineInformation.Features[CapabilityID.GodModeFnQSwitchable]). */
  supported: boolean
  /** Current value (1 = enabled). null when the capability is unsupported or the read failed. */
  enabled: boolean | null
}

export const wmiApi = {
  getGodModeFnQ: (): Promise<GodModeFnQStatus> => invokeObject<GodModeFnQStatus>('wmi.getGodModeFnQ'),
  setGodModeFnQ: (enabled: boolean): Promise<{ ok: boolean }> =>
    invokeObject<{ ok: boolean }>('wmi.setGodModeFnQ', { enabled })
}
