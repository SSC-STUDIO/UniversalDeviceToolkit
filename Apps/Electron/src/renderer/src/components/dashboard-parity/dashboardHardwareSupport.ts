import type { DashboardHardwareState } from '../../api/dashboardHardware'
import type { SpecialDashboardItem } from './DashboardSpecialCard'

export function isSpecialItemSupported(
  item: SpecialDashboardItem,
  hardware: DashboardHardwareState | null
): boolean {
  if (hardware == null) return false
  if (item === 'DiscreteGpu') return hardware.discreteGpu.supported
  if (item === 'OverclockDiscreteGpu') return hardware.overclockDiscreteGpu.supported
  return hardware.turnOffMonitors.supported
}

export function requireHardwareOk(result: { ok: boolean }, message: string): void {
  if (result.ok !== true) {
    throw new Error(message)
  }
}
