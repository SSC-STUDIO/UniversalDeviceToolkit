import { useCallback, useEffect, useState } from 'react'
import { create } from 'zustand'
import { useTranslation } from 'react-i18next'
import { useUtilsDialog } from './useUtilsDialog'
import { Flash24Regular, Phone24Regular, ArrowSync24Regular } from '../icons/fluent'
import { featuresApi } from '../../api/features'
import { dashboardHardwareApi, type DiscreteGpuState } from '../../api/dashboardHardware'
import { sensorsApi, type SensorSnapshot } from '../../api/sensors'
import { settingsApi } from '../../api/settings'
import { godModeApi } from '../../api/godMode'
import { updateApi } from '../../api/update'
import { systemApi } from '../../api/system'
import './utils.css'

/**
 * Port of Electron StatusWindow (tray status popup): power mode + God Mode preset,
 * CPU/memory/SSD sensor summaries, discrete GPU state and battery overview,
 * plus an update-available indicator. Opened via `tray:status` bridge event
 * (hover tooltip / explicit callers; not part of the original tray context menu).
 */

interface StatusRequest {
  id: number
}

let requestSeq = 0
let pendingResolve: (() => void) | null = null

interface StatusState {
  request: StatusRequest | null
  show: () => void
  settle: () => void
}

const useStatusStore = create<StatusState>((set) => ({
  request: null,
  show: () => set({ request: { id: ++requestSeq } }),
  settle: () => {
    pendingResolve?.()
    pendingResolve = null
    set({ request: null })
  }
}))

export function openStatusModal(): Promise<void> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useStatusStore.getState().show()
  })
}

function stateKey(value: string): string {
  return value.charAt(0).toLowerCase() + value.slice(1)
}

function formatTemperature(celsius: number | null | undefined, unit: 'C' | 'F'): string | null {
  if (celsius == null || !Number.isFinite(celsius) || celsius < 0) return null
  const value = unit === 'F' ? (celsius * 9) / 5 + 32 : celsius
  return `${value.toFixed(0)} °${unit}`
}

function formatPower(watts: number | null | undefined): string | null {
  if (watts == null || !Number.isFinite(watts) || watts < 0) return null
  return `${watts.toFixed(1)} W`
}

function formatVoltage(volts: number | null | undefined): string | null {
  if (volts == null || !Number.isFinite(volts) || volts < 0) return null
  return `${volts.toFixed(2)} V`
}

function formatSensorSummary(
  temperature: number | null | undefined,
  power: number | null | undefined,
  voltage: number | null | undefined,
  unit: 'C' | 'F'
): string | null {
  const parts = [
    formatTemperature(temperature, unit),
    formatPower(power),
    formatVoltage(voltage)
  ].filter((part): part is string => part != null && part.length > 0)
  return parts.length === 0 ? null : parts.join(' | ')
}

function formatMemorySummary(snapshot: SensorSnapshot | null): string | null {
  const memory = snapshot?.memory
  if (!memory) return null
  const parts: string[] = []
  if (
    memory.usedMb != null && Number.isFinite(memory.usedMb) && memory.usedMb >= 0 &&
    memory.totalMb != null && Number.isFinite(memory.totalMb) && memory.totalMb > 0
  ) {
    parts.push(`${(memory.usedMb / 1024).toFixed(1)} / ${(memory.totalMb / 1024).toFixed(1)} GB`)
  } else if (memory.usage != null && Number.isFinite(memory.usage) && memory.usage >= 0) {
    parts.push(`${memory.usage.toFixed(0)}%`)
  }
  if (memory.highestTemperature != null && Number.isFinite(memory.highestTemperature) && memory.highestTemperature > 0) {
    parts.push(`${memory.highestTemperature.toFixed(0)} °C`)
  }
  return parts.length === 0 ? null : parts.join(' | ')
}

function formatSsdSummary(snapshot: SensorSnapshot | null): string | null {
  const temperatures = (snapshot?.storage?.temperatures ?? []).filter(
    (value): value is number => value != null && Number.isFinite(value) && value >= 0
  )
  if (temperatures.length === 0) return null
  return temperatures.map((value) => `${value.toFixed(0)} °C`).join(' / ')
}

function formatSignedWatts(mw: number | null | undefined): string {
  if (mw == null || !Number.isFinite(mw) || mw === -1) return '-'
  const w = mw / 1000
  const sign = w > 0 ? '+' : w < 0 ? '-' : ''
  return `${sign}${Math.abs(w).toFixed(2)} W`
}

const GPU_DOT_COLORS: Record<DiscreteGpuState, string> = {
  Active: '#4caf50',
  MonitorConnected: '#4caf50',
  Inactive: '#ffaa00',
  PoweredOff: '#e05656',
  Unknown: '#9e9e9e',
  NvidiaGpuNotFound: '#9e9e9e'
}

interface StatusData {
  powerMode: string | null
  godModePresetName: string | null
  cpuSummary: string | null
  memorySummary: string | null
  ssdSummary: string | null
  gpuState: DiscreteGpuState | null
  gpuPerformanceState: string | null
  gpuSummary: string | null
  batteryPercent: number | null
  batteryState: string | null
  dischargeRate: number | null
  minDischargeRate: number | null
  maxDischargeRate: number | null
  isCompatibilityMode: boolean
  hasUpdate: boolean
}

export default function StatusModalHost(): React.JSX.Element {
  const { t } = useTranslation()
  const request = useStatusStore((s) => s.request)
  const settle = useStatusStore((s) => s.settle)
  const { dialogRef, titleId, dialogProps } = useUtilsDialog(request != null, settle)
  const [data, setData] = useState<StatusData | null>(null)

  const load = useCallback(async (): Promise<void> => {
    const unit = 'C'
    const next: StatusData = {
      powerMode: null,
      godModePresetName: null,
      cpuSummary: null,
      memorySummary: null,
      ssdSummary: null,
      gpuState: null,
      gpuPerformanceState: null,
      gpuSummary: null,
      batteryPercent: null,
      batteryState: null,
      dischargeRate: null,
      minDischargeRate: null,
      maxDischargeRate: null,
      isCompatibilityMode: false,
      hasUpdate: false
    }

    try {
      const result = await settingsApi.get('application')
      const app = (result.value ?? {}) as Record<string, unknown>
      const temperatureUnit = app['TemperatureUnit'] === 'F' ? 'F' : unit

      const [supported, snapshot] = await Promise.all([
        featuresApi.getSupported('powerMode').catch(() => ({ supported: false })),
        sensorsApi.getSnapshot().catch(() => null)
      ])

      if (supported.supported) {
        try {
          const stateResult = await featuresApi.getState('powerMode')
          const state = typeof stateResult.state === 'string' ? stateResult.state : null
          next.powerMode = state
          if (state === 'GodMode') {
            try {
              const godMode = await godModeApi.load()
              const store = godMode?.store
              const preset = store != null ? store.presets[store.activePresetId] : undefined
              next.godModePresetName = preset?.name?.trim() ? preset.name : '-'
            } catch {
              next.godModePresetName = '-'
            }
          }
        } catch {
          // Power mode state unavailable.
        }
      }

      if (snapshot != null) {
        next.cpuSummary = formatSensorSummary(
          snapshot.cpu?.temperature,
          snapshot.cpu?.power,
          snapshot.cpu?.voltage,
          temperatureUnit
        )
        next.memorySummary = formatMemorySummary(snapshot)
        next.ssdSummary = formatSsdSummary(snapshot)
        if (snapshot.battery?.chargeLevel != null) {
          next.batteryPercent = snapshot.battery.chargeLevel
        }
      }

      try {
        const batteryState = await featuresApi.getState('battery')
        next.batteryState = typeof batteryState.state === 'string' ? batteryState.state : null
      } catch {
        // Battery feature unsupported.
      }

      try {
        const gpu = await dashboardHardwareApi.getState()
        next.gpuState = gpu.discreteGpu.supported ? gpu.discreteGpu.state : null
        next.gpuPerformanceState = gpu.discreteGpu.performanceState ?? null
      } catch {
        // Discrete GPU controller unavailable.
      }

      if (snapshot != null) {
        const battery = snapshot.battery
        if (battery?.chargeRate != null) next.dischargeRate = battery.chargeRate
        next.gpuSummary = formatSensorSummary(
          snapshot.gpu?.temperature,
          snapshot.gpu?.power,
          snapshot.gpu?.voltage,
          temperatureUnit
        )
      }

      try {
        const systemInfo = await systemApi.info()
        next.isCompatibilityMode = systemInfo.isCompatible === false
      } catch {
        // Compatibility detection unavailable.
      }

      try {
        const update = await updateApi.check(false)
        next.hasUpdate = update.available === true
      } catch {
        // Update check unavailable.
      }

      setData(next)
    } catch {
      setData(next)
    }
  }, [])

  useEffect(() => {
    if (!request) return
    setData(null)
    void load()
  }, [request, load])

  if (!request) return <></>

  const renderRow = (label: string, value: string | null | undefined): React.JSX.Element | null => {
    if (value == null) return null
    return (
      <div className="udt-utils-row" style={{ cursor: 'default' }}>
        <span className="udt-utils-row__label">{label}</span>
        <span className="udt-utils-row__value">{value}</span>
      </div>
    )
  }

  const powerModeLabel = data?.powerMode
    ? t(`feature.powerModeOptions.${stateKey(data.powerMode)}`, { defaultValue: data.powerMode })
    : '-'

  const batteryStateLabel = data?.batteryState
    ? t(`feature.batteryModes.${stateKey(data.batteryState)}`, { defaultValue: data.batteryState })
    : '-'

  const gpuDot = data?.gpuState ? GPU_DOT_COLORS[data.gpuState] : undefined
  const gpuActiveLabel =
    data?.gpuState === 'Active' || data?.gpuState === 'MonitorConnected'
      ? t('wpf.active')
      : data?.gpuState === 'PoweredOff'
        ? t('wpf.poweredOff')
        : data?.gpuState != null && data.gpuState !== 'Unknown' && data.gpuState !== 'NvidiaGpuNotFound'
          ? t('wpf.inactive')
          : null

  const hasGpuBlock = data?.gpuState != null || data?.gpuSummary != null

  return (
    <div className="udt-utils-backdrop" onClick={settle}>
      <div
        ref={dialogRef}
        className="udt-utils-modal"
        style={{ width: 380, minWidth: 300 }}
        onClick={(event) => event.stopPropagation()}
        {...dialogProps}
      >
        <div className="udt-utils-modal__title" id={titleId} style={{ paddingBottom: 8 }}>
          {t('app.name')}
        </div>
        <div className="udt-utils-modal__body" style={{ paddingTop: 0 }}>
          <div className="udt-utils-card" style={{ padding: 0 }}>
            <div className="udt-utils-row" style={{ cursor: 'default' }}>
              <span className="udt-utils-row__label" style={{ flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                <Flash24Regular />
                {t('wpf.statusTrayPopuppowerMode')}
              </span>
              <span className="udt-utils-row__value" style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                <span
                  style={{
                    width: 10,
                    height: 10,
                    borderRadius: '50%',
                    background: data?.powerMode ? '#4caf50' : 'transparent',
                    display: 'inline-block'
                  }}
                />
                {powerModeLabel}
              </span>
            </div>
            {data?.powerMode === 'GodMode' &&
              renderRow(t('wpf.statusTrayPopuppreset'), data.godModePresetName ?? '-')}
            {renderRow(t('wpf.sensorsControlcputitle'), data?.cpuSummary)}
            {renderRow(t('wpf.deviceInformationWindowmemorytitle'), data?.memorySummary)}
            {renderRow(t('wpf.sensorsControlssdTemperaturetitle'), data?.ssdSummary)}
          </div>

          {hasGpuBlock && (
            <div className="udt-utils-card" style={{ padding: 0 }}>
              <div className="udt-utils-row" style={{ cursor: 'default' }}>
                <span className="udt-utils-row__label" style={{ flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                  {t('wpf.statusTrayPopupdiscreteGPU')}
                </span>
                <span className="udt-utils-row__value" style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                  {gpuDot != null && (
                    <span style={{ width: 10, height: 10, borderRadius: '50%', background: gpuDot, display: 'inline-block' }} />
                  )}
                  {gpuActiveLabel ?? '-'}
                </span>
              </div>
              {data?.gpuState != null &&
                renderRow(t('wpf.statusTrayPopuppowerState'), data.gpuPerformanceState ?? '-')}
              {renderRow(t('wpf.sensorsControlgputitle'), data?.gpuSummary)}
            </div>
          )}

          <div className="udt-utils-card" style={{ padding: 0 }}>
            <div className="udt-utils-row" style={{ cursor: 'default' }}>
              <span className="udt-utils-row__label" style={{ flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                <Phone24Regular />
                {t('wpf.statusTrayPopupbattery')}
              </span>
              <span className="udt-utils-row__value">
                {data?.batteryPercent != null ? `${Math.round(data.batteryPercent)}%` : '-'}
              </span>
            </div>
            {renderRow(t('wpf.statusTrayPopupmode'), batteryStateLabel)}
            {!data?.isCompatibilityMode && renderRow(t('wpf.statusTrayPopupdischargeRate'), formatSignedWatts(data?.dischargeRate))}
            {!data?.isCompatibilityMode && renderRow(t('wpf.statusTrayPopupminDischargeRate'), formatSignedWatts(data?.minDischargeRate))}
            {!data?.isCompatibilityMode && renderRow(t('wpf.statusTrayPopupmaxDischargeRate'), formatSignedWatts(data?.maxDischargeRate))}
            {renderRow(t('wpf.statusTrayPopupusageTime'), '-')}
          </div>

          {data?.hasUpdate && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 6,
                padding: '8px 4px',
                marginBottom: 6,
                background: 'rgba(76, 175, 80, 0.18)',
                color: 'var(--udt-text-primary)',
                borderRadius: '0 0 12px 12px',
                fontSize: 12
              }}
            >
              <ArrowSync24Regular /> {t('wpf.statusTrayPopupupdateAvailable')}
            </div>
          )}
        </div>
        <div className="udt-utils-modal__actions" style={{ paddingTop: 8 }}>
          <button type="button" className="udt-utils-button udt-utils-button--primary" onClick={settle}>
            {t('wpf.close')}
          </button>
        </div>
      </div>
    </div>
  )
}
