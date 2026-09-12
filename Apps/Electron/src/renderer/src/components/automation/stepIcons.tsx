import type { ReactNode } from 'react'
import {
  ArrowClockwise24Regular,
  ArrowImport24Regular,
  ArrowSync24Regular,
  BoardRegular,
  Code24Regular,
  Desktop24Regular,
  EyeOff24Regular,
  Flash24Regular,
  Gauge24Regular,
  Key24Regular,
  Lightbulb24Regular,
  Mic24Regular,
  Options24Regular,
  Power24Regular,
  Rocket24Regular,
  ServiceBell24Regular,
  SoundSource24Regular,
  Tablet24Regular,
  TextFont24Regular,
  TopSpeed24Regular,
  UsbPlug24Regular,
  WeatherMoon24Regular,
  WeatherSunny24Regular,
  Wifi124Regular,
  WindowConsole20Regular
} from '../icons/fluent'

/**
 * Per-step icon mapping — mirrors WPF automation step SymbolRegular icons.
 */
const STEP_ICONS: Record<string, ReactNode> = {
  alwaysOnUsb: <UsbPlug24Regular />,
  battery: <Flash24Regular />,
  batteryNightCharge: <WeatherMoon24Regular />,
  deactivateGPU: <Desktop24Regular />,
  delay: <ArrowClockwise24Regular />,
  displayBrightness: <WeatherSunny24Regular />,
  dpiScale: <TextFont24Regular />,
  flipToStart: <Power24Regular />,
  fnLock: <Key24Regular />,
  godModePreset: <Options24Regular />,
  hdr: <WeatherSunny24Regular />,
  hideMainWindow: <EyeOff24Regular />,
  hybridMode: <ArrowSync24Regular />,
  instantBoot: <Power24Regular />,
  macro: <Code24Regular />,
  microphone: <Mic24Regular />,
  notification: <ServiceBell24Regular />,
  oneLevelWhiteKeyboardBacklight: <Lightbulb24Regular />,
  osd: <BoardRegular />,
  overclockDiscreteGPU: <TopSpeed24Regular />,
  overDrive: <Rocket24Regular />,
  panelLogoBacklight: <Lightbulb24Regular />,
  playSound: <SoundSource24Regular />,
  portsBacklight: <UsbPlug24Regular />,
  powerMode: <Gauge24Regular />,
  quickAction: <Rocket24Regular />,
  refreshRate: <ArrowClockwise24Regular />,
  resolution: <Desktop24Regular />,
  rgbKeyboardBacklight: <Lightbulb24Regular />,
  run: <WindowConsole20Regular />,
  showMainWindow: <Desktop24Regular />,
  speaker: <SoundSource24Regular />,
  spectrumKeyboardBacklightBrightness: <Lightbulb24Regular />,
  spectrumKeyboardBacklightImportProfile: <ArrowImport24Regular />,
  spectrumKeyboardBacklightProfile: <Lightbulb24Regular />,
  touchpadLock: <Tablet24Regular />,
  turnOffMonitors: <Desktop24Regular />,
  turnOffWiFi: <Wifi124Regular />,
  turnOnWiFi: <Wifi124Regular />,
  whiteKeyboardBacklight: <Lightbulb24Regular />,
  winKey: <Key24Regular />
}

export function stepIcon($type: string): ReactNode | null {
  return STEP_ICONS[$type] ?? null
}
