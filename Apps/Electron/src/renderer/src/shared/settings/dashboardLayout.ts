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

