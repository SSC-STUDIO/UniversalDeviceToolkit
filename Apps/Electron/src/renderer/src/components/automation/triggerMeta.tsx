import {
  ArrowSync24Regular,
  BatteryCharge24Regular,
  Desktop24Regular,
  Gauge24Regular,
  Globe24Regular,
  Hdr24Regular,
  Heart24Regular,
  Link24Regular,
  LockClosed24Regular,
  Play24Regular,
  Power24Regular,
  Timer24Regular,
  UsbPlug24Regular,
  XboxController24Regular
} from '@fluentui/react-icons'
import type { ReactNode } from 'react'
import { normalizeTriggerKind, type TriggerKind } from './triggers'

/**
 * Per-trigger icon mapping — port of Electron
 * Extensions/AutomationPipelineTriggerExtensions.cs (SymbolRegular).
 *
 * AC adapter cards use Link (connection/node) to match the original UDT
 * collapsed chrome; triggerless quick actions use Play on the page.
 */

const TRIGGER_ICONS: Record<TriggerKind, ReactNode> = {
  aCAdapterConnected: <Link24Regular />,
  lowWattageACAdapterConnected: <Link24Regular />,
  aCAdapterDisconnected: <Link24Regular />,
  powerMode: <Gauge24Regular />,
  godModePresetChanged: <Gauge24Regular />,
  gamesAreRunning: <XboxController24Regular />,
  gamesStop: <XboxController24Regular />,
  processesAreRunning: <Desktop24Regular />,
  processesStopRunning: <Desktop24Regular />,
  userInactivity: <Timer24Regular />,
  sessionLock: <LockClosed24Regular />,
  sessionUnlock: <LockClosed24Regular />,
  lidOpened: <Desktop24Regular />,
  lidClosed: <Desktop24Regular />,
  displayOn: <Desktop24Regular />,
  displayOff: <Desktop24Regular />,
  hdrOn: <Hdr24Regular />,
  hdrOff: <Hdr24Regular />,
  deviceConnected: <UsbPlug24Regular />,
  deviceDisconnected: <UsbPlug24Regular />,
  externalDisplayConnected: <Desktop24Regular />,
  externalDisplayDisconnected: <Desktop24Regular />,
  wiFiConnected: <Globe24Regular />,
  wiFiDisconnected: <Globe24Regular />,
  time: <Timer24Regular />,
  periodic: <ArrowSync24Regular />,
  hardwareSensor: <Heart24Regular />,
  batteryPercentage: <BatteryCharge24Regular />,
  onStartup: <Power24Regular />,
  onResume: <Power24Regular />,
  and: <Link24Regular />
}

/** Default icon for triggerless quick actions (Electron play triangle). */
export const QUICK_ACTION_ICON: ReactNode = <Play24Regular />

/** Icon for a serialized trigger $type; null when the type is unknown. */
export function triggerIcon($type: string): ReactNode | null {
  const stripped = $type.replace(/AutomationPipelineTrigger$/i, '')
  if (/^and$/i.test(stripped)) return TRIGGER_ICONS.and
  const key = normalizeTriggerKind($type)
  return key === null ? null : (TRIGGER_ICONS[key] ?? null)
}
