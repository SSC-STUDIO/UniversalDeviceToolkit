import { app } from 'electron'
import { existsSync, readFileSync } from 'fs'
import { join } from 'path'

/**
 * Command-line flags for the Electron client. Mirrors the Electron app's
 * UniversalDeviceToolkit.Electron.Flags surface: same switch names, same
 * semantics, and the same external args.txt source so launch arguments
 * behave identically between the two clients.
 *
 * Switches that affect the headless host (trace / safe-start / proxy) are
 * forwarded via {@link toHostArgs}; UI-owned switches are consumed here.
 */
export interface AppFlags {
  isTraceEnabled: boolean
  minimized: boolean
  disableTrayTooltip: boolean
  allowAllPowerModesOnBattery: boolean
  forceDisableRgbKeyboardSupport: boolean
  forceDisableSpectrumKeyboardSupport: boolean
  forceDisableLenovoLighting: boolean
  experimentalGpuWorkingMode: boolean
  /** Chromium single-process mode: merge renderers into one process for memory inspection. */
  singleProcess: boolean
  proxyUrl?: string
  proxyUsername?: string
  proxyPassword?: string
  proxyAllowAllCerts: boolean
  disableUpdateChecker: boolean
  safeStart: boolean
  resetHardwareState: boolean
  resetNetworkState: boolean
  restoreProcessorMinState: boolean
  noHardware: boolean
}

const BOOL_SWITCHES: ReadonlyArray<readonly [string, keyof AppFlags]> = [
  ['--trace', 'isTraceEnabled'],
  ['--minimized', 'minimized'],
  ['--disable-tray-tooltip', 'disableTrayTooltip'],
  ['--allow-all-power-modes-on-battery', 'allowAllPowerModesOnBattery'],
  ['--force-disable-rgbkb', 'forceDisableRgbKeyboardSupport'],
  ['--force-disable-spectrumkb', 'forceDisableSpectrumKeyboardSupport'],
  ['--force-disable-lenovolighting', 'forceDisableLenovoLighting'],
  ['--experimental-gpu-working-mode', 'experimentalGpuWorkingMode'],
  ['--single-process', 'singleProcess'],
  ['--proxy-allow-all-certs', 'proxyAllowAllCerts'],
  ['--disable-update-checker', 'disableUpdateChecker'],
  ['--safe-start', 'safeStart'],
  ['--reset-hardware-state', 'resetHardwareState'],
  ['--reset-network-state', 'resetNetworkState'],
  ['--restore-processor-min-state', 'restoreProcessorMinState'],
  ['--no-hardware', 'noHardware']
]

const VALUE_SWITCHES: ReadonlyArray<readonly [string, keyof AppFlags]> = [
  ['--proxy-url', 'proxyUrl'],
  ['--proxy-username', 'proxyUsername'],
  ['--proxy-password', 'proxyPassword']
]

/**
 * Mirrors Electron Flags.StringValue: matches the key case-insensitively, taking
 * the following argument unless it starts with "--", and also accepts the
 * "--key=value" form.
 */
function stringValue(args: string[], key: string): string | undefined {
  for (let i = 0; i < args.length; i++) {
    const value = args[i]
    if (value.toLowerCase() === key) {
      const next = args[i + 1]
      return next !== undefined && !next.startsWith('--') ? next : undefined
    }
    if (value.toLowerCase().startsWith(`${key}=`)) {
      return value.slice(key.length + 1)
    }
  }
  return undefined
}

export function parseFlags(argv: string[]): AppFlags {
  const flags: AppFlags = {
    isTraceEnabled: false,
    minimized: false,
    disableTrayTooltip: false,
    allowAllPowerModesOnBattery: false,
    forceDisableRgbKeyboardSupport: false,
    forceDisableSpectrumKeyboardSupport: false,
    forceDisableLenovoLighting: false,
    experimentalGpuWorkingMode: false,
    singleProcess: false,
    proxyAllowAllCerts: false,
    disableUpdateChecker: false,
    safeStart: false,
    resetHardwareState: false,
    resetNetworkState: false,
    restoreProcessorMinState: false,
    noHardware: false
  }

  const record = flags as unknown as Record<string, unknown>

  for (const [switchName, key] of BOOL_SWITCHES) {
    if (argv.includes(switchName)) {
      record[key] = true
    }
  }

  for (const [switchName, key] of VALUE_SWITCHES) {
    const value = stringValue(argv, switchName)
    if (value !== undefined) {
      record[key] = value
    }
  }

  return flags
}

/**
 * Mirrors Electron Flags.LoadExternalArgs: reads extra switches from
 * %LOCALAPPDATA%\UniversalDeviceToolkit\args.txt (one switch per line),
 * matching the Lib Folders.AppData root. Missing/unreadable files are ignored.
 */
export function loadExternalArgs(): string[] {
  try {
    const localAppData = process.env.LOCALAPPDATA
    const root = localAppData
      ? join(localAppData, 'UniversalDeviceToolkit')
      : app.getPath('userData')
    const argsFile = join(root, 'args.txt')
    if (!existsSync(argsFile)) return []
    return readFileSync(argsFile, 'utf8')
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => line.length > 0)
  } catch {
    return []
  }
}

/** App-wide flags, parsed once from process.argv plus the external args.txt. */
export const flags: AppFlags = parseFlags([...process.argv.slice(2), ...loadExternalArgs()])

/**
 * Subset of switches understood by the headless host (HostFlags). UI-owned
 * switches (--minimized, --disable-tray-tooltip, ...) are consumed here and
 * deliberately not forwarded. `--experimental-gpu-working-mode` is forwarded
 * because the Host owns IGPUModeFeature / DGPUNotify backend selection.
 */
export function toHostArgs(appFlags: AppFlags): string[] {
  const args: string[] = []
  if (appFlags.isTraceEnabled) args.push('--trace')
  if (appFlags.safeStart) args.push('--safe-start')
  if (appFlags.noHardware) args.push('--no-hardware')
  if (appFlags.experimentalGpuWorkingMode) args.push('--experimental-gpu-working-mode')
  if (appFlags.proxyUrl) args.push('--proxy-url', appFlags.proxyUrl)
  if (appFlags.proxyUsername) args.push('--proxy-username', appFlags.proxyUsername)
  if (appFlags.proxyPassword) args.push('--proxy-password', appFlags.proxyPassword)
  if (appFlags.proxyAllowAllCerts) args.push('--proxy-allow-all-certs')
  return args
}

/** Mirrors Electron Flags.ToString() for trace logging. */
export function describeFlags(appFlags: AppFlags): string {
  return [
    `isTraceEnabled: ${appFlags.isTraceEnabled}`,
    `minimized: ${appFlags.minimized}`,
    `disableTrayTooltip: ${appFlags.disableTrayTooltip}`,
    `allowAllPowerModesOnBattery: ${appFlags.allowAllPowerModesOnBattery}`,
    `forceDisableRgbKeyboardSupport: ${appFlags.forceDisableRgbKeyboardSupport}`,
    `forceDisableSpectrumKeyboardSupport: ${appFlags.forceDisableSpectrumKeyboardSupport}`,
    `forceDisableLenovoLighting: ${appFlags.forceDisableLenovoLighting}`,
    `experimentalGpuWorkingMode: ${appFlags.experimentalGpuWorkingMode}`,
    `singleProcess: ${appFlags.singleProcess}`,
    `proxyUrl: ${appFlags.proxyUrl ?? 'null'}`,
    `proxyUsername: ${appFlags.proxyUsername ?? 'null'}`,
    'proxyPassword: [REDACTED]',
    `proxyAllowAllCerts: ${appFlags.proxyAllowAllCerts}`,
    `disableUpdateChecker: ${appFlags.disableUpdateChecker}`,
    `safeStart: ${appFlags.safeStart}`,
    `resetHardwareState: ${appFlags.resetHardwareState}`,
    `resetNetworkState: ${appFlags.resetNetworkState}`,
    `restoreProcessorMinState: ${appFlags.restoreProcessorMinState}`
  ].join(', ')
}
