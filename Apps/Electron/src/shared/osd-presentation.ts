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

export interface OsdSnapshot {
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
export interface OsdFpsData {
  fps?: number | null
  lowFps?: number | null
  frameTimeMs?: number | null
}

/** "osd" settings scope —mirrors OsdSettingsStore in UniversalDeviceToolkit.Lib. */
export interface OsdSettingsStore {
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
export const OSD_ITEMS = [
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

export type OsdItemName = (typeof OSD_ITEMS)[number]

export const DEFAULT_OSD_SETTINGS: OsdSettingsStore = {
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


export interface OsdFormatOptions {
  showCpuAverageFrequency: boolean
  displayMemoryInGigabytes: boolean
  temperatureUnit: 'C' | 'F'
}

export function createOsdPresentation() {
  let settings: OsdSettingsStore = { ...DEFAULT_OSD_SETTINGS }
  let lastSnapshot: OsdSnapshot | null = null
  let lastFps: OsdFpsData | null = null
  let showCpuAverageFrequency = false
  let displayMemoryInGigabytes = false
  let temperatureUnit: 'C' | 'F' = 'C'
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

function isMiniStyle(): boolean {
  return settings.selectedStyleIndex === 2
}

function isBarStyle(): boolean {
  return settings.selectedStyleIndex === 1
}

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


  return {
    configure(value: unknown): OsdSettingsStore {
      mergeSettings(value)
      return settings
    },
    render(store: OsdSettingsStore, snapshot: OsdSnapshot | null, fps: OsdFpsData | null, options: OsdFormatOptions): OsdRenderModel {
      settings = store
      lastSnapshot = snapshot
      lastFps = fps
      showCpuAverageFrequency = options.showCpuAverageFrequency
      displayMemoryInGigabytes = options.displayMemoryInGigabytes
      temperatureUnit = options.temperatureUnit
      return buildRenderModel()
    },
    document: buildOsdDocument
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

function buildOsdDocument(nonce: string): string {
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
  return html
}
