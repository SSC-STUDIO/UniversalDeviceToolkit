import './sensor.css'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Alert, Button, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import { formatDateForUi } from '../../utils/dateFormat'
import {
  ArrowSync24Regular,
  ChevronDown24Regular,
  ChevronUp24Regular,
  Flash24Regular,
  FluentIcon,
  Gauge24Regular,
  Heart24Regular,
  WeatherSunny24Regular
} from '../icons/fluent'
import type { SensorsBattery, SensorsCpu } from '../../api/sensors'
import { settingsApi } from '../../api/settings'
import { useSensorsStore } from '../../stores/sensorsStore'
import { useSettingsStore } from '../../stores/settingsStore'
import { useThemeStore } from '../../stores/themeStore'
import { SensorSkeletonColumn } from '../DashboardSkeleton'
import SensorGauge from './SensorGauge'
import TrendChart, { type TrendSeries } from './TrendChart'
import { formatUsageInGigabytes } from '../../utils/format'
import { subscribeUiVisibility } from '../../utils/uiVisibility'
import { notify } from '../../notifications'
import { useNotificationCenter } from '../../notifications/notificationCenterStore'
import { resolveSensorViewPhase, type SensorViewPhase } from './sensorViewPhase'

const CPU_UTILIZATION = '#4f9df7'
const CPU_CLOCK = '#6fbf73'
const CPU_TEMPERATURE = '#d9883b'
const GPU_UTILIZATION = '#4f9df7'
const GPU_CLOCK = '#6fbf73'
const GPU_TEMPERATURE = '#d9883b'
const BATTERY_LEVEL = '#6fbf73'
const BATTERY_RATE = '#6fbf73'
const BATTERY_TEMPERATURE = '#d9883b'
const BATTERY_CAUTION = '#e0a92e'
const BATTERY_CRITICAL = '#e05656'
const BATTERY_LOW_THRESHOLD = 20
const REFRESH_INTERVALS = [1, 2, 3, 5]

/** Session flag so 1s sensor polls do not restack the same adapter warning. */
let lowPowerAdapterNotified = false

function dismissLowPowerAdapterNotification(title: string): void {
  const state = useNotificationCenter.getState()
  for (const item of state.items) {
    if (item.severity === 'Warning' && item.title === title) {
      state.dismiss(item.id)
    }
  }
}

/** Saved refresh interval from the dashboard settings scope (default 1s). */
function readSavedRefreshInterval(scopes: Record<string, unknown>): number {
  const dashboard =
    typeof scopes.dashboard === 'object' && scopes.dashboard !== null
      ? (scopes.dashboard as Record<string, unknown>)
      : {}
  const raw = dashboard['SensorsRefreshIntervalSeconds']
  return typeof raw === 'number' && Number.isFinite(raw) && raw >= 1 && raw <= 60 ? raw : 1
}

type TemperatureUnit = 'C' | 'F'

const SENSOR_COLUMNS = ['CPU', 'Battery', 'GPU'] as const
type SensorColumnId = (typeof SENSOR_COLUMNS)[number]

function normalizeSensorColumnId(value: string): SensorColumnId | null {
  const upper = value.toUpperCase()
  if (upper === 'CPU') return 'CPU'
  if (upper === 'BATTERY') return 'Battery'
  if (upper === 'GPU') return 'GPU'
  return null
}

function readStringList(value: unknown): string[] {
  if (!Array.isArray(value)) return []
  return value.filter((item): item is string => typeof item === 'string')
}

/** Visible dashboard columns from hardwareSensors (VisibleSections + SectionOrder). */
export function readSensorLayout(scopes: Record<string, unknown>): SensorColumnId[] {
  const hardware =
    typeof scopes.hardwareSensors === 'object' && scopes.hardwareSensors !== null
      ? (scopes.hardwareSensors as Record<string, unknown>)
      : {}
  const visible = new Set(
    readStringList(hardware.VisibleSections ?? hardware.visibleSections)
      .map(normalizeSensorColumnId)
      .filter((id): id is SensorColumnId => id != null)
  )
  const ordered: SensorColumnId[] = []
  for (const item of readStringList(hardware.SectionOrder ?? hardware.sectionOrder)) {
    const id = normalizeSensorColumnId(item)
    if (id != null && (visible.size === 0 || visible.has(id)) && !ordered.includes(id)) {
      ordered.push(id)
    }
  }
  for (const id of SENSOR_COLUMNS) {
    if ((visible.size === 0 || visible.has(id)) && !ordered.includes(id)) ordered.push(id)
  }
  return ordered.length > 0 ? ordered : [...SENSOR_COLUMNS]
}

function readTemperatureUnit(scopes: Record<string, unknown>): TemperatureUnit {
  const application =
    typeof scopes.application === 'object' && scopes.application !== null
      ? (scopes.application as Record<string, unknown>)
      : {}
  if (application['TemperatureUnit'] === 'F' || application['TemperatureUnit'] === 'C') {
    return application['TemperatureUnit']
  }
  return 'C'
}

interface SensorMetric {
  label: string
  value: string
  valueColor?: string
  barValue: number
  barMax: number
  icon: React.JSX.Element
}

interface SensorDetail {
  label: string
  value: string
}

interface SensorPanelProps {
  title: string
  model?: string | null
  gauge: React.JSX.Element
  metrics: SensorMetric[]
  series: TrendSeries[]
  labels: string[]
  /** Inline warning between metrics and chart (e.g. low battery). */
  warnings?: React.JSX.Element
  /** Pinned to the bottom of the panel (below expanded details). */
  footer?: React.JSX.Element
  /** Detail rows grouped per column (Electron detail panel: two equal columns). */
  details?: SensorDetail[][]
  /** Optional block above the detail columns (battery mini gauges / GPU VRAM bar). */
  detailsHeader?: React.JSX.Element
  emptyLabel?: string
  /** WPF SensorsControl._detailsExpanded — shared across all sensor cards. */
  detailsExpanded: boolean
  onToggleDetails: () => void
}

// Formatting mirrors SensorsControl.Formatting.cs / UpdateValue():
// frequency MHz→GHz 1 decimal, temperature 0 decimals, fan 1 decimal,
// health percent 2 decimals, rate signed W 2 decimals, invalid → "-".
function formatFrequency(mhz: number | null | undefined): string {
  if (mhz == null || !Number.isFinite(mhz) || mhz < 0) return '-'
  return `${(mhz / 1000).toFixed(1)} GHz`
}

// FormatTemperature: °F conversion when the appearance setting is F.
function formatTemperature(c: number | null | undefined, unit: TemperatureUnit = 'C'): string {
  if (c == null || !Number.isFinite(c) || c < 0) return '-'
  if (unit === 'F') {
    return `${(c * (9 / 5) + 32).toFixed(0)} °F`
  }
  return `${c.toFixed(0)} °C`
}

function formatFan(speed: number | null | undefined): string {
  if (speed == null || !Number.isFinite(speed) || speed < 0) return '-'
  return `${Math.round(speed)} RPM`
}

function formatHealth(health: number | null | undefined): string {
  if (health == null || !Number.isFinite(health) || health < 0) return '-'
  const normalized = health > 1 ? health : health * 100
  return `${normalized.toFixed(2)}%`
}

function formatRate(mw: number | null | undefined): string {
  if (mw == null || !Number.isFinite(mw) || mw === -1) return '-'
  const w = mw / 1000
  const sign = w > 0 ? '+' : w < 0 ? '-' : ''
  return `${sign}${w.toFixed(2)} W`
}

// FormatMemoryClock: 0.0 MHz (Electron _gpuMemoryClockText).
function formatMemoryClock(mhz: number | null | undefined): string {
  if (mhz == null || !Number.isFinite(mhz) || mhz < 0) return '-'
  return `${mhz.toFixed(1)} MHz`
}

// FormatWattHours: mWh → Wh, 2 decimals (Electron "{0:0.00} Wh").
function formatWattHours(mwh: number | null | undefined): string {
  if (mwh == null || !Number.isFinite(mwh) || mwh <= 0) return '-'
  return `${(mwh / 1000).toFixed(2)} Wh`
}

// FormatPower: wattage 0.# decimals.
function formatPower(w: number | null | undefined): string {
  if (w == null || !Number.isFinite(w) || w < 0) return '-'
  return `${w.toFixed(1)} W`
}

// FormatVoltage: 0.000 V.
function formatVoltage(v: number | null | undefined): string {
  if (v == null || !Number.isFinite(v) || v <= 0) return '-'
  return `${v.toFixed(3)} V`
}

// FormatThroughput: B/s → KB/s → MB/s → GB/s (0.00 decimals).
function formatThroughput(bytesPerSecond: number | null | undefined): string {
  if (bytesPerSecond == null || !Number.isFinite(bytesPerSecond) || bytesPerSecond < 0) return '-'
  const kb = 1024
  const mb = kb * 1024
  const gb = mb * 1024
  if (bytesPerSecond >= gb) return `${(bytesPerSecond / gb).toFixed(2)} GB/s`
  if (bytesPerSecond >= mb) return `${(bytesPerSecond / mb).toFixed(2)} MB/s`
  if (bytesPerSecond >= kb) return `${(bytesPerSecond / kb).toFixed(2)} KB/s`
  return `${bytesPerSecond.toFixed(0)} B/s`
}

// FormatThroughputPair: "Rx x\nTx y" (multi-line via pre-line whitespace).
function formatThroughputPair(rx: number | null | undefined, tx: number | null | undefined): string {
  const rxText = formatThroughput(rx)
  const txText = formatThroughput(tx)
  if (rxText === '-' && txText === '-') return '-'
  if (rxText === '-') return `Tx ${txText}`
  if (txText === '-') return `Rx ${rxText}`
  return `Rx ${rxText}\nTx ${txText}`
}

/** Electron FormatFallbackRangeText: "a ~ b", or a single value when min==max / one side missing. */
function formatRangeText(
  min: number | null | undefined,
  max: number | null | undefined,
  formatOne: (value: number) => string
): string {
  const minOk = min != null && Number.isFinite(min)
  const maxOk = max != null && Number.isFinite(max)
  if (!minOk && !maxOk) return '-'
  if (minOk && maxOk) {
    if (min === max) return formatOne(min)
    return `${formatOne(Math.min(min, max))} ~ ${formatOne(Math.max(min, max))}`
  }
  return formatOne((minOk ? min : max) as number)
}

/** Battery power range: mW rates → "x.xx W ~ y.yy W" (signed magnitude like FormatRate). */
function formatPowerRangeMw(
  minMw: number | null | undefined,
  maxMw: number | null | undefined
): string {
  const toW = (mw: number): string => {
    const w = mw / 1000
    const sign = w > 0 ? '+' : w < 0 ? '-' : ''
    return `${sign}${Math.abs(w).toFixed(2)} W`
  }
  const minOk = minMw != null && Number.isFinite(minMw) && minMw !== -1
  const maxOk = maxMw != null && Number.isFinite(maxMw) && maxMw !== -1
  if (!minOk && !maxOk) return '-'
  if (minOk && maxOk) return `${toW(minMw)} ~ ${toW(maxMw)}`
  return toW((minOk ? minMw : maxMw) as number)
}

function formatCycleCount(cycles: number | null | undefined): string {
  if (cycles == null || !Number.isFinite(cycles) || cycles < 0) return '-'
  return String(Math.round(cycles))
}

/** Host sends yyyy-MM-dd; display as local short date. */
function formatBatteryDate(iso: string | null | undefined): string {
  if (iso == null || iso === '') return '-'
  const parsed = new Date(`${iso}T00:00:00`)
  if (!Number.isFinite(parsed.getTime())) return iso
  return formatDateForUi(parsed)
}

function trackSessionExtremum(
  slot: { min: number | null; max: number | null },
  value: number | null | undefined,
  opts?: { requirePositive?: boolean }
): void {
  if (value == null || !Number.isFinite(value)) return
  if (opts?.requirePositive === true && value <= 0) return
  if (value < 0) return
  slot.min = slot.min == null ? value : Math.min(slot.min, value)
  slot.max = slot.max == null ? value : Math.max(slot.max, value)
}

// FormatCpuPowerBreakdown: "12 W | Cores 8.5 W | Memory 3.2 W | Platform 1.1 W".
function formatCpuPowerBreakdown(cpu: SensorsCpu, labels: { cores: string; memory: string; platform: string }): string {
  const parts: string[] = []
  const total = cpu.power
  if (total != null && Number.isFinite(total) && total >= 0) parts.push(formatPower(total))
  if (cpu.powerCores != null && Number.isFinite(cpu.powerCores) && cpu.powerCores > 0) {
    parts.push(`${labels.cores} ${cpu.powerCores.toFixed(1)} W`)
  }
  if (cpu.powerMemory != null && Number.isFinite(cpu.powerMemory) && cpu.powerMemory > 0) {
    parts.push(`${labels.memory} ${cpu.powerMemory.toFixed(1)} W`)
  }
  if (cpu.powerPlatform != null && Number.isFinite(cpu.powerPlatform) && cpu.powerPlatform > 0) {
    parts.push(`${labels.platform} ${cpu.powerPlatform.toFixed(1)} W`)
  }
  return parts.length > 0 ? parts.join(' | ') : '-'
}

// Electron UpdateValue: value < 0 → bar zeroed; max < 0 → max = max(value, 1).
function metricBar(
  value: number | null | undefined,
  max: number | null | undefined,
  fallbackMax: number
): { barValue: number; barMax: number } {
  if (value == null || !Number.isFinite(value) || value < 0) return { barValue: 0, barMax: 1 }
  if (max != null && Number.isFinite(max) && max > 0) return { barValue: value, barMax: max }
  return { barValue: value, barMax: Math.max(value, fallbackMax) }
}

function barPercent(metric: SensorMetric): number {
  if (metric.barMax <= 0 || metric.barValue == null || !Number.isFinite(metric.barValue)) return 0
  return Math.min(100, Math.max(0, (metric.barValue / metric.barMax) * 100))
}

// Electron temperature thresholds: < 50 °C → green, 50–79 °C → caution, ≥ 80 °C → critical.
// Thresholds are always evaluated on the °C value, before unit conversion.
function temperatureColor(temp: number | null | undefined): string | undefined {
  if (temp == null || !Number.isFinite(temp) || temp < 0) return undefined
  if (temp >= 80) return '#eb6b6b'
  if (temp >= 50) return '#e0a92e'
  return '#6fbf73'
}

// Battery ring: green >= 80 health, caution 60-79, critical < 60;
// low charge level overrides the ring to caution (Electron IsLowBattery → ChartCautionBrush).
function batteryGaugeColor(battery: SensorsBattery | undefined): string {
  if (battery?.isLowBattery === true) {
    return BATTERY_CAUTION
  }
  const level = battery?.chargeLevel
  if (level != null && Number.isFinite(level) && level >= 0 && level <= BATTERY_LOW_THRESHOLD) {
    return BATTERY_CAUTION
  }
  const health = battery?.health
  if (health != null && Number.isFinite(health) && health >= 0) {
    const percent = health * 100
    if (percent < 60) return BATTERY_CRITICAL
    if (percent < 80) return BATTERY_CAUTION
  }
  return BATTERY_LEVEL
}

// Electron SymbolIcon "Battery024", FontSizeDisplaySection (25).
function BatteryIcon({ level, charging }: { level?: number | null; charging?: boolean }): React.JSX.Element {
  const clamped =
    level != null && Number.isFinite(level) ? Math.min(100, Math.max(0, level)) : 40
  const fillWidth = charging ? 14 : Math.max(2, (clamped / 100) * 14)
  return (
    <svg width="25" height="13" viewBox="0 0 25 13" fill="none" stroke="currentColor" strokeWidth="1.2" aria-hidden="true">
      <rect x="0.6" y="0.6" width="20.2" height="11.8" rx="2.6" />
      <path d="M23 4.2v4.6" strokeLinecap="round" />
      <rect x="3.2" y="3.2" width={fillWidth} height="6.6" rx="1.4" fill="currentColor" stroke="none" />
    </svg>
  )
}

function FrequencyIcon(): React.JSX.Element {
  return (
    <FluentIcon size={14} className="udt-sensor-panel__metric-icon">
      <Gauge24Regular />
    </FluentIcon>
  )
}

function TemperatureIcon({ color }: { color?: string }): React.JSX.Element {
  return (
    <FluentIcon size={14} className="udt-sensor-panel__metric-icon" color={color}>
      <WeatherSunny24Regular />
    </FluentIcon>
  )
}

function FanIcon(): React.JSX.Element {
  return (
    <FluentIcon size={14} className="udt-sensor-panel__metric-icon">
      <ArrowSync24Regular />
    </FluentIcon>
  )
}

function HealthIcon(): React.JSX.Element {
  return (
    <FluentIcon size={14} className="udt-sensor-panel__metric-icon">
      <Heart24Regular />
    </FluentIcon>
  )
}

function RateIcon(): React.JSX.Element {
  return (
    <FluentIcon size={14} className="udt-sensor-panel__metric-icon">
      <Flash24Regular />
    </FluentIcon>
  )
}

// Static metric icons hoisted to module scope so the 1 Hz sensor render loop
// reuses the same elements instead of re-creating them every tick
// (TemperatureIcon stays inline - its color follows the reading).
const FREQUENCY_ICON = <FrequencyIcon />
const FAN_ICON = <FanIcon />
const HEALTH_ICON = <HealthIcon />
const RATE_ICON = <RateIcon />

interface BatteryCapacityStat {
  label: string
  value: string
}

function BatteryCapacityStats({ rows }: { rows: BatteryCapacityStat[] }): React.JSX.Element {
  return (
    <div className="udt-sensor-panel__battery-stats">
      {rows.map((row) => (
        <div key={row.label} className="udt-sensor-panel__battery-stat">
          <dt title={row.label}>{row.label}</dt>
          <dd title={row.value}>{row.value}</dd>
        </div>
      ))}
    </div>
  )
}

function SensorPanel({
  title,
  model,
  gauge,
  metrics,
  series,
  labels,
  warnings,
  footer,
  details,
  detailsHeader,
  emptyLabel,
  detailsExpanded,
  onToggleDetails
}: SensorPanelProps): React.JSX.Element {
  const { t } = useTranslation()
  const hasDetails = details != null && details.length > 0
  return (
    // WPF SensorsControl._detailsExpanded is a single global flag — double-click
    // on any card toggles the detail panels of ALL sensor cards at once.
    // (Electron CardControl_PreviewMouseLeftButtonDown: 500ms double-click
    // threshold; React's native onDoubleClick fires within that window.)
    <Tooltip
      title={hasDetails && !detailsExpanded ? t('dashboard.sensor.doubleClickHint') : undefined}
      placement="top"
      mouseEnterDelay={0.4}
    >
      <section
        className={`udt-sensor-panel${detailsExpanded && hasDetails ? ' udt-sensor-panel--expanded' : ''}`}
        onDoubleClick={() => {
          if (hasDetails) onToggleDetails()
        }}
      >
      <div className="udt-sensor-panel__heading">
        <h2>{title}</h2>
        {model != null && model !== '' && (
          <span className="udt-sensor-panel__model" title={model}>
            {model}
          </span>
        )}
        {hasDetails && (
          <button
            type="button"
            className={`udt-sensor-panel__details-toggle${detailsExpanded ? ' udt-sensor-panel__details-toggle--expanded' : ''}`}
            aria-expanded={detailsExpanded}
            onClick={(event) => {
              event.stopPropagation()
              onToggleDetails()
            }}
          >
            {detailsExpanded ? <ChevronUp24Regular /> : <ChevronDown24Regular />}
            {detailsExpanded
              ? t('dashboard.sensor.hideDetails', { defaultValue: 'Hide details' })
              : t('dashboard.sensor.showDetails', { defaultValue: 'Show details' })}
          </button>
        )}
      </div>
      <div className="udt-sensor-panel__summary">
        {gauge}
        <dl className="udt-sensor-panel__metrics">
          {metrics.map((metric) => (
            <div key={metric.label} className="udt-sensor-panel__metric">
              <dt title={metric.label} aria-label={metric.label}>
                {metric.icon}
                <span className="udt-sensor-panel__metric-label">{metric.label}</span>
              </dt>
              <div className="udt-sensor-panel__metric-track">
                {/* Hot path (1Hz): scaleX composites on the GPU; width would relayout every tick. */}
                <div
                  className="udt-sensor-panel__metric-fill"
                  style={{ transform: `scaleX(${barPercent(metric) / 100})` }}
                />
              </div>
              <dd style={metric.valueColor != null ? { color: metric.valueColor } : undefined} title={metric.value}>
                {metric.value}
              </dd>
            </div>
          ))}
        </dl>
      </div>
      {warnings}
      <div className="udt-sensor-panel__chart">
        <TrendChart series={series} labels={labels} emptyLabel={emptyLabel} />
      </div>
      <div className="udt-sensor-panel__legend">
        {series.map((item) => (
          <span key={item.name}>
            <i style={{ backgroundColor: item.color }} />
            {item.name}
          </span>
        ))}
      </div>
      {detailsExpanded && hasDetails && (
        <div className="udt-sensor-panel__details">
          {detailsHeader}
          <div className="udt-sensor-panel__details-cols">
            {details.map((column, columnIndex) => (
              <div key={columnIndex} className="udt-sensor-panel__details-col">
                {column
                  .filter((row) => row.value !== '-')
                  .map((row) => (
                    <div key={row.label} className="udt-sensor-panel__detail">
                      <dt>{row.label}</dt>
                      <dd title={row.value}>{row.value}</dd>
                    </div>
                  ))}
              </div>
            ))}
          </div>
        </div>
      )}
      {footer}
      </section>
    </Tooltip>
  )
}

export default function SensorSection(): React.JSX.Element {
  const { t } = useTranslation()
  const isDark = useThemeStore((state) => state.themeMode === 'dark')
  const snapshot = useSensorsStore((state) => state.snapshot)
  const status = useSensorsStore((state) => state.status)
  const trend = useSensorsStore((state) => state.trend)
  const scopes = useSettingsStore((state) => state.scopes)
  const [requestedPhase, setRequestedPhase] = useState<SensorViewPhase>('idle')
  const [loadError, setLoadError] = useState<string | null>(null)
  const viewPhase = resolveSensorViewPhase(snapshot, requestedPhase)
  const retryRef = useRef<(() => void) | null>(null)
  const savedIntervalRef = useRef(1)
  // WPF SensorsControl._detailsExpanded: one flag toggles all detail panels.
  const [allDetailsExpanded, setAllDetailsExpanded] = useState(false)
  const toggleAllDetails = (): void => setAllDetailsExpanded((value) => !value)
  // Session min/max for temp & voltage ranges (FormatFallbackRangeText parity).
  const sessionExtremumRef = useRef({
    cpuTemp: { min: null as number | null, max: null as number | null },
    cpuVoltage: { min: null as number | null, max: null as number | null },
    gpuTemp: { min: null as number | null, max: null as number | null },
    gpuVoltage: { min: null as number | null, max: null as number | null }
  })
  const intervalSec = useSensorsStore((state) => state.intervalSec)
  const [refreshMenu, setRefreshMenu] = useState<{ x: number; y: number } | null>(null)
  const boardRef = useRef<HTMLDivElement>(null)
  const refreshMenuRef = useRef<HTMLDivElement>(null)
  const isLowPowerAdapter =
    useSensorsStore((state) => state.snapshot?.battery?.isLowPowerAdapter === true)

  // Host already detects the adapter (isLowPowerAdapter on the snapshot).
  // Presentation goes through the notification center, not a dashboard overlay.
  // Persistent while the low-watt adapter is still connected; rising-edge only
  // so polling cannot stack duplicates. Cleared when a sufficient adapter is in.
  useEffect(() => {
    const title = t('dashboard.sensor.lowPowerAdapter')
    if (isLowPowerAdapter) {
      if (lowPowerAdapterNotified) return
      lowPowerAdapterNotified = true
      notify({ title, severity: 'Warning', isPersistent: true })
      return
    }
    if (!lowPowerAdapterNotified) return
    lowPowerAdapterNotified = false
    dismissLowPowerAdapterNotification(title)
  }, [isLowPowerAdapter, t])

  // First-snapshot loading chrome: fixed 3-column skeleton with the global
  // shimmer/breathing animation (same structure as DashboardSkeleton).

  const temperatureUnit = readTemperatureUnit(scopes)
  const sensorLayout = readSensorLayout(scopes)

  useEffect(() => {
    const store = useSensorsStore.getState()
    let cancelled = false
    let pollGeneration = 0
    let polling = false
    let uiActive = false
    savedIntervalRef.current = 1

    const loadFirstSnapshot = async (): Promise<void> => {
      setRequestedPhase('loading')
      setLoadError(null)
      await store.loadStatus()
      if (cancelled) return
      await store.loadSnapshot()
      if (cancelled) return
      const after = useSensorsStore.getState()
      if (after.snapshot != null) {
        setRequestedPhase('ready')
        return
      }
      setLoadError(after.error)
      setRequestedPhase('error')
    }

    const startPolls = async (): Promise<void> => {
      if (cancelled || polling) return
      polling = true
      const generation = ++pollGeneration
      await store.start(savedIntervalRef.current)
      if (cancelled || generation !== pollGeneration) {
        polling = false
        await store.stop()
        return
      }
      if (!useSensorsStore.getState().subscribed) {
        polling = false
      }
    }

    const stopPolls = async (): Promise<void> => {
      pollGeneration += 1
      polling = false
      await store.stop()
    }

    retryRef.current = () => {
      void (async () => {
        await loadFirstSnapshot()
        if (cancelled) return
        if (uiActive) {
          polling = false
          await startPolls()
        }
      })()
    }

    void (async () => {
      await useSettingsStore.getState().load()
      if (cancelled) return
      savedIntervalRef.current = readSavedRefreshInterval(useSettingsStore.getState().scopes)
      await loadFirstSnapshot()
    })()

    const unsubscribeVisibility = subscribeUiVisibility((active) => {
      if (cancelled) return
      uiActive = active
      if (active) {
        void startPolls()
      } else {
        void stopPolls()
      }
    })
    return () => {
      cancelled = true
      retryRef.current = null
      unsubscribeVisibility()
      void stopPolls()
    }
  }, [])

  const trendSeries = useMemo(
    () => ({
      cpu: [
        { name: t('dashboard.sensor.usage'), color: CPU_UTILIZATION, data: [...trend.cpuUsage], max: 100 },
        { name: t('dashboard.sensor.frequency'), color: CPU_CLOCK, data: [...trend.cpuClock] },
        { name: t('dashboard.sensor.temperature'), color: CPU_TEMPERATURE, data: [...trend.cpuTemperature], max: 110 }
      ] satisfies TrendSeries[],
      battery: [
        { name: t('dashboard.sensor.rate'), color: BATTERY_RATE, data: [...trend.batteryRate] },
        { name: t('dashboard.sensor.temperature'), color: BATTERY_TEMPERATURE, data: [...trend.batteryTemperature], max: 60 }
      ] satisfies TrendSeries[],
      gpu: [
        { name: t('dashboard.sensor.usage'), color: GPU_UTILIZATION, data: [...trend.gpuUsage], max: 100 },
        { name: t('dashboard.sensor.frequency'), color: GPU_CLOCK, data: [...trend.gpuClock] },
        { name: t('dashboard.sensor.temperature'), color: GPU_TEMPERATURE, data: [...trend.gpuTemperature], max: 110 }
      ] satisfies TrendSeries[]
    }),
    [trend, t]
  )

  const cpu = snapshot?.cpu
  const gpu = snapshot?.gpu
  const memory = snapshot?.memory
  const battery = snapshot?.battery
  const cpuModel = snapshot?.info?.cpuName ?? status?.cpuName ?? null
  const gpuModel = snapshot?.info?.gpuName ?? status?.gpuName ?? null
  const labels = trend.labels
  const valueColor = isDark ? 'rgba(255, 255, 255, 0.77)' : 'rgba(0, 0, 0, 0.62)'

  const cpuClock = cpu?.coreClockAvg ?? cpu?.coreClockMax
  const cpuTemperature = temperatureColor(cpu?.temperature)
  const gpuTemperature = temperatureColor(gpu?.temperature)
  const batteryTemperature = temperatureColor(battery?.temperature)

  const healthPercent =
    battery?.health != null && Number.isFinite(battery.health) && battery.health >= 0
      ? battery.health > 1
        ? battery.health
        : battery.health * 100
      : null

  const rateValid =
    battery?.chargeRate != null && Number.isFinite(battery.chargeRate) && battery.chargeRate !== -1
  const rateBarValue = rateValid ? Math.min(Math.abs(battery.chargeRate!) / 1000, 100) : 0

  const isLowBattery =
    battery?.isLowBattery === true ||
    (battery?.chargeLevel != null &&
      Number.isFinite(battery.chargeLevel) &&
      battery.chargeLevel <= BATTERY_LOW_THRESHOLD)

  // Session extrema for detail-grid ranges when Host snapshot lacks min/max.
  trackSessionExtremum(sessionExtremumRef.current.cpuTemp, cpu?.temperature)
  trackSessionExtremum(sessionExtremumRef.current.cpuVoltage, cpu?.voltage, { requirePositive: true })
  trackSessionExtremum(sessionExtremumRef.current.gpuTemp, gpu?.temperature)
  trackSessionExtremum(sessionExtremumRef.current.gpuVoltage, gpu?.voltage, { requirePositive: true })
  const session = sessionExtremumRef.current

  // Low-battery warning stays in the Battery column (between metrics and chart).
  const batteryWarnings =
    battery != null && isLowBattery ? (
      <div className="udt-sensor-panel__warnings">
        <div className="udt-sensor-panel__warning">{t('dashboard.sensor.batteryLow')}</div>
        <div className="udt-sensor-panel__battery-icon">
          <BatteryIcon level={battery.chargeLevel} charging={battery.isCharging === true} />
        </div>
      </div>
    ) : undefined

  // Advanced details (SensorsControl detail window parity). Two columns; rows
  // whose value is "-" are collapsed (Electron UpdateDetailContainerVisibility).
  // Temp/voltage ranges fall back to the live/session value (FormatFallbackRangeText).
  const cpuDetails: SensorDetail[][] = [
    [
      {
        label: t('dashboard.sensor.detail.power'),
        value: formatCpuPowerBreakdown(cpu ?? {}, {
          cores: t('dashboard.sensor.detail.powerCores'),
          memory: t('dashboard.sensor.detail.powerMemory'),
          platform: t('dashboard.sensor.detail.powerPlatform')
        })
      },
      { label: t('dashboard.sensor.detail.pCoreClock'), value: formatFrequency(cpu?.pCoreClock) },
      { label: t('dashboard.sensor.detail.eCoreClock'), value: formatFrequency(cpu?.eCoreClock) },
      {
        label: t('dashboard.sensor.detail.memoryUsage'),
        value: formatUsageInGigabytes(memory?.usedMb, memory?.totalMb, memory?.usage)
      }
    ],
    [
      {
        label: t('dashboard.sensor.temperature'),
        value: formatRangeText(session.cpuTemp.min, session.cpuTemp.max, (v) =>
          formatTemperature(v, temperatureUnit)
        )
      },
      {
        label: t('dashboard.sensor.voltageRange'),
        value: formatRangeText(session.cpuVoltage.min, session.cpuVoltage.max, formatVoltage)
      },
      {
        label: t('dashboard.sensor.memoryTemperature'),
        value: formatTemperature(memory?.highestTemperature, temperatureUnit)
      },
      {
        label: t('dashboard.sensor.ssdTemperature'),
        value: (() => {
          const temps = snapshot?.storage?.temperatures
          const valid = (temps ?? []).filter((value) => value != null && Number.isFinite(value) && value >= 0)
          return valid.length > 0
            ? valid.map((value) => formatTemperature(value, temperatureUnit)).join(' / ')
            : '-'
        })()
      }
    ]
  ]

  const gpuMemoryClockBar = metricBar(gpu?.memoryClock, null, 8000)
  const gpuMemoryClockText = formatMemoryClock(gpu?.memoryClock)
  const gpuDetailsHeader =
    gpuMemoryClockText !== '-' ? (
      <div className="udt-sensor-panel__vram-clock">
        <dt title={t('dashboard.sensor.detail.vramClock')}>{t('dashboard.sensor.detail.vramClock')}</dt>
        <div className="udt-sensor-panel__vram-clock-track">
          <div
            className="udt-sensor-panel__vram-clock-fill"
            style={{
              transform: `scaleX(${Math.min(1, Math.max(0, gpuMemoryClockBar.barValue / gpuMemoryClockBar.barMax))})`
            }}
          />
        </div>
        <dd title={gpuMemoryClockText}>{gpuMemoryClockText}</dd>
      </div>
    ) : undefined

  const gpuDetails: SensorDetail[][] = [
    [
      {
        label: snapshot?.info?.gpuIsIntegrated
          ? t('dashboard.sensor.detail.sharedMemoryUsage')
          : t('dashboard.sensor.detail.vramUsage'),
        value: formatUsageInGigabytes(gpu?.vramUsedMb, gpu?.vramTotalMb, gpu?.vramUtilization)
      },
      {
        label: t('dashboard.sensor.detail.pcieThroughput'),
        value: formatThroughputPair(gpu?.pcieRxThroughput, gpu?.pcieTxThroughput)
      },
      {
        label: t('dashboard.sensor.temperature'),
        value: formatRangeText(session.gpuTemp.min, session.gpuTemp.max, (v) =>
          formatTemperature(v, temperatureUnit)
        )
      }
    ],
    [
      { label: t('dashboard.sensor.vramTemperature'), value: formatTemperature(gpu?.vramTemperature, temperatureUnit) },
      { label: t('dashboard.sensor.detail.hotSpot'), value: formatTemperature(gpu?.hotSpotTemperature, temperatureUnit) },
      {
        label: t('dashboard.sensor.voltageRange'),
        value: formatRangeText(session.gpuVoltage.min, session.gpuVoltage.max, formatVoltage)
      }
    ]
  ]

  const designCapacity = battery?.designCapacity
  const fullChargeCapacity = battery?.fullChargeCapacity
  const designCapacityValid = designCapacity != null && Number.isFinite(designCapacity) && designCapacity > 0
  const fullChargeCapacityValid =
    fullChargeCapacity != null && Number.isFinite(fullChargeCapacity) && fullChargeCapacity > 0
  const remainingWh =
    battery?.chargeLevel != null && Number.isFinite(battery.chargeLevel) && fullChargeCapacityValid
      ? (battery.chargeLevel / 100) * (fullChargeCapacity / 1000)
      : null

  // Battery detail header: capacity / full charge / health as label+value (no bars).
  const batteryDetailsHeader = (
    <BatteryCapacityStats
      rows={[
        {
          label: t('dashboard.sensor.capacity'),
          value: remainingWh != null ? `${remainingWh.toFixed(2)} Wh` : '-'
        },
        {
          label: t('dashboard.sensor.fullCapacity'),
          value: fullChargeCapacityValid ? formatWattHours(fullChargeCapacity) : '-'
        },
        {
          label: t('dashboard.sensor.health'),
          value: formatHealth(battery?.health)
        }
      ]}
    />
  )

  const batteryAmbientTemp = battery?.avgTemperature ?? battery?.temperature
  const batteryDate = battery?.manufactureDate ?? battery?.firstUseDate
  const batteryDetails: SensorDetail[][] = [
    [
      {
        label: t('dashboard.sensor.powerRange'),
        value: formatPowerRangeMw(battery?.minDischargeRate, battery?.maxDischargeRate)
      },
      { label: t('dashboard.sensor.cycles'), value: formatCycleCount(battery?.cycleCount) },
      ...(designCapacityValid
        ? [{ label: t('dashboard.sensor.designCapacity'), value: formatWattHours(designCapacity) }]
        : [])
    ],
    [
      { label: t('dashboard.sensor.date'), value: formatBatteryDate(batteryDate) },
      {
        label: t('dashboard.sensor.temperature'),
        value: formatTemperature(batteryAmbientTemp, temperatureUnit)
      }
    ]
  ]

  const chartEmptyLabel = t('dashboard.sensor.chartEmpty', {
    defaultValue: 'Waiting for sensor data'
  })

  // Close the refresh context menu on outside click / Escape.
  useEffect(() => {
    if (refreshMenu == null) return
    const close = (event: MouseEvent): void => {
      if (event.target instanceof Node && refreshMenuRef.current?.contains(event.target)) return
      setRefreshMenu(null)
    }
    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') setRefreshMenu(null)
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [refreshMenu])

  // Electron SensorsControl refresh context menu: right-click on the sensors card.
  const openRefreshMenu = (event: React.MouseEvent<HTMLDivElement>): void => {
    event.preventDefault()
    const rect = boardRef.current?.getBoundingClientRect()
    if (rect == null) return
    setRefreshMenu({
      x: Math.max(0, Math.min(event.clientX - rect.left, rect.width - 148)),
      y: Math.max(0, Math.min(event.clientY - rect.top, rect.height - 168))
    })
  }

  const handleRetry = (): void => {
    retryRef.current?.()
  }

  const handleIntervalChange = (seconds: number): void => {
    setRefreshMenu(null)
    savedIntervalRef.current = seconds
    // start() re-subscribes with the new interval when already polling.
    void useSensorsStore.getState().start(seconds)
    // Persist to the dashboard settings scope so the choice survives restarts.
    const scopesState = useSettingsStore.getState().scopes
    const currentDashboard =
      typeof scopesState.dashboard === 'object' && scopesState.dashboard !== null
        ? (scopesState.dashboard as Record<string, unknown>)
        : {}
    const next = { ...currentDashboard, SensorsRefreshIntervalSeconds: seconds }
    useSettingsStore.getState().setScope('dashboard', next)
    settingsApi
      .set('dashboard', next)
      .then(() => settingsApi.save(['dashboard']))
      .catch(() => undefined)
  }

  if (viewPhase === 'error') {
    return (
      <div className="udt-sensors udt-fade-in">
        <div className="udt-sensor-board udt-sensor-board--status">
          <Alert
            type="error"
            showIcon
            message={t('dashboard.sensor.loadError', { defaultValue: 'Failed to load sensor data' })}
            description={loadError ?? t('common.error', { defaultValue: 'Something went wrong' })}
            action={
              <Button size="small" onClick={handleRetry}>
                {t('common.retry', { defaultValue: 'Retry' })}
              </Button>
            }
          />
        </div>
      </div>
    )
  }

  if (viewPhase === 'idle' || viewPhase === 'loading' || snapshot == null) {
    return (
      <div className="udt-sensors udt-fade-in" role="status" aria-label={t('common.loading', { defaultValue: 'Loading…' })}>
        <div className="udt-sensor-board">
          <div
            className="udt-sensor-board__grid"
            style={{ ['--udt-sensor-columns' as string]: String(sensorLayout.length) }}
          >
            {sensorLayout.map((columnId, idx) => {
              if (columnId === 'CPU') {
                return (
                  <SensorSkeletonColumn
                    key="CPU"
                    titleWidth={72}
                    subtitleWidth={168}
                    metricWidths={[52, 44, 40]}
                    staggerBase={idx * 16}
                  />
                )
              }
              if (columnId === 'Battery') {
                return (
                  <SensorSkeletonColumn
                    key="Battery"
                    titleWidth={64}
                    subtitleWidth={140}
                    metricWidths={[48, 44, 44]}
                    staggerBase={idx * 16}
                    legendCount={2}
                  />
                )
              }
              return (
                <SensorSkeletonColumn
                  key="GPU"
                  titleWidth={68}
                  subtitleWidth={152}
                  metricWidths={[52, 40, 44]}
                  staggerBase={idx * 16}
                />
              )
            })}
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="udt-sensors udt-fade-in">
      <div
        ref={boardRef}
        className="udt-sensor-board"
        onContextMenu={openRefreshMenu}
      >
        <div
          className="udt-sensor-board__grid"
          style={{ ['--udt-sensor-columns' as string]: String(sensorLayout.length) }}
        >
          {sensorLayout.map((columnId) => {
            if (columnId === 'CPU') {
              return (
                <SensorPanel
                  key="CPU"
                  title={t('dashboard.sensor.cpu')}
                  model={cpuModel}
                  gauge={
                    <SensorGauge
                      value={cpu?.usage}
                      max={100}
                      unit="%"
                      label={t('dashboard.sensor.usage')}
                      color={CPU_UTILIZATION}
                    />
                  }
                  metrics={[
                    {
                      label: t('dashboard.sensor.frequency'),
                      value: formatFrequency(cpuClock),
                      icon: FREQUENCY_ICON,
                      ...metricBar(cpuClock, cpu?.coreClockMax, 5000)
                    },
                    {
                      label: t('dashboard.sensor.temperature'),
                      value: formatTemperature(cpu?.temperature, temperatureUnit),
                      valueColor: cpuTemperature ?? valueColor,
                      icon: <TemperatureIcon color={cpuTemperature ?? valueColor} />,
                      ...metricBar(cpu?.temperature, null, 100)
                    },
                    {
                      label: t('dashboard.sensor.fan'),
                      value: formatFan(cpu?.fanSpeed),
                      icon: FAN_ICON,
                      ...metricBar(cpu?.fanSpeed, null, 5000)
                    }
                  ]}
                  series={trendSeries.cpu}
                  labels={labels}
                  details={cpuDetails}
                  detailsExpanded={allDetailsExpanded}
                  onToggleDetails={toggleAllDetails}
                  emptyLabel={chartEmptyLabel}
                />
              )
            }
            if (columnId === 'Battery') {
              return (
                <SensorPanel
                  key="Battery"
                  title={t('dashboard.sensor.battery')}
                  model={battery?.modelName}
                  gauge={
                    <SensorGauge
                      value={battery?.chargeLevel}
                      max={100}
                      unit="%"
                      label={t('dashboard.sensor.charge')}
                      color={batteryGaugeColor(battery)}
                    />
                  }
                  metrics={[
                    {
                      label: t('dashboard.sensor.health'),
                      value: formatHealth(battery?.health),
                      icon: HEALTH_ICON,
                      ...metricBar(healthPercent, 100, 100)
                    },
                    {
                      label: t('dashboard.sensor.temperature'),
                      value: formatTemperature(battery?.temperature, temperatureUnit),
                      valueColor: batteryTemperature ?? valueColor,
                      icon: <TemperatureIcon color={batteryTemperature ?? valueColor} />,
                      ...metricBar(battery?.temperature, 60, 60)
                    },
                    {
                      label: t('dashboard.sensor.rate'),
                      value: formatRate(battery?.chargeRate),
                      icon: RATE_ICON,
                      barValue: rateBarValue,
                      barMax: 100
                    }
                  ]}
                  series={trendSeries.battery}
                  labels={labels}
                  warnings={batteryWarnings}
                  details={batteryDetails}
                  detailsExpanded={allDetailsExpanded}
                  onToggleDetails={toggleAllDetails}
                  detailsHeader={batteryDetailsHeader}
                  emptyLabel={chartEmptyLabel}
                />
              )
            }
            return (
              <SensorPanel
                key="GPU"
                title={t('dashboard.sensor.gpu')}
                model={gpuModel}
                gauge={
                  <SensorGauge
                    value={gpu?.usage}
                    max={100}
                    unit="%"
                    label={t('dashboard.sensor.usage')}
                    color={GPU_UTILIZATION}
                  />
                }
                metrics={[
                  {
                    label: t('dashboard.sensor.frequency'),
                    value: formatFrequency(gpu?.coreClock),
                    icon: FREQUENCY_ICON,
                    ...metricBar(gpu?.coreClock, null, 2500)
                  },
                  {
                    label: t('dashboard.sensor.temperature'),
                    value: formatTemperature(gpu?.temperature, temperatureUnit),
                    valueColor: gpuTemperature ?? valueColor,
                    icon: <TemperatureIcon color={gpuTemperature ?? valueColor} />,
                    ...metricBar(gpu?.temperature, null, 100)
                  },
                  {
                    label: t('dashboard.sensor.fan'),
                    value: formatFan(gpu?.fanSpeed),
                    icon: FAN_ICON,
                    ...metricBar(gpu?.fanSpeed, null, 5000)
                  }
                ]}
                series={trendSeries.gpu}
                labels={labels}
                details={gpuDetails}
                detailsHeader={gpuDetailsHeader}
                detailsExpanded={allDetailsExpanded}
                onToggleDetails={toggleAllDetails}
                emptyLabel={chartEmptyLabel}
              />
            )
          })}
        </div>
        {refreshMenu != null && (
          <div
            ref={refreshMenuRef}
            className="udt-sensor-context-menu"
            style={{ left: refreshMenu.x, top: refreshMenu.y }}
            role="menu"
            aria-label={t('dashboard.sensor.refreshInterval')}
          >
            {REFRESH_INTERVALS.map((seconds) => (
              <button
                key={seconds}
                type="button"
                role="menuitem"
                className={`udt-sensor-context-menu__item${intervalSec === seconds ? ' udt-sensor-context-menu__item--checked' : ''}`}
                onClick={() => handleIntervalChange(seconds)}
              >
                <span className="udt-sensor-context-menu__check" aria-hidden="true">
                  {intervalSec === seconds ? '✓' : ''}
                </span>
                {seconds} s
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
