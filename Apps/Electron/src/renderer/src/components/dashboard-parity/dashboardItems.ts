import type { TFunction } from 'i18next'
import type { FeatureInfo, FeatureKey } from '../../api/features'
import type { DashboardGroup, DashboardItem } from '../../api/dashboard'

export const ALL_DASHBOARD_ITEMS: DashboardItem[] = [
  'PowerMode',
  'BatteryMode',
  'BatteryNightChargeMode',
  'AlwaysOnUsb',
  'InstantBoot',
  'HybridMode',
  'DiscreteGpu',
  'OverclockDiscreteGpu',
  'PanelLogoBacklight',
  'PortsBacklight',
  'Resolution',
  'RefreshRate',
  'DpiScale',
  'Hdr',
  'OverDrive',
  'TurnOffMonitors',
  'Microphone',
  'FlipToStart',
  'TouchpadLock',
  'FnLock',
  'WinKeyLock',
  'WhiteKeyboardBacklight',
  'ItsMode'
]

export const DEFAULT_DASHBOARD_GROUPS: DashboardGroup[] = [
  {
    type: 'Power',
    items: [
      'PowerMode',
      'ItsMode',
      'BatteryMode',
      'BatteryNightChargeMode',
      'AlwaysOnUsb',
      'InstantBoot',
      'FlipToStart'
    ]
  },
  {
    type: 'Graphics',
    items: ['HybridMode', 'DiscreteGpu', 'OverclockDiscreteGpu']
  },
  {
    type: 'Display',
    items: ['Resolution', 'RefreshRate', 'DpiScale', 'Hdr', 'OverDrive', 'TurnOffMonitors']
  },
  {
    type: 'Other',
    items: [
      'Microphone',
      'WhiteKeyboardBacklight',
      'PanelLogoBacklight',
      'PortsBacklight',
      'TouchpadLock',
      'FnLock',
      'WinKeyLock'
    ]
  }
]

const FEATURE_CANDIDATES: Partial<Record<DashboardItem, readonly FeatureKey[]>> = {
  PowerMode: ['powerMode'],
  ItsMode: ['itsMode'],
  BatteryMode: ['battery'],
  BatteryNightChargeMode: ['batteryNightCharge'],
  AlwaysOnUsb: ['alwaysOnUsb'],
  InstantBoot: ['instantBoot'],
  FlipToStart: ['flipToStart'],
  HybridMode: ['hybridMode', 'igpuMode'],
  Resolution: ['resolution'],
  RefreshRate: ['refreshRate'],
  DpiScale: ['dpiScale'],
  Hdr: ['hdr'],
  OverDrive: ['overDrive'],
  Microphone: ['microphone'],
  WhiteKeyboardBacklight: ['whiteKeyboard', 'oneLevelWhiteKeyboard'],
  PanelLogoBacklight: ['panelLogo'],
  PortsBacklight: ['portsBacklight'],
  TouchpadLock: ['touchpadLock'],
  FnLock: ['fnLock'],
  WinKeyLock: ['winKey']
}

export type FeatureInfoMap = Partial<Record<FeatureKey, FeatureInfo>>

export function resolveDashboardFeature(
  item: DashboardItem,
  infos: FeatureInfoMap
): FeatureKey | null {
  const candidates = FEATURE_CANDIDATES[item]
  if (candidates == null) return null

  return candidates.find((candidate) => infos[candidate]?.supported === true)
    ?? candidates.find((candidate) => infos[candidate] != null)
    ?? null
}

export function isSpecialDashboardItem(item: DashboardItem): boolean {
  return item === 'DiscreteGpu' || item === 'OverclockDiscreteGpu' || item === 'TurnOffMonitors'
}

/** Localized label for a dashboard item (shared by edit modal and add-item modal). */
export function dashboardItemLabel(item: DashboardItem, t: TFunction): string {
  switch (item) {
    case 'DiscreteGpu':
      return t('dashboard.edit.items.discreteGpu')
    case 'OverclockDiscreteGpu':
      return t('dashboard.edit.items.overclockGpu')
    case 'TurnOffMonitors':
      return t('dashboard.edit.items.turnOffMonitors')
    default: {
      // First candidate feature key carries the display name; support-state
      // resolution (resolveDashboardFeature) is only needed for rendering.
      const feature = FEATURE_CANDIDATES[item]?.[0] ?? null
      return feature != null ? t(`feature.${feature}`, { defaultValue: item }) : item
    }
  }
}
