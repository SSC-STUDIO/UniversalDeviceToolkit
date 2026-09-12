import { randomBytes } from 'node:crypto'
import { BrowserWindow, globalShortcut, powerMonitor, screen } from 'electron'
import { hostClient } from './host-client'
import { effectiveZoom } from './ui-scale'
import { cancelIdleDestroy, scheduleIdleDestroy, setSurfaceVisible } from './ui-activity'

/**
 * On-screen display (OSD) —port of the Electron OsdWindowBase family:
 *
 * - OsdWindowBase.cs: frameless, transparent, always-on-top window; saved
 *   position restore, edge snapping (SnapThreshold), click-through while
 *   locked, sensor refresh loop (OsdRefreshInterval), severity coloring.
 * - OsdBarWindow.xaml(.cs): horizontal bar at the top center.
 * - OsdPanelWindow.xaml(.cs): vertical panel at the left edge.
 *
 * Style ("Panel" | "Bar"), appearance, thresholds and sensor items come from
 * the "osd" settings scope (osd.json). Position is persisted back into the
 * same scope on drag end. Sensor data is consumed from the shared
 * sensors.updated producer (same loop as the dashboard); FPS uses
 * sensors.subscribeFps (ref-counted in the host). OSD visibility is driven by
 * the host's "osd.changed" events (automation steps) and the showOsd setting.
 */

type OsdState = 'Hidden' | 'Show' | 'Toggle'

interface OsdEventData {
  state: OsdState
}

/** Snapshot projection —mirrors api/sensors.ts SensorSnapshot (camelCase). */
interface OsdSnapshot {
  ts?: string
  isHybrid?: boolean
  cpu?: {
    temperature?: number | null
    usage?: number | null
    fanSpeed?: number | null
    power?: number | null
    coreClockMax?: number | null
    coreClockAvg?: number | null
    pCoreClock?: number | null
    eCoreClock?: number | null
  }
  gpu?: {
    usage?: number | null
    temperature?: number | null
    coreClock?: number | null
    power?: number | null
    vramTemperature?: number | null
    vramUtilization?: number | null
    vramUsedMb?: number | null
    vramTotalMb?: number | null
    fanSpeed?: number | null
  }
  memory?: {
    usage?: number | null
    usedMb?: number | null
    totalMb?: number | null
    highestTemperature?: number | null
  }
  motherboard?: {
    highestTemperature?: number | null
  }
  storage?: {
    temperatures?: (number | null)[]
  }
}

/** FPS projection —mirrors MapFpsData in SensorsHandlers.cs. */
interface OsdFpsData {
  fps?: number | null
  lowFps?: number | null
  frameTimeMs?: number | null
}

/** "osd" settings scope —mirrors OsdSettingsStore in UniversalDeviceToolkit.Lib. */
interface OsdSettingsStore {
  showOsd: boolean
  osdRefreshInterval: number
  selectedStyleIndex: number
  items: string[]
  backgroundOpacity: number
  backgroundColor: string
  fontSize: number
  cornerRadiusTop: number
  cornerRadiusBottom: number
  isLocked: boolean
  panelPositionX: number | null
  panelPositionY: number | null
  barPositionX: number | null
  barPositionY: number | null
  tempThresholdWarning: number
  tempThresholdCritical: number
  usageThresholdWarning: number
  usageThresholdCritical: number
  fpsThresholdCritical: number
  lowFpsDeltaThreshold: number
  categoryColor: string
  labelColor: string
  valueColor: string
  warningColor: string
  criticalColor: string
  separatorColor: string
  snapThreshold: number
}

/** OsdItem enum names (Enums.cs) —persisted verbatim in osd.json. */
const OSD_ITEMS = [
  'Fps',
  'LowFps',
  'FrameTime',
  'CpuFrequency',
  'CpuPCoreFrequency',
  'CpuECoreFrequency',
  'CpuUtilization',
  'CpuTemperature',
  'CpuPower',
  'CpuFan',
  'GpuFrequency',
  'GpuUtilization',
  'GpuTemperature',
  'GpuVramUtilization',
  'GpuVramTemperature',
  'GpuPower',
  'GpuFan',
  'MemoryUtilization',
  'MemoryTemperature',
  'Disk1Temperature',
  'Disk2Temperature',
  'PchTemperature',
  'PchFan'
] as const

type OsdItemName = (typeof OSD_ITEMS)[number]

const DEFAULT_OSD_SETTINGS: OsdSettingsStore = {
  showOsd: false,
  osdRefreshInterval: 1,
  selectedStyleIndex: 0,
  items: [...OSD_ITEMS],
  backgroundOpacity: 0.6,
  backgroundColor: '#1E1E1E',
  fontSize: 12,
  cornerRadiusTop: 6,
  cornerRadiusBottom: 6,
  isLocked: false,
  panelPositionX: null,
  panelPositionY: null,
  barPositionX: null,
  barPositionY: null,
  tempThresholdWarning: 75,
  tempThresholdCritical: 90,
  usageThresholdWarning: 70,
  usageThresholdCritical: 90,
  fpsThresholdCritical: 30,
  lowFpsDeltaThreshold: 30,
  categoryColor: '#2196F3',
  labelColor: '#ADFF2F',
  valueColor: '#FFFFFF',
  warningColor: '#FFFF00',
  criticalColor: '#FF0000',
  separatorColor: '#555555',
  snapThreshold: 20
}

/** OsdItem label map (Resource.en.resx: Osd_*, OsdItem_*, SensorsControl_*). */
const ITEM_LABELS: Record<OsdItemName, string> = {
  Fps: 'FPS',
  LowFps: '1% Low',
  FrameTime: 'Frame Time',
  CpuFrequency: 'Core Clock',
  CpuPCoreFrequency: 'P-Core Clock',
  CpuECoreFrequency: 'E-Core Clock',
  CpuUtilization: 'Utilization',
  CpuTemperature: 'Temperature',
  CpuPower: 'Power',
  CpuFan: 'Fan',
  GpuFrequency: 'Core Clock',
  GpuUtilization: 'Utilization',
  GpuTemperature: 'Core Temp',
  GpuVramUtilization: 'VRAM Utilization',
  GpuVramTemperature: 'VRAM Temp',
  GpuPower: 'Power',
  GpuFan: 'Fan',
  MemoryUtilization: 'Utilization',
  MemoryTemperature: 'Temperature',
  Disk1Temperature: 'Disk 1 Temperature',
  Disk2Temperature: 'Disk 2 Temperature',
  PchTemperature: 'PCH Temperature',
  PchFan: 'Fan'
}

/** Group definitions —mirrors the _measurementGroups of the Electron windows. */
interface OsdGroupDef {
  label: string
  items: OsdItemName[]
}

const PANEL_GROUPS: OsdGroupDef[] = [
  { label: 'FPS', items: ['Fps', 'LowFps', 'FrameTime'] },
  {
    label: 'CPU',
    items: [
      'CpuFrequency',
      'CpuPCoreFrequency',
      'CpuECoreFrequency',
      'CpuUtilization',
      'CpuTemperature',
      'CpuPower',
      'CpuFan'
    ]
  },
  {
    label: 'GPU',
    items: [
      'GpuFrequency',
      'GpuUtilization',
      'GpuTemperature',
      'GpuVramUtilization',
      'GpuVramTemperature',
      'GpuPower',
      'GpuFan'
    ]
  },
  { label: 'RAM', items: ['MemoryUtilization', 'MemoryTemperature'] },
  {
    label: 'PCH',
    items: ['PchTemperature', 'PchFan', 'Disk1Temperature', 'Disk2Temperature']
  }
]

const BAR_GROUPS: OsdGroupDef[] = [
  { label: 'FPS', items: ['Fps', 'LowFps', 'FrameTime'] },
  {
    label: 'CPU',
    items: [
      'CpuFrequency',
      'CpuPCoreFrequency',
      'CpuECoreFrequency',
      'CpuUtilization',
      'CpuTemperature',
      'CpuPower',
      'CpuFan'
    ]
  },
  {
    label: 'GPU',
    items: [
      'GpuFrequency',
      'GpuUtilization',
      'GpuTemperature',
      'GpuVramUtilization',
      'GpuVramTemperature',
      'GpuPower',
      'GpuFan'
    ]
  },
  { label: 'RAM', items: ['MemoryUtilization', 'MemoryTemperature'] },
  {
    label: 'PCH',
    items: ['PchTemperature', 'PchFan', 'Disk1Temperature', 'Disk2Temperature']
  }
]

const OSD_WIDTH = 320
const OSD_HEIGHT = 96

let osdWindow: BrowserWindow | null = null
let unsubscribe: (() => void) | null = null
let unsubscribeSettings: (() => void) | null = null
let unsubscribeFps: (() => void) | null = null
let unsubscribeDisplay: (() => void) | null = null
let unsubscribePower: (() => void) | null = null

let settings: OsdSettingsStore = { ...DEFAULT_OSD_SETTINGS }
let showCpuAverageFrequency = false
let displayMemoryInGigabytes = false
let temperatureUnit: 'C' | 'F' = 'C'

let lastSnapshot: OsdSnapshot | null = null
let lastFps: OsdFpsData | null = null

let visible = false
let pageLoaded = false
let sensorsSubscribed = false
let unsubscribeUpdated: (() => void) | null = null
let fpsSubscribed = false
let positionSaveTimer: ReturnType<typeof setTimeout> | null = null
let lastAppearanceSignature = ''

// ── settings ────────────────────────────────────────────────────────────────

const HEX_COLOR_PATTERN = /^#[0-9A-F]{6}$/i
const POSITION_LIMIT = 100_000

function sanitizeBoolean(value: unknown, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback
}

function sanitizeFiniteNumber(
  value: unknown,
  min: number,
  max: number,
  fallback: number
): number {
  return typeof value === 'number' && Number.isFinite(value)
    ? Math.min(max, Math.max(min, value))
    : fallback
}

function sanitizeInteger(value: unknown, min: number, max: number, fallback: number): number {
  const sanitized = sanitizeFiniteNumber(value, min, max, fallback)
  return Math.round(sanitized)
}

function sanitizePosition(value: unknown): number | null {
  if (value === null) return null
  return typeof value === 'number' && Number.isFinite(value)
    ? Math.min(POSITION_LIMIT, Math.max(-POSITION_LIMIT, value))
    : null
}

function sanitizeColor(value: unknown, fallback: string): string {
  return typeof value === 'string' && value.length === 7 && HEX_COLOR_PATTERN.test(value)
    ? value.toUpperCase()
    : fallback
}

function sanitizeStyleIndex(value: unknown): number {
  return value === 0 || value === 1 || value === 2 ? value : DEFAULT_OSD_SETTINGS.selectedStyleIndex
}

function sanitizeItems(value: unknown): OsdItemName[] {
  if (!Array.isArray(value)) return [...DEFAULT_OSD_SETTINGS.items] as OsdItemName[]
  const validItems = value.filter(
    (item): item is OsdItemName =>
      typeof item === 'string' && (OSD_ITEMS as readonly string[]).includes(item)
  )
  return [...new Set(validItems)]
}

function mergeSettings(value: unknown): void {
  if (!value || typeof value !== 'object') return
  // The host serializes settings stores with their .NET property names
  // (PascalCase); the in-memory model below uses camelCase.
  const raw = value as Record<string, unknown>
  const merged: OsdSettingsStore = { ...DEFAULT_OSD_SETTINGS, ...settings }
  const read = (pascal: string, current: unknown): unknown => {
    return Object.prototype.hasOwnProperty.call(raw, pascal) ? raw[pascal] : current
  }

  merged.showOsd = sanitizeBoolean(
    read('ShowOsd', settings.showOsd),
    DEFAULT_OSD_SETTINGS.showOsd
  )
  merged.osdRefreshInterval = sanitizeFiniteNumber(
    read('OsdRefreshInterval', settings.osdRefreshInterval),
    0.1,
    10,
    DEFAULT_OSD_SETTINGS.osdRefreshInterval
  )
  merged.selectedStyleIndex = sanitizeStyleIndex(
    read('SelectedStyleIndex', settings.selectedStyleIndex)
  )
  merged.items = sanitizeItems(read('Items', settings.items))
  merged.backgroundOpacity = sanitizeFiniteNumber(
    read('BackgroundOpacity', settings.backgroundOpacity),
    0,
    1,
    DEFAULT_OSD_SETTINGS.backgroundOpacity
  )
  merged.backgroundColor = sanitizeColor(
    read('BackgroundColor', settings.backgroundColor),
    DEFAULT_OSD_SETTINGS.backgroundColor
  )
  merged.fontSize = sanitizeInteger(
    read('FontSize', settings.fontSize),
    8,
    24,
    DEFAULT_OSD_SETTINGS.fontSize
  )
  merged.cornerRadiusTop = sanitizeInteger(
    read('CornerRadiusTop', settings.cornerRadiusTop),
    0,
    50,
    DEFAULT_OSD_SETTINGS.cornerRadiusTop
  )
  merged.cornerRadiusBottom = sanitizeInteger(
    read('CornerRadiusBottom', settings.cornerRadiusBottom),
    0,
    50,
    DEFAULT_OSD_SETTINGS.cornerRadiusBottom
  )
  merged.isLocked = sanitizeBoolean(
    read('IsLocked', settings.isLocked),
    DEFAULT_OSD_SETTINGS.isLocked
  )
  merged.panelPositionX = sanitizePosition(read('PanelPositionX', settings.panelPositionX))
  merged.panelPositionY = sanitizePosition(read('PanelPositionY', settings.panelPositionY))
  merged.barPositionX = sanitizePosition(read('BarPositionX', settings.barPositionX))
  merged.barPositionY = sanitizePosition(read('BarPositionY', settings.barPositionY))
  merged.tempThresholdWarning = sanitizeInteger(
    read('TempThresholdWarning', settings.tempThresholdWarning),
    0,
    110,
    DEFAULT_OSD_SETTINGS.tempThresholdWarning
  )
  merged.tempThresholdCritical = sanitizeInteger(
    read('TempThresholdCritical', settings.tempThresholdCritical),
    0,
    110,
    DEFAULT_OSD_SETTINGS.tempThresholdCritical
  )
  merged.usageThresholdWarning = sanitizeInteger(
    read('UsageThresholdWarning', settings.usageThresholdWarning),
    0,
    100,
    DEFAULT_OSD_SETTINGS.usageThresholdWarning
  )
  merged.usageThresholdCritical = sanitizeInteger(
    read('UsageThresholdCritical', settings.usageThresholdCritical),
    0,
    100,
    DEFAULT_OSD_SETTINGS.usageThresholdCritical
  )
  merged.fpsThresholdCritical = sanitizeInteger(
    read('FpsThresholdCritical', settings.fpsThresholdCritical),
    0,
    1000,
    DEFAULT_OSD_SETTINGS.fpsThresholdCritical
  )
  merged.lowFpsDeltaThreshold = sanitizeInteger(
    read('LowFpsDeltaThreshold', settings.lowFpsDeltaThreshold),
    0,
    1000,
    DEFAULT_OSD_SETTINGS.lowFpsDeltaThreshold
  )
  merged.categoryColor = sanitizeColor(
    read('CategoryColor', settings.categoryColor),
    DEFAULT_OSD_SETTINGS.categoryColor
  )
  merged.labelColor = sanitizeColor(
    read('LabelColor', settings.labelColor),
    DEFAULT_OSD_SETTINGS.labelColor
  )
  merged.valueColor = sanitizeColor(
    read('ValueColor', settings.valueColor),
    DEFAULT_OSD_SETTINGS.valueColor
  )
  merged.warningColor = sanitizeColor(
    read('WarningColor', settings.warningColor),
    DEFAULT_OSD_SETTINGS.warningColor
  )
  merged.criticalColor = sanitizeColor(
    read('CriticalColor', settings.criticalColor),
    DEFAULT_OSD_SETTINGS.criticalColor
  )
  merged.separatorColor = sanitizeColor(
    read('SeparatorColor', settings.separatorColor),
    DEFAULT_OSD_SETTINGS.separatorColor
  )
  merged.snapThreshold = sanitizeInteger(
    read('SnapThreshold', settings.snapThreshold),
    0,
    100,
    DEFAULT_OSD_SETTINGS.snapThreshold
  )

  settings = merged
}

/** Convert the camelCase model back to the PascalCase host store shape. */
function toHostStore(store: OsdSettingsStore): Record<string, unknown> {
  const pascal = (key: string): string => key.charAt(0).toUpperCase() + key.slice(1)
  const result: Record<string, unknown> = {}
  for (const [key, value] of Object.entries(store)) {
    result[pascal(key)] = value
  }
  return result
}

async function readSettings(): Promise<void> {
  try {
    const result = (await hostClient.invoke('settings.get', { scope: 'osd' })) as
      | { value?: unknown }
      | null
      | undefined
    mergeSettings(result?.value)
  } catch (error) {
    console.error('[osd] failed to read settings:', error)
  }
}

/** Retry the initial read —the host may still be starting up. */
async function readSettingsWithRetry(attempts = 10): Promise<void> {
  for (let attempt = 0; attempt < attempts; attempt++) {
    try {
      const result = (await hostClient.invoke('settings.get', { scope: 'osd' })) as
        | { value?: unknown }
        | null
        | undefined
      mergeSettings(result?.value)
      return
    } catch {
      await new Promise((resolve) => setTimeout(resolve, 500))
    }
  }
}

async function readSiblingSettings(): Promise<void> {
  try {
    const [hardware, application] = (await Promise.all([
      hostClient.invoke('settings.get', { scope: 'hardwareSensors' }),
      hostClient.invoke('settings.get', { scope: 'application' })
    ])) as [{ value?: Record<string, unknown> }, { value?: Record<string, unknown> }]
    const hardwareRaw = (hardware.value ?? {}) as Record<string, unknown>
    const applicationRaw = (application.value ?? {}) as Record<string, unknown>
    showCpuAverageFrequency = hardwareRaw['ShowCpuAverageFrequency'] === true
    displayMemoryInGigabytes = hardwareRaw['DisplayMemoryInGigabytes'] === true
    const unit = applicationRaw['TemperatureUnit']
    temperatureUnit = unit === 'F' ? 'F' : 'C'
  } catch {
    // Keep defaults when the host is not reachable yet.
  }
}

/** Persist the whole in-memory store; settings.set replaces the full scope. */
async function writeSettings(): Promise<void> {
  try {
    await hostClient.invoke('settings.set', { scope: 'osd', value: toHostStore(settings) })
    await hostClient.invoke('settings.save', { scopes: ['osd'] })
  } catch (error) {
    console.error('[osd] failed to save settings:', error)
  }
}

// ── window lifecycle ────────────────────────────────────────────────────────

function isMiniStyle(): boolean {
  return settings.selectedStyleIndex === 2
}

function isBarStyle(): boolean {
  return settings.selectedStyleIndex === 1
}

function savedPosition(): { x: number | null; y: number | null } {
  return isBarStyle() || isMiniStyle()
    ? { x: settings.barPositionX, y: settings.barPositionY }
    : { x: settings.panelPositionX, y: settings.panelPositionY }
}

function savePosition(x: number, y: number): void {
  if (isBarStyle() || isMiniStyle()) {
    settings.barPositionX = x
    settings.barPositionY = y
  } else {
    settings.panelPositionX = x
    settings.panelPositionY = y
  }
  if (positionSaveTimer) clearTimeout(positionSaveTimer)
  positionSaveTimer = setTimeout(() => {
    positionSaveTimer = null
    void writeSettings()
  }, 400)
}

/** Overlap with a work area below this is treated as unreachable by the user. */
const MIN_VISIBLE_PX = 24

/**
 * screen.getDisplayMatching always resolves to the nearest display and never
 * throws, so it cannot answer "is this rectangle still on a monitor?" — the
 * work areas have to be intersected explicitly. Without this the OSD keeps a
 * position that belonged to an unplugged or rescaled monitor and stays
 * invisible.
 */
function rectOnScreen(x: number, y: number, width: number, height: number): boolean {
  const minWidth = Math.min(MIN_VISIBLE_PX, width)
  const minHeight = Math.min(MIN_VISIBLE_PX, height)
  return screen.getAllDisplays().some((display) => {
    const area = display.workArea
    const overlapWidth = Math.min(x + width, area.x + area.width) - Math.max(x, area.x)
    const overlapHeight = Math.min(y + height, area.y + area.height) - Math.max(y, area.y)
    return overlapWidth >= minWidth && overlapHeight >= minHeight
  })
}

function isPositionOnScreen(x: number, y: number): boolean {
  const [width, height] = osdWindow && !osdWindow.isDestroyed() ? osdWindow.getSize() : [100, 30]
  return rectOnScreen(x, y, width, height)
}

function setDefaultWindowPosition(): void {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  const { workArea } = screen.getPrimaryDisplay()
  const [width, height] = win.getSize()
  if (isBarStyle() || isMiniStyle()) {
    win.setPosition(
      Math.round(workArea.x + (workArea.width - width) / 2),
      Math.round(workArea.y + (isMiniStyle() ? 10 : 0))
    )
  } else {
    win.setPosition(
      Math.round(workArea.x),
      Math.round(workArea.y + (workArea.height - height) / 2)
    )
  }
}

function setWindowPosition(): void {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  const saved = savedPosition()
  if (saved.x !== null && saved.y !== null && isPositionOnScreen(saved.x, saved.y)) {
    win.setPosition(Math.round(saved.x), Math.round(saved.y))
    return
  }
  setDefaultWindowPosition()
}

/** Electron OnMouseLeftButtonDown snapping + clamping against the work area. */
function snapAndClampPosition(): void {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  const [x, y] = win.getPosition()
  const [width, height] = win.getSize()
  const { workArea } = screen.getDisplayMatching({ x, y, width, height })
  const threshold = Math.max(0, settings.snapThreshold)

  let left = x
  let top = y
  if (Math.abs(left - workArea.x) < threshold) left = workArea.x
  else if (Math.abs(workArea.x + workArea.width - (left + width)) < threshold) {
    left = workArea.x + workArea.width - width
  }

  if (Math.abs(top - workArea.y) < threshold) top = workArea.y
  else if (Math.abs(workArea.y + workArea.height - (top + height)) < threshold) {
    top = workArea.y + workArea.height - height
  }

  left = Math.min(Math.max(left, workArea.x), workArea.x + workArea.width - width)
  top = Math.min(Math.max(top, workArea.y), workArea.y + workArea.height - height)

  if (left !== x || top !== y) win.setPosition(Math.round(left), Math.round(top))
  savePosition(Math.round(left), Math.round(top))
}

function onDisplayMetricsChanged(): void {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  const [x, y] = win.getPosition()
  if (!isPositionOnScreen(x, y)) setDefaultWindowPosition()
}

// ── data refresh ────────────────────────────────────────────────────────────

const FPS_ITEMS: OsdItemName[] = ['Fps', 'LowFps', 'FrameTime']

function fpsItemsActive(): boolean {
  return FPS_ITEMS.some((item) => settings.items.includes(item))
}

function updateFpsSubscription(): void {
  const shouldSubscribe = visible && fpsItemsActive()
  if (shouldSubscribe && !fpsSubscribed) {
    fpsSubscribed = true
    unsubscribeFps = hostClient.on('sensors.fpsUpdated', (data) => {
      if (!visible) return
      lastFps = (data ?? null) as OsdFpsData | null
      updateValues()
    })
    void hostClient.invoke('sensors.subscribeFps', {}).catch((error) => {
      console.error('[osd] failed to subscribe FPS:', error)
    })
  } else if (!shouldSubscribe && fpsSubscribed) {
    fpsSubscribed = false
    unsubscribeFps?.()
    unsubscribeFps = null
    void hostClient.invoke('sensors.unsubscribeFps', {}).catch(() => undefined)
  }
}

function startRefresh(): void {
  if (!sensorsSubscribed) {
    sensorsSubscribed = true
    unsubscribeUpdated = hostClient.on('sensors.updated', (data) => {
      if (!visible) return
      lastSnapshot = (data ?? null) as OsdSnapshot | null
      updateValues()
    })
    void hostClient
      .invoke('sensors.subscribe', {
        intervalSec: Math.max(0.5, settings.osdRefreshInterval),
        subscriberId: 'osd'
      })
      .catch((error) => {
        console.error('[osd] failed to subscribe sensors:', error)
      })
    void hostClient
      .invoke('sensors.getSnapshot', {})
      .then((snapshot) => {
        if (!visible || snapshot == null) return
        lastSnapshot = snapshot as OsdSnapshot
        updateValues()
      })
      .catch(() => undefined)
  }
  updateFpsSubscription()
}

function stopRefresh(): void {
  if (sensorsSubscribed) {
    sensorsSubscribed = false
    unsubscribeUpdated?.()
    unsubscribeUpdated = null
    void hostClient.invoke('sensors.unsubscribe', { subscriberId: 'osd' }).catch(() => undefined)
  }
}

// ── rendering ───────────────────────────────────────────────────────────────

function isHybrid(): boolean {
  return lastSnapshot?.isHybrid === true
}

/** Electron UpdateMeasurementControlsVisibility: hybrid CPUs show P/E cores only. */
function isItemVisible(item: OsdItemName): boolean {
  if (!settings.items.includes(item)) return false
  if (isHybrid()) {
    if (item === 'CpuFrequency') return false
  } else if (item === 'CpuPCoreFrequency' || item === 'CpuECoreFrequency') {
    return false
  }
  return true
}

function groupVisible(group: OsdGroupDef): boolean {
  return group.items.some(isItemVisible)
}

interface ValueRender {
  text: string
  color: string
}

function severityColor(value: number, warning: number, critical: number, base: string): string {
  if (value >= critical) return settings.criticalColor
  return value >= warning ? settings.warningColor : base
}

function dash(): ValueRender {
  return { text: '-', color: settings.valueColor }
}

function formatTemperature(raw: number): string {
  if (temperatureUnit === 'F') {
    return `${(raw * 9) / 5 + 32}°F`
  }
  return `${raw}°C`
}

function temperatureValue(raw: number | null | undefined): ValueRender {
  if (typeof raw !== 'number' || Number.isNaN(raw) || raw < 0) return dash()
  return {
    text: formatTemperature(raw),
    color: severityColor(
      raw,
      settings.tempThresholdWarning,
      settings.tempThresholdCritical,
      settings.valueColor
    )
  }
}

function usageValue(raw: number | null | undefined): ValueRender {
  if (typeof raw !== 'number' || Number.isNaN(raw) || raw < 0) return dash()
  return {
    text: `${raw.toFixed(0)}%`,
    color: severityColor(
      raw,
      settings.usageThresholdWarning,
      settings.usageThresholdCritical,
      settings.valueColor
    )
  }
}

function frequencyValue(raw: number | null | undefined): ValueRender {
  if (typeof raw !== 'number' || Number.isNaN(raw) || raw < 0) return dash()
  return { text: `${raw.toFixed(0)} MHz`, color: settings.valueColor }
}

function powerValue(raw: number | null | undefined): ValueRender {
  if (typeof raw !== 'number' || Number.isNaN(raw) || raw < 0) return dash()
  return { text: `${raw.toFixed(1)} W`, color: settings.valueColor }
}

function fanValue(raw: number | null | undefined): ValueRender {
  if (typeof raw !== 'number' || Number.isNaN(raw) || raw < 0) return dash()
  return { text: `${raw.toFixed(0)} RPM`, color: settings.valueColor }
}

/** Electron GetMemoryDisplayText: GB when enabled, otherwise percent. */
function memoryValue(
  usage: number | null | undefined,
  used: number | null | undefined,
  total: number | null | undefined
): ValueRender {
  const severity = severityColor(
    usage ?? -1,
    settings.usageThresholdWarning,
    settings.usageThresholdCritical,
    settings.valueColor
  )
  if (displayMemoryInGigabytes) {
    const usedGb = typeof used === 'number' && Number.isFinite(used) && used >= 0 ? used / 1024 : null
    const totalGb = typeof total === 'number' && Number.isFinite(total) && total > 0 ? total / 1024 : null
    if (usedGb != null && totalGb != null) {
      return { text: `${usedGb.toFixed(1)}/${totalGb.toFixed(1)} GB`, color: severity }
    }
    if (usedGb != null) {
      return { text: `${usedGb.toFixed(1)} GB`, color: severity }
    }
    return dash()
  }
  return usageValue(usage)
}

function fpsValue(): ValueRender {
  const fps = lastFps?.fps
  if (typeof fps !== 'number' || !Number.isFinite(fps) || fps <= 0) return dash()
  const text = `${Math.round(fps)}`
  const color = fps < settings.fpsThresholdCritical ? settings.criticalColor : settings.valueColor
  return { text, color }
}

function lowFpsValue(): ValueRender {
  const low = lastFps?.lowFps
  if (typeof low !== 'number' || !Number.isFinite(low) || low <= 0) return dash()
  const fps = lastFps?.fps
  const delta = typeof fps === 'number' && fps >= 0 ? fps - low : 0
  const color =
    delta >= settings.lowFpsDeltaThreshold ? settings.criticalColor : settings.valueColor
  return { text: `${Math.round(low)}`, color }
}

function frameTimeValue(): ValueRender {
  const ft = lastFps?.frameTimeMs
  if (typeof ft !== 'number' || !Number.isFinite(ft) || ft <= 0.1) return dash()
  const text = `${ft.toFixed(1)}ms`
  const color = ft > 10 ? settings.criticalColor : settings.valueColor
  return { text, color }
}

function renderItem(item: OsdItemName): ValueRender {
  const cpu = lastSnapshot?.cpu
  const gpu = lastSnapshot?.gpu
  const memory = lastSnapshot?.memory
  const motherboard = lastSnapshot?.motherboard
  const storage = lastSnapshot?.storage
  switch (item) {
    case 'Fps':
      return fpsValue()
    case 'LowFps':
      return lowFpsValue()
    case 'FrameTime':
      return frameTimeValue()
    case 'CpuFrequency': {
      const raw = showCpuAverageFrequency ? cpu?.coreClockAvg : cpu?.coreClockMax
      return frequencyValue(typeof raw === 'number' && raw >= 0 ? raw : (cpu?.coreClockAvg ?? cpu?.coreClockMax))
    }
    case 'CpuPCoreFrequency':
      return frequencyValue(cpu?.pCoreClock)
    case 'CpuECoreFrequency':
      return frequencyValue(cpu?.eCoreClock)
    case 'CpuUtilization':
      return usageValue(cpu?.usage)
    case 'CpuTemperature':
      return temperatureValue(cpu?.temperature)
    case 'CpuPower':
      return powerValue(cpu?.power)
    case 'CpuFan':
      return fanValue(cpu?.fanSpeed)
    case 'GpuFrequency':
      return frequencyValue(gpu?.coreClock)
    case 'GpuUtilization':
      return usageValue(gpu?.usage)
    case 'GpuTemperature':
      return temperatureValue(gpu?.temperature)
    case 'GpuVramUtilization':
      return memoryValue(gpu?.vramUtilization, gpu?.vramUsedMb, gpu?.vramTotalMb)
    case 'GpuVramTemperature':
      return temperatureValue(gpu?.vramTemperature)
    case 'GpuPower':
      return powerValue(gpu?.power)
    case 'GpuFan':
      return fanValue(gpu?.fanSpeed)
    case 'MemoryUtilization':
      return memoryValue(memory?.usage, memory?.usedMb, memory?.totalMb)
    case 'MemoryTemperature':
      return temperatureValue(memory?.highestTemperature)
    case 'Disk1Temperature':
      return temperatureValue(storage?.temperatures?.[0])
    case 'Disk2Temperature':
      return temperatureValue(storage?.temperatures?.[1])
    case 'PchTemperature':
      return temperatureValue(motherboard?.highestTemperature)
    case 'PchFan':
      return dash()
  }
}

type OsdLayout = 'bar' | 'panel' | 'mini'

interface OsdRenderItem {
  key: OsdItemName
  label: string
  text: string
  color: string
}

interface OsdRenderGroup {
  key: string
  label: string
  items: OsdRenderItem[]
}

interface OsdRenderModel {
  layout: OsdLayout
  structure: string
  appearance: {
    isLocked: boolean
    backgroundOpacity: number
    backgroundColor: string
    fontSize: number
    cornerRadiusTop: number
    cornerRadiusBottom: number
    categoryColor: string
    labelColor: string
    separatorColor: string
  }
  groups: OsdRenderGroup[]
}

function buildRenderModel(): OsdRenderModel {
  const layout: OsdLayout = isMiniStyle() ? 'mini' : isBarStyle() ? 'bar' : 'panel'
  const groupDefinitions = layout === 'panel' ? PANEL_GROUPS : BAR_GROUPS
  const groups = groupDefinitions.filter(groupVisible).map((group) => ({
    key: group.label,
    label: group.label,
    items: group.items.filter(isItemVisible).map((item) => ({
      key: item,
      label: ITEM_LABELS[item],
      ...renderItem(item)
    }))
  }))
  const structure = `${layout}:${groups
    .map((group) => `${group.key}:${group.items.map((item) => item.key).join(',')}`)
    .join('|')}`

  return {
    layout,
    structure,
    appearance: {
      isLocked: settings.isLocked,
      backgroundOpacity: settings.backgroundOpacity,
      backgroundColor: settings.backgroundColor,
      fontSize: settings.fontSize,
      cornerRadiusTop: settings.cornerRadiusTop,
      cornerRadiusBottom: settings.cornerRadiusBottom,
      categoryColor: settings.categoryColor,
      labelColor: settings.labelColor,
      separatorColor: settings.separatorColor
    },
    groups
  }
}

const OSD_DOCUMENT_STYLE = [
  'html,body{margin:0;padding:0;background:transparent;overflow:hidden;',
  'font-family:"Segoe UI",-apple-system,"Noto Sans",system-ui,sans-serif;user-select:none;cursor:default;}',
  'body.osd-body--draggable{-webkit-app-region:drag;}',
  '.osd-root--bar{display:flex;align-items:center;white-space:nowrap;padding:4px 10px;}',
  '.osd-bar-label{font-weight:500;margin-right:8px;min-width:25px;text-align:center;}',
  '.osd-bar-value{display:inline-block;min-width:34px;margin-right:8px;text-align:center;}',
  '.osd-bar-separator{width:1px;height:12px;margin:0 10px;}',
  '.osd-root--panel{min-width:220px;padding:15px;}',
  '.osd-panel-header{margin-bottom:5px;font-weight:500;}',
  '.osd-row{display:flex;align-items:center;justify-content:space-between;margin:3px 0 1px;}',
  '.osd-label{margin-right:16px;}',
  '.osd-value{text-align:right;}',
  '.osd-panel-separator{height:1px;margin:8px 0;}',
  '.osd-panel-separator--clear{height:8px;margin:0;}',
  '.osd-root--mini{display:flex;align-items:center;white-space:nowrap;padding:2px 8px;font-family:Consolas,"Segoe UI",monospace;}',
  '.osd-mini-badge{display:inline-flex;align-items:center;padding:1px 5px;margin-right:6px;background:rgba(255,255,255,0.06);border-radius:3px;}',
  '.osd-mini-label{font-weight:700;font-size:0.85em;margin-right:4px;letter-spacing:0.5px;}',
  '.osd-mini-value{display:inline-block;min-width:28px;text-align:center;font-weight:600;}',
  '.osd-mini-dot{width:3px;height:3px;border-radius:50%;margin:0 5px;opacity:0.3;display:inline-block;background:currentColor;}'
].join('')

const OSD_RENDERER_SCRIPT = `(() => {
  'use strict'
  const root = document.getElementById('udt-root')
  if (!root) return

  let structure = ''
  let categoryNodes = []
  let labelNodes = []
  let separatorNodes = []
  const valueNodes = new Map()

  const createElement = (tagName, className) => {
    const element = document.createElement(tagName)
    element.className = className
    return element
  }

  const rebuild = (model) => {
    const fragment = document.createDocumentFragment()
    categoryNodes = []
    labelNodes = []
    separatorNodes = []
    valueNodes.clear()
    root.className = model.layout === 'mini'
      ? 'osd-root osd-root--mini'
      : model.layout === 'bar'
      ? 'osd-root osd-root--bar'
      : 'osd-root osd-root--panel'

    if (model.layout === 'mini') {
      model.groups.forEach((group, groupIndex) => {
        if (groupIndex > 0) {
          const dot = createElement('span', 'osd-mini-dot')
          separatorNodes.push(dot)
          fragment.append(dot)
        }

        const badge = createElement('span', 'osd-mini-badge')
        const groupLabel = createElement('span', 'osd-mini-label')
        groupLabel.textContent = group.label
        categoryNodes.push(groupLabel)
        badge.append(groupLabel)

        group.items.forEach((item) => {
          const value = createElement('span', 'osd-mini-value')
          valueNodes.set(item.key, value)
          badge.append(value)
        })
        fragment.append(badge)
      })
    } else if (model.layout === 'bar') {
      model.groups.forEach((group, groupIndex) => {
        if (groupIndex > 0) {
          const separator = createElement('span', 'osd-bar-separator')
          separatorNodes.push(separator)
          fragment.append(separator)
        }

        const groupLabel = createElement('span', 'osd-bar-label')
        groupLabel.textContent = group.label
        categoryNodes.push(groupLabel)
        fragment.append(groupLabel)

        group.items.forEach((item) => {
          const value = createElement('span', 'osd-bar-value')
          valueNodes.set(item.key, value)
          fragment.append(value)
        })
      })
    } else {
      model.groups.forEach((group, groupIndex) => {
        if (groupIndex > 0) {
          const separator = createElement(
            'div',
            groupIndex === 1
              ? 'osd-panel-separator osd-panel-separator--clear'
              : 'osd-panel-separator'
          )
          if (groupIndex > 1) separatorNodes.push(separator)
          fragment.append(separator)
        }

        const groupElement = createElement('div', 'osd-panel-group')
        const groupHeader = createElement('div', 'osd-panel-header')
        groupHeader.textContent = '—' + group.label + ' —'
        categoryNodes.push(groupHeader)
        groupElement.append(groupHeader)

        group.items.forEach((item) => {
          const row = createElement('div', 'osd-row')
          const label = createElement('span', 'osd-label')
          label.textContent = item.label
          labelNodes.push(label)
          row.append(label)

          const value = createElement('span', 'osd-value')
          valueNodes.set(item.key, value)
          row.append(value)
          groupElement.append(row)
        })

        fragment.append(groupElement)
      })
    }

    root.replaceChildren(fragment)
    structure = model.structure
  }

  const backgroundColor = (hex, opacity) => {
    const parsed = Number.parseInt(hex.slice(1), 16)
    const red = (parsed >> 16) & 0xff
    const green = (parsed >> 8) & 0xff
    const blue = parsed & 0xff
    return 'rgba(' + red + ',' + green + ',' + blue + ',' + opacity.toFixed(3) + ')'
  }

  const applyAppearance = (model) => {
    const appearance = model.appearance
    const opacityFactor = model.layout === 'mini' ? 0.75 : model.layout === 'bar' ? 0.8 : 1
    const opacity = Math.min(1, Math.max(0, appearance.backgroundOpacity * opacityFactor))
    const radius =
      appearance.cornerRadiusTop + 'px ' +
      appearance.cornerRadiusTop + 'px ' +
      appearance.cornerRadiusBottom + 'px ' +
      appearance.cornerRadiusBottom + 'px'
    const smallFontSize = Math.max(8, appearance.fontSize - 1) + 'px'

    document.body.classList.toggle('osd-body--draggable', !appearance.isLocked)
    root.style.backgroundColor = backgroundColor(appearance.backgroundColor, opacity)
    root.style.borderRadius = radius
    root.style.fontSize = appearance.fontSize + 'px'

    categoryNodes.forEach((node) => {
      node.style.color = appearance.categoryColor
      node.style.fontSize = model.layout === 'panel' ? smallFontSize : ''
    })
    labelNodes.forEach((node) => {
      node.style.color = appearance.labelColor
      node.style.fontSize = smallFontSize
    })
    separatorNodes.forEach((node) => {
      node.style.backgroundColor = appearance.separatorColor
    })
    valueNodes.forEach((node) => {
      node.style.fontSize = model.layout === 'panel' ? appearance.fontSize + 1 + 'px' : ''
    })
  }

  const updateValues = (model) => {
    model.groups.forEach((group) => {
      group.items.forEach((item) => {
        const node = valueNodes.get(item.key)
        if (!node) return
        node.textContent = item.text
        node.style.color = item.color
      })
    })
  }

  globalThis.udtRender = (model) => {
    if (structure !== model.structure) rebuild(model)
    applyAppearance(model)
    updateValues(model)
    return [document.body.scrollWidth, document.body.scrollHeight]
  }
})()`

function buildOsdUrl(): string {
  const nonce = randomBytes(16).toString('base64')
  const csp = [
    "default-src 'none'",
    `script-src 'nonce-${nonce}'`,
    "style-src 'unsafe-inline'",
    "img-src 'none'",
    "font-src 'none'",
    "connect-src 'none'",
    "media-src 'none'",
    "object-src 'none'",
    "frame-src 'none'",
    "worker-src 'none'",
    "manifest-src 'none'",
    "base-uri 'none'",
    "form-action 'none'"
  ].join('; ')
  const html = [
    '<!DOCTYPE html>',
    '<html>',
    '<head>',
    '<meta charset="utf-8">',
    `<meta http-equiv="Content-Security-Policy" content="${csp}">`,
    '<style>',
    OSD_DOCUMENT_STYLE,
    '</style>',
    '</head>',
    '<body><div id="udt-root"></div>',
    `<script nonce="${nonce}">`,
    OSD_RENDERER_SCRIPT,
    '</script>',
    '</body>',
    '</html>'
  ].join('')
  return `data:text/html;charset=utf-8,${encodeURIComponent(html)}`
}

type ContentSize = [number, number]

function isContentSize(value: unknown): value is ContentSize {
  return (
    Array.isArray(value) &&
    value.length === 2 &&
    typeof value[0] === 'number' &&
    Number.isFinite(value[0]) &&
    typeof value[1] === 'number' &&
    Number.isFinite(value[1])
  )
}

function resizeToContent(win: BrowserWindow, size: ContentSize): void {
  // scrollWidth/Height are CSS px; the window is sized in DIPs, so convert
  // through the shared zoom factor (ui-scale.ts applies it to every surface).
  const zoom = effectiveZoom()
  win.setSize(
    Math.max(1, Math.round(size[0] * zoom)),
    Math.max(1, Math.round(size[1] * zoom))
  )
}

async function fitToContent(): Promise<void> {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  try {
    const size: unknown = await win.webContents.executeJavaScript(
      '[document.body.scrollWidth, document.body.scrollHeight]'
    )
    if (win.isDestroyed() || !isContentSize(size)) return
    resizeToContent(win, size)
  } catch {
    // Page not ready yet.
  }
}

function updateValues(): void {
  const win = osdWindow
  if (!win || win.isDestroyed() || !visible) return
  const serializedModel = encodeURIComponent(JSON.stringify(buildRenderModel()))
  const renderExpression =
    `globalThis.udtRender(JSON.parse(decodeURIComponent(${JSON.stringify(serializedModel)})))`
  void win.webContents
    .executeJavaScript(renderExpression)
    .then((size: unknown) => {
      if (!win.isDestroyed() && isContentSize(size)) resizeToContent(win, size)
    })
    .catch(() => undefined)
}

async function applyAppearance(): Promise<void> {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  try {
    await win.loadURL(buildOsdUrl())
  } catch (error) {
    console.error('[osd] failed to load OSD page:', error)
  }
  if (win.isDestroyed()) return
  pageLoaded = true
  await fitToContent()
  win.setIgnoreMouseEvents(settings.isLocked)
  if (visible) updateValues()
}

/** Fields that require a full page rebuild when they change. */
function appearanceSignature(store: OsdSettingsStore): string {
  return [
    store.selectedStyleIndex,
    store.backgroundOpacity,
    store.backgroundColor,
    store.fontSize,
    store.cornerRadiusTop,
    store.cornerRadiusBottom,
    store.isLocked,
    store.categoryColor,
    store.labelColor,
    store.valueColor,
    store.warningColor,
    store.criticalColor,
    store.separatorColor
  ].join('|')
}

/** Electron ApplyAppearanceSettings + RecalculatePosition on settings change. */
function onSettingsChanged(data: unknown): void {
  const changed = (data as { scope?: string; reason?: string } | null)?.scope
  if (changed === 'osd') {
    void readSettings().then(() => {
      const win = osdWindow
      if (!win || win.isDestroyed()) return

      const signature = appearanceSignature(settings)
      if (signature !== lastAppearanceSignature) {
        lastAppearanceSignature = signature
        void applyAppearance()
      } else if (visible) {
        updateValues()
      }

      const resetRequested =
        (isBarStyle() && settings.barPositionX === null && settings.barPositionY === null) ||
        (!isBarStyle() && settings.panelPositionX === null && settings.panelPositionY === null)
      if (resetRequested) setDefaultWindowPosition()

      if (settings.showOsd && !visible) {
        showOsd()
      } else if (!settings.showOsd && visible) {
        hideOsd()
      }
    })
  } else if (changed === 'hardwareSensors' || changed === 'application') {
    void readSiblingSettings().then(() => {
      if (visible) updateValues()
    })
  }
}

// ── visibility ──────────────────────────────────────────────────────────────

function showOsd(): void {
  // Lazy creation: the window is built on first show, not at startup.
  cancelIdleDestroy('osd')
  ensureOsdWindow()
  const win = osdWindow
  if (!win || win.isDestroyed()) return

  const apply = (): void => {
    if (win.isDestroyed()) return
    if (win.isVisible()) return
    setWindowPosition()
    win.show()
    visible = true
    setSurfaceVisible('osd', true)
    settings.showOsd = true
    void writeSettings()
    startRefresh()
  }

  if (!pageLoaded) {
    void applyAppearance().then(apply)
  } else {
    apply()
  }
}

function hideOsd(persistPreference = true): void {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  if (win.isVisible()) {
    win.hide()
  }
  visible = false
  setSurfaceVisible('osd', false)
  if (persistPreference) {
    settings.showOsd = false
    void writeSettings()
  }
  stopRefresh()
  if (fpsSubscribed) {
    fpsSubscribed = false
    unsubscribeFps?.()
    unsubscribeFps = null
    void hostClient.invoke('sensors.unsubscribeFps', {}).catch(() => undefined)
  }
  scheduleIdleDestroy('osd', releaseOsdWindow)
}

function handleOsdChanged(data: unknown): void {
  const state = (data as OsdEventData | null)?.state
  if (state === 'Hidden') {
    hideOsd()
  } else if (state === 'Toggle') {
    if (osdWindow?.isVisible()) {
      hideOsd()
    } else {
      showOsd()
    }
  } else if (state === 'Show') {
    showOsd()
  }
}

// ── public API ──────────────────────────────────────────────────────────────

export function toggleOsd(): void {
  if (visible && osdWindow && !osdWindow.isDestroyed() && osdWindow.isVisible()) {
    hideOsd(true)
  } else {
    showOsd()
  }
}

export function initOsdWindow(): void {
  // Lazy window creation: registering the subscriptions is cheap (no
  // renderer process), the BrowserWindow itself is only created when the OSD
  // actually needs to show (showOsd setting or osd.changed event). Each OSD
  // window costs a renderer process (~60-90MB), so never build it at startup.
  if (osdWindow && !osdWindow.isDestroyed()) return

  try {
    if (!globalShortcut.isRegistered('CommandOrControl+Shift+O')) {
      globalShortcut.register('CommandOrControl+Shift+O', () => {
        toggleOsd()
      })
    }
  } catch {
    // best-effort global shortcut registration
  }

  if (!unsubscribe) {
    unsubscribe = hostClient.on('osd.changed', handleOsdChanged)
  }
  if (!unsubscribeSettings) {
    unsubscribeSettings = hostClient.on('settings.changed', onSettingsChanged)
  }
  if (!unsubscribeDisplay) {
    const listener = (): void => onDisplayMetricsChanged()
    screen.on('display-metrics-changed', listener)
    unsubscribeDisplay = () => screen.removeListener('display-metrics-changed', listener)
  }
  // Electron OsdWindowBase listened to SystemEvents.PowerModeChanged: hide the OSD
  // while the machine suspends so it never stays pinned over the lock screen.
  // Transient only — do not persist showOsd=false, or resume can never restore it.
  if (!unsubscribePower) {
    const onSuspend = (): void => hideOsd(false)
    const onResume = (): void => {
      if (settings.showOsd) showOsd()
    }
    powerMonitor.on('suspend', onSuspend)
    powerMonitor.on('resume', onResume)
    unsubscribePower = () => {
      powerMonitor.removeListener('suspend', onSuspend)
      powerMonitor.removeListener('resume', onResume)
    }
  }

  // If the persisted setting enables the OSD, create + show it on startup.
  void readSettingsWithRetry().then(() => {
    void readSiblingSettings().then(() => {
      if (settings.showOsd) {
        showOsd()
      }
    })
  })
}

function ensureOsdWindow(): void {
  if (osdWindow && !osdWindow.isDestroyed()) return

  osdWindow = new BrowserWindow({
    width: OSD_WIDTH,
    height: OSD_HEIGHT,
    show: false,
    frame: false,
    transparent: true,
    backgroundColor: '#00000000',
    // Always-on-top + skip-taskbar is the OSD contract on every platform:
    // Linux pins it above normal windows without an entry in the taskbar/dock
    // (some WMs also honor it as an override-redirect-style float); Windows
    // keeps it above apps and out of Alt+Tab. macOS keeps it above windows on
    // the active Space (see setVisibleOnAllWorkspaces below).
    alwaysOnTop: true,
    skipTaskbar: true,
    resizable: false,
    focusable: false,
    hasShadow: false,
    webPreferences: {
      sandbox: true,
      contextIsolation: true,
      nodeIntegration: false,
      backgroundThrottling: true
    }
  })

  // macOS limitation: macOS has no overlay/coverage API for third-party apps,
  // so the OSD cannot be drawn above a game running in true fullscreen
  // (exclusive display capture) — the game will simply cover it. macOS Spaces
  // "Full Screen" is handled by setVisibleOnAllWorkspaces below; only direct
  // display-grabbing fullscreen (e.g. games) defeats the OSD, same as the
  // Windows client's limitation on exclusive-fullscreen games.

  // macOS: Mission Control Spaces would hide the OSD when the user switches
  // desktops; pin it to every Space so it behaves like the Windows always-on-top
  // OSD. Older macOS may reject the call — the OSD still works on the active Space.
  if (process.platform === 'darwin') {
    try {
      osdWindow.setVisibleOnAllWorkspaces(true)
    } catch {
      // ignore — visible-on-current-Space fallback
    }
  }

  osdWindow.on('closed', () => {
    osdWindow = null
    pageLoaded = false
    visible = false
    setSurfaceVisible('osd', false)
    stopRefresh()
  })

  osdWindow.on('moved', () => {
    if (visible) snapAndClampPosition()
  })
}

export function isOsdVisible(): boolean {
  return visible && osdWindow != null && !osdWindow.isDestroyed() && osdWindow.isVisible()
}

function releaseOsdWindow(): void {
  stopRefresh()
  if (fpsSubscribed) {
    fpsSubscribed = false
    unsubscribeFps?.()
    unsubscribeFps = null
    void hostClient.invoke('sensors.unsubscribeFps', {}).catch(() => undefined)
  }
  setSurfaceVisible('osd', false)
  if (osdWindow && !osdWindow.isDestroyed()) {
    osdWindow.destroy()
  }
  osdWindow = null
  visible = false
  pageLoaded = false
}

/** Destroy the OSD renderer but keep host subscriptions for on-demand show. */
export function suspendOsdWindow(): void {
  cancelIdleDestroy('osd')
  releaseOsdWindow()
}

export function destroyOsdWindow(): void {
  cancelIdleDestroy('osd')
  try {
    globalShortcut.unregister('CommandOrControl+Shift+O')
  } catch {
    // best-effort
  }
  if (positionSaveTimer) {
    clearTimeout(positionSaveTimer)
    positionSaveTimer = null
  }
  unsubscribe?.()
  unsubscribe = null
  unsubscribeSettings?.()
  unsubscribeSettings = null
  unsubscribeFps?.()
  unsubscribeFps = null
  unsubscribeUpdated?.()
  unsubscribeUpdated = null
  unsubscribeDisplay?.()
  unsubscribeDisplay = null
  unsubscribePower?.()
  unsubscribePower = null
  stopRefresh()
  setSurfaceVisible('osd', false)
  if (osdWindow && !osdWindow.isDestroyed()) {
    osdWindow.destroy()
  }
  osdWindow = null
  visible = false
  pageLoaded = false
}
