/**
 * Automation pipeline trigger catalog — port of Electron
 * Windows/Automation/CreateAutomationPipelineWindow.cs (trigger factory list),
 * Windows/Automation/AutomationPipelineTriggerConfigurationWindow.cs (validity),
 * Controls/Automation/AutomationPipelineControl.cs (subtitle formatting).
 *
 * Discriminators follow AutomationJsonDiscriminators.ForTrigger: class name
 * without the "AutomationPipelineTrigger" suffix, camelCased. Property names
 * are serialized camelCase; the host reads case-insensitively.
 */

export interface AutomationTrigger extends Record<string, unknown> {
  $type: string
}

export type TriggerKind =
  | 'aCAdapterConnected'
  | 'lowWattageACAdapterConnected'
  | 'aCAdapterDisconnected'
  | 'powerMode'
  | 'godModePresetChanged'
  | 'gamesAreRunning'
  | 'gamesStop'
  | 'processesAreRunning'
  | 'processesStopRunning'
  | 'userInactivity'
  | 'sessionLock'
  | 'sessionUnlock'
  | 'lidOpened'
  | 'lidClosed'
  | 'displayOn'
  | 'displayOff'
  | 'hdrOn'
  | 'hdrOff'
  | 'deviceConnected'
  | 'deviceDisconnected'
  | 'externalDisplayConnected'
  | 'externalDisplayDisconnected'
  | 'wiFiConnected'
  | 'wiFiDisconnected'
  | 'time'
  | 'periodic'
  | 'hardwareSensor'
  | 'batteryPercentage'
  | 'onStartup'
  | 'onResume'
  | 'and'

export interface TriggerDefinition {
  kind: TriggerKind
  /** i18n key under `automation.triggerNames` (falls back to `wpf.*` display names). */
  nameKey: string
  /** True when the trigger has a parameter editor (IAutomationPipelineTriggerTabItemContent). */
  configurable: boolean
  /** True for IDisallowDuplicatesAutomationPipelineTrigger markers. */
  disallowDuplicates: boolean
  /** Display name source: wpf.<key> when set, otherwise automation.triggerNames.<kind>. */
  wpfKey?: string
  createDefault: () => AutomationTrigger
}

export const TRIGGER_DEFINITIONS: TriggerDefinition[] = [
  { kind: 'aCAdapterConnected', nameKey: 'aCAdapterConnected', configurable: false, disallowDuplicates: true, wpfKey: 'aCAdapterConnectedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'aCAdapterConnected' }) },
  { kind: 'lowWattageACAdapterConnected', nameKey: 'lowWattageACAdapterConnected', configurable: false, disallowDuplicates: true, wpfKey: 'lowWattageACAdapterConnectedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'lowWattageACAdapterConnected' }) },
  { kind: 'aCAdapterDisconnected', nameKey: 'aCAdapterDisconnected', configurable: false, disallowDuplicates: true, wpfKey: 'aCAdapterDisconnectedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'aCAdapterDisconnected' }) },
  { kind: 'powerMode', nameKey: 'powerMode', configurable: true, disallowDuplicates: false, wpfKey: 'powerModeAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'powerMode', powerModeState: 'Balance' }) },
  { kind: 'godModePresetChanged', nameKey: 'godModePresetChanged', configurable: true, disallowDuplicates: false, wpfKey: 'godModePresetChangedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'godModePresetChanged', presetId: '00000000-0000-0000-0000-000000000000' }) },
  { kind: 'gamesAreRunning', nameKey: 'gamesAreRunning', configurable: false, disallowDuplicates: true, wpfKey: 'gamesAreRunningAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'gamesAreRunning' }) },
  { kind: 'gamesStop', nameKey: 'gamesStop', configurable: false, disallowDuplicates: true, wpfKey: 'gamesStopAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'gamesStop' }) },
  { kind: 'processesAreRunning', nameKey: 'processesAreRunning', configurable: true, disallowDuplicates: false, wpfKey: 'processesAreRunningAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'processesAreRunning', processes: [], processesStarted: true }) },
  { kind: 'processesStopRunning', nameKey: 'processesStopRunning', configurable: true, disallowDuplicates: false, wpfKey: 'processesStopRunningAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'processesStopRunning', processes: [], processesStarted: false }) },
  { kind: 'userInactivity', nameKey: 'userInactivity', configurable: true, disallowDuplicates: false, wpfKey: 'userInactivityAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'userInactivity', inactivityTimeSpan: '00:01:00' }) },
  { kind: 'userInactivity', nameKey: 'userInactivityZero', configurable: true, disallowDuplicates: false, wpfKey: 'userInactivityAutomationPipelineTriggerDisplayNameZero', createDefault: () => ({ $type: 'userInactivity', inactivityTimeSpan: '00:00:00' }) },
  { kind: 'sessionLock', nameKey: 'sessionLock', configurable: false, disallowDuplicates: true, wpfKey: 'sessionLockAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'sessionLock' }) },
  { kind: 'sessionUnlock', nameKey: 'sessionUnlock', configurable: false, disallowDuplicates: true, wpfKey: 'sessionUnlockAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'sessionUnlock' }) },
  { kind: 'lidOpened', nameKey: 'lidOpened', configurable: false, disallowDuplicates: true, wpfKey: 'lidOpenedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'lidOpened' }) },
  { kind: 'lidClosed', nameKey: 'lidClosed', configurable: false, disallowDuplicates: true, wpfKey: 'lidClosedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'lidClosed' }) },
  { kind: 'displayOn', nameKey: 'displayOn', configurable: false, disallowDuplicates: true, wpfKey: 'displayOnAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'displayOn' }) },
  { kind: 'displayOff', nameKey: 'displayOff', configurable: false, disallowDuplicates: true, wpfKey: 'displayOffAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'displayOff' }) },
  { kind: 'hdrOn', nameKey: 'hdrOn', configurable: false, disallowDuplicates: true, wpfKey: 'hdrOnAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'hdrOn' }) },
  { kind: 'hdrOff', nameKey: 'hdrOff', configurable: false, disallowDuplicates: true, wpfKey: 'hdrOffAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'hdrOff' }) },
  { kind: 'deviceConnected', nameKey: 'deviceConnected', configurable: true, disallowDuplicates: false, wpfKey: 'deviceConnectedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'deviceConnected', instanceIds: [], deviceConnected: true }) },
  { kind: 'deviceDisconnected', nameKey: 'deviceDisconnected', configurable: true, disallowDuplicates: false, wpfKey: 'deviceDisconnectedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'deviceDisconnected', instanceIds: [], deviceConnected: false }) },
  { kind: 'externalDisplayConnected', nameKey: 'externalDisplayConnected', configurable: false, disallowDuplicates: true, wpfKey: 'externalDisplayConnectedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'externalDisplayConnected' }) },
  { kind: 'externalDisplayDisconnected', nameKey: 'externalDisplayDisconnected', configurable: false, disallowDuplicates: true, wpfKey: 'externalDisplayDisconnectedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'externalDisplayDisconnected' }) },
  { kind: 'wiFiConnected', nameKey: 'wiFiConnected', configurable: true, disallowDuplicates: false, wpfKey: 'wiFiConnectedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'wiFiConnected', ssids: [] }) },
  { kind: 'wiFiDisconnected', nameKey: 'wiFiDisconnected', configurable: false, disallowDuplicates: true, wpfKey: 'wiFiDisconnectedAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'wiFiDisconnected' }) },
  { kind: 'time', nameKey: 'time', configurable: true, disallowDuplicates: false, wpfKey: 'timeAutomationPipelineTriggerDisplayName', createDefault: () => {
    const now = new Date()
    return { $type: 'time', isSunrise: false, isSunset: false, time: { hour: now.getHours(), minute: now.getMinutes() }, days: [1, 2, 3, 4, 5, 6, 0] }
  } },
  { kind: 'periodic', nameKey: 'periodic', configurable: true, disallowDuplicates: false, wpfKey: 'periodicActionPipelineTriggerDisplayName', createDefault: () => ({ $type: 'periodic', period: '00:01:00' }) },
  {
    kind: 'hardwareSensor', nameKey: 'hardwareSensor', configurable: true, disallowDuplicates: false, wpfKey: 'hardwareSensorAutomationPipelineTriggerDisplayName',
    createDefault: () => ({ $type: 'hardwareSensor', metric: 'CpuTemperature', comparison: 'GreaterThanOrEqual', threshold: 90, duration: '00:00:05', cooldown: '00:01:00' })
  },
  {
    kind: 'batteryPercentage', nameKey: 'batteryPercentage', configurable: true, disallowDuplicates: false, wpfKey: 'batteryPercentageAutomationPipelineTriggerDisplayName',
    createDefault: () => ({ $type: 'batteryPercentage', comparison: 'BelowOrEqual', threshold: 20, duration: '00:00:05', cooldown: '00:05:00', chargeFilter: 'Any' })
  },
  { kind: 'onStartup', nameKey: 'onStartup', configurable: false, disallowDuplicates: true, wpfKey: 'onStartupAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'onStartup' }) },
  { kind: 'onResume', nameKey: 'onResume', configurable: false, disallowDuplicates: true, wpfKey: 'onResumeAutomationPipelineTriggerDisplayName', createDefault: () => ({ $type: 'onResume' }) },
]

const DEFINITION_BY_KIND = new Map<string, TriggerDefinition[]>()
const DEFINITION_BY_LOWER_KIND = new Map<string, TriggerKind>()

for (const definition of TRIGGER_DEFINITIONS) {
  const list = DEFINITION_BY_KIND.get(definition.kind) ?? []
  list.push(definition)
  DEFINITION_BY_KIND.set(definition.kind, list)
  DEFINITION_BY_LOWER_KIND.set(definition.kind.toLowerCase(), definition.kind)
}

// Add canonical lowercase / common alias mappings
DEFINITION_BY_LOWER_KIND.set('acadapterconnected', 'aCAdapterConnected')
DEFINITION_BY_LOWER_KIND.set('acadapterdisconnected', 'aCAdapterDisconnected')
DEFINITION_BY_LOWER_KIND.set('lowwattageacadapterconnected', 'lowWattageACAdapterConnected')
DEFINITION_BY_LOWER_KIND.set('wificonnected', 'wiFiConnected')
DEFINITION_BY_LOWER_KIND.set('wifidisconnected', 'wiFiDisconnected')

/** All configurable trigger kinds (IAutomationPipelineTriggerTabItemContent families). */
export const CONFIGURABLE_KINDS: TriggerKind[] = [
  'powerMode',
  'godModePresetChanged',
  'periodic',
  'processesAreRunning',
  'processesStopRunning',
  'time',
  'userInactivity',
  'wiFiConnected',
  'hardwareSensor',
  'batteryPercentage',
  'deviceConnected',
  'deviceDisconnected',
]

/** Port of AutomationPipelineTriggerConfigurationWindow.IsValid. */
export function isTriggerValid(trigger: AutomationTrigger): boolean {
  const kind = normalizeTriggerKind(trigger.$type)
  if (kind === null) return false
  switch (kind) {
    case 'periodic':
      return parseTimeSpanSeconds(String(trigger.period ?? '00:00:00')) > 0
    case 'userInactivity':
      return parseTimeSpanSeconds(String(trigger.inactivityTimeSpan ?? '00:00:00')) > 0
    default:
      return CONFIGURABLE_KINDS.includes(kind)
  }
}

/** Normalize a serialized $type to a catalog kind (case-insensitive, suffix tolerant). */
export function normalizeTriggerKind($type: string | undefined | null): TriggerKind | null {
  if (typeof $type !== 'string') return null
  const trimmed = $type
    .replace(/AutomationPipelineTrigger$/i, '')
    .replace(/PipelineTrigger$/i, '')
    .replace(/Trigger$/i, '')
  if (!trimmed) return null
  if (DEFINITION_BY_KIND.has(trimmed)) {
    return trimmed as TriggerKind
  }
  const first = trimmed.charAt(0).toLowerCase() + trimmed.slice(1)
  if (DEFINITION_BY_KIND.has(first)) {
    return first as TriggerKind
  }
  const canonical = DEFINITION_BY_LOWER_KIND.get(trimmed.toLowerCase())
  if (canonical) {
    return canonical
  }
  return null
}

/** True when Configure belongs in the expanded pipeline body (not the collapsed header). */
export function isTriggerConfigurable(trigger: AutomationTrigger): boolean {
  if (trigger.$type === 'and' && Array.isArray(trigger.triggers)) {
    return trigger.triggers.some(
      (child) =>
        typeof child === 'object' &&
        child !== null &&
        typeof (child as { $type?: unknown }).$type === 'string' &&
        isTriggerConfigurable(child as AutomationTrigger)
    )
  }
  const kind = normalizeTriggerKind(trigger.$type)
  if (kind === null) return false
  return (DEFINITION_BY_KIND.get(kind) ?? []).some((definition) => definition.configurable)
}

/** Display name lookup: prefers the exact serialized $type family, else the catalog name. */
export function triggerDisplayNameKey(trigger: AutomationTrigger): string {
  const kind = normalizeTriggerKind(trigger.$type)
  if (kind !== null) {
    const candidates = DEFINITION_BY_KIND.get(kind) ?? []
    if (kind === 'userInactivity' && parseTimeSpanSeconds(String(trigger.inactivityTimeSpan ?? '')) === 0) {
      const zero = candidates.find((d) => d.nameKey === 'userInactivityZero')
      if (zero) return `automation.triggerNames.${zero.nameKey}`
    }
    const exact = candidates[0]
    if (exact) return `automation.triggerNames.${exact.nameKey}`
  }
  return 'automation.triggerNames.powerMode'
}

/** "00:01:00" → 60; ISO 8601 "PT1M" tolerant. */
export function parseTimeSpanSeconds(value: string | number): number {
  if (typeof value === 'number') return value
  if (value.startsWith('PT')) {
    const match = /^PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?$/.exec(value)
    if (!match) return 0
    const hours = match[1] ? Number(match[1]) : 0
    const minutes = match[2] ? Number(match[2]) : 0
    const seconds = match[3] ? Number(match[3]) : 0
    return hours * 3600 + minutes * 60 + seconds
  }
  const parts = String(value).split(':').map(Number)
  if (parts.length !== 3 || parts.some((p) => Number.isNaN(p))) return 0
  return parts[0] * 3600 + parts[1] * 60 + parts[2]
}

/** Seconds → "HH:MM:SS" (System.Text.Json TimeSpan "c" format). */
export function formatTimeSpan(seconds: number): string {
  const clamped = Math.max(0, Math.round(seconds))
  const hours = Math.floor(clamped / 3600)
  const minutes = Math.floor((clamped % 3600) / 60)
  const secs = clamped % 60
  const pad = (n: number): string => n.toString().padStart(2, '0')
  return `${pad(hours)}:${pad(minutes)}:${pad(secs)}`
}

/** Flatten a composite (and) trigger into its children; single triggers return themselves. */
export function flattenTriggers(trigger: AutomationTrigger | null | undefined): AutomationTrigger[] {
  if (trigger == null) return []
  if (trigger.$type === 'and' && Array.isArray(trigger.triggers)) {
    return (trigger.triggers as unknown[]).filter(isAutomationTrigger)
  }
  return [trigger]
}

/** Build a composite trigger from children (matches SaveButton_Click semantics). */
export function composeTriggers(triggers: AutomationTrigger[]): AutomationTrigger | null {
  if (triggers.length === 0) return null
  if (triggers.length === 1) return triggers[0]
  return { $type: 'and', triggers }
}

function isAutomationTrigger(value: unknown): value is AutomationTrigger {
  return typeof value === 'object' && value !== null && typeof (value as { $type?: unknown }).$type === 'string'
}
