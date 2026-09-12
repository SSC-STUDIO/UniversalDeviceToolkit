/**
 * Per-family trigger parameter editors — port of Electron
 * Windows/Automation/TabItemContent/*.xaml.cs
 * (IAutomationPipelineTriggerTabItemContent implementations).
 */
import { useEffect, useMemo, useState } from 'react'
import { Delete24Regular, Add24Regular } from '../icons/fluent'
import { Select } from 'antd'
import { useTranslation } from 'react-i18next'
import { automationApi } from '../../api/automation'
import type { AutomationTrigger } from './triggers'
import { parseTimeSpanSeconds, formatTimeSpan } from './triggers'

export interface TriggerEditorProps {
  trigger: AutomationTrigger
  onChange: (next: AutomationTrigger) => void
}

const DAY_KEYS = [1, 2, 3, 4, 5, 6, 0]

function DropdownField(props: {
  label: string
  value: string
  options: string[]
  onChange: (value: string) => void
}): React.JSX.Element {
  return (
    <label className="udt-trigger-field">
      <span className="udt-trigger-field__label">{props.label}</span>
      <Select
        className="udt-select"
        value={props.value}
        onChange={props.onChange}
        options={props.options.map((option) => ({
          value: option,
          label: option
        }))}
      />
    </label>
  )
}

function NumberField(props: {
  label: string
  value: number
  min?: number
  max?: number
  step?: number
  onChange: (value: number) => void
}): React.JSX.Element {
  return (
    <label className="udt-trigger-field">
      <span className="udt-trigger-field__label">{props.label}</span>
      <input
        type="number"
        className="udt-input udt-number"
        value={props.value}
        min={props.min}
        max={props.max}
        step={props.step}
        onChange={(e) => props.onChange(Number(e.target.value))}
      />
    </label>
  )
}

function SecondsField(props: {
  label: string
  value: number
  onChange: (seconds: number) => void
}): React.JSX.Element {
  return (
    <NumberField
      label={props.label}
      value={props.value}
      min={0}
      step={1}
      onChange={props.onChange}
    />
  )
}

function RadioList(props: {
  options: Array<{ value: string; label: string }>
  selected: string
  onChange: (value: string) => void
}): React.JSX.Element {
  return (
    <div className="udt-trigger-radios">
      {props.options.map((option) => (
        <label key={option.value} className="udt-trigger-radio">
          <input
            type="radio"
            name={`udt-trigger-${option.value}`}
            checked={props.selected === option.value}
            onChange={() => props.onChange(option.value)}
          />
          <span>{option.label}</span>
        </label>
      ))}
    </div>
  )
}

/** When Power Mode is changed → radio list of available power modes. */
export function PowerModeEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const [states, setStates] = useState<string[]>([])
  const current = String(trigger.powerModeState ?? 'Balance')

  useEffect(() => {
    void automationApi
      .getFeatureStates('powerMode')
      .then((result) => {
        const extracted = (result.states ?? [])
          .map((s) => {
            const value = s as Record<string, unknown>
            return (value.powerModeState ?? value['PowerModeState'] ?? value.value) as string | undefined
          })
          .filter((s): s is string => typeof s === 'string' && s.length > 0)
        if (extracted.length > 0) setStates(extracted)
      })
      .catch(() => {
        // feature probe failed; fall back to the static list below
      })
  }, [])

  const options = useMemo(() => {
    const known = states.length > 0 ? states : ['Quiet', 'Balance', 'Performance', 'Extreme', 'GodMode']
    const unique = [...new Set([current, ...known])]
    return unique.map((value) => ({ value, label: value }))
  }, [states, current])

  return (
    <RadioList
      options={options}
      selected={current}
      onChange={(value) => onChange({ ...trigger, powerModeState: value })}
    />
  )
}

/** When Custom Mode preset changes → radio list of god mode presets. */
export function GodModePresetEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const [presets, setPresets] = useState<Array<{ id: string; name: string }>>([])
  const current = String(trigger.presetId ?? '00000000-0000-0000-0000-000000000000')

  useEffect(() => {
    void automationApi
      .getGodModePresets()
      .then((result) => {
        const value = result.value as { presets?: Record<string, { name?: string }>; Presets?: Record<string, { name?: string }> } | null
        const raw = value?.presets ?? value?.Presets
        if (raw == null) return
        setPresets(
          Object.entries(raw)
            .map(([id, preset]) => ({ id, name: preset.name ?? id }))
            .sort((a, b) => a.name.localeCompare(b.name))
        )
      })
      .catch(() => {
        // presets unavailable; only the empty preset remains selectable
      })
  }, [])

  const options = useMemo(() => {
    const empty: Array<{ value: string; label: string }> = []
    if (current === '00000000-0000-0000-0000-000000000000') {
      empty.push({ value: '00000000-0000-0000-0000-000000000000', label: '-' })
    }
    return [
      ...empty,
      ...presets.map((preset) => ({ value: preset.id, label: preset.name })),
    ]
  }, [presets, current])

  return (
    <RadioList
      options={options}
      selected={current}
      onChange={(value) => onChange({ ...trigger, presetId: value })}
    />
  )
}

/** Periodic action → period in minutes. */
export function PeriodicEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const { t } = useTranslation()
  const periodMinutes = Math.round(parseTimeSpanSeconds(String(trigger.period ?? '00:01:00')) / 60)
  return (
    <div className="udt-trigger-fields">
      <NumberField
        label={t('wpf.periodicActionPipelineTriggerTabItemContentperiodMinutes')}
        value={periodMinutes}
        min={1}
        onChange={(minutes) => onChange({ ...trigger, period: formatTimeSpan(Math.max(1, minutes) * 60) })}
      />
    </div>
  )
}

export interface ProcessEntry {
  name?: string
  executablePath?: string
}

/** When app starts / closes → process list editor. */
export function ProcessesEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const { t } = useTranslation()
  const [selected, setSelected] = useState<string>('')
  const processes = useMemo<ProcessEntry[]>(
    () => (Array.isArray(trigger.processes) ? (trigger.processes as ProcessEntry[]) : []),
    [trigger.processes]
  )

  const addProcess = (name: string, executablePath: string): void => {
    const trimmed = name.trim()
    if (trimmed === '') return
    if (processes.some((p) => (p.name ?? '').toLowerCase() === trimmed.toLowerCase())) return
    onChange({
      ...trigger,
      processes: [...processes, { name: trimmed, executablePath: executablePath || trimmed }],
    })
    setSelected('')
  }

  const browse = async (): Promise<void> => {
    const bridge = window.bridge
    if (bridge == null || bridge.selectExeFile == null) return
    try {
      const path = await bridge.selectExeFile()
      if (path == null) return
      const name = path.split(/[\\/]/).pop() ?? path
      addProcess(name, path)
    } catch {
      // dialog unavailable; fall back to manual entry
    }
  }

  const removeProcess = (index: number): void => {
    onChange({
      ...trigger,
      processes: processes.filter((_, i) => i !== index),
    })
  }

  const clearAll = (): void => {
    onChange({ ...trigger, processes: [] })
  }

  return (
    <div className="udt-trigger-processes">
      <div className="udt-trigger-processes__add">
        <input
          className="udt-input"
          value={selected}
          placeholder={t('wpf.commonExecutableFileDialogFilter', { defaultValue: 'Process name or executable path' }).replace(/^.*\((\*\.[a-z]+)\)$/i, '$1')}
          onChange={(e) => setSelected(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') addProcess(selected, selected)
          }}
        />
        <button
          type="button"
          className="udt-btn udt-btn--secondary udt-btn--sm"
          disabled={!selected.trim()}
          onClick={() => addProcess(selected, selected)}
        >
          <Add24Regular /> {t('wpf.open', { defaultValue: 'Add' })}
        </button>
        {window.bridge?.selectExeFile != null && (
          <button type="button" className="udt-btn udt-btn--secondary udt-btn--sm" onClick={() => void browse()}>
            {t('wpf.browse', { defaultValue: 'Browse…' })}
          </button>
        )}
      </div>
      {processes.length > 0 ? (
        <div className="udt-trigger-processes__list">
          {processes.map((process, index) => (
            <div key={`${process.name ?? ''}-${index}`} className="udt-trigger-process-row">
              <div className="udt-trigger-process-row__copy">
                <div className="udt-trigger-process-row__name">{process.name}</div>
                {process.executablePath != null && process.executablePath !== process.name && (
                  <div className="udt-trigger-process-row__path">{process.executablePath}</div>
                )}
              </div>
              <button
                type="button"
                className="udt-icon-btn udt-icon-btn--danger"
                aria-label={t('automation.deleteStep')}
                title={t('automation.deleteStep')}
                onClick={() => removeProcess(index)}
              >
                <Delete24Regular />
              </button>
            </div>
          ))}
          <button type="button" className="udt-btn udt-btn--secondary udt-btn--sm" onClick={clearAll}>
            {t('wpf.deleteAll', { defaultValue: 'Clear all' })}
          </button>
        </div>
      ) : (
        <div className="udt-trigger-field__empty">{t('automation.triggerEditors.noProcesses', { defaultValue: 'No processes selected.' })}</div>
      )}
    </div>
  )
}

const INACTIVITY_OPTIONS = [10, 30, 60, 120, 180, 300, 600, 900, 1800]

/** When user becomes inactive → timeout dropdown. */
export function UserInactivityEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const { t } = useTranslation()
  const currentSeconds = parseTimeSpanSeconds(String(trigger.inactivityTimeSpan ?? '00:00:30'))
  const options = useMemo(() => {
    const all = [...new Set([currentSeconds > 0 ? currentSeconds : 30, ...INACTIVITY_OPTIONS])].sort((a, b) => a - b)
    return all.map((seconds) => ({
      value: seconds,
      label:
        seconds < 60
          ? t('automation.triggerEditors.seconds', { count: seconds, defaultValue: `${seconds} seconds` })
          : seconds < 3600
            ? t('automation.triggerEditors.minutes', { count: Math.round(seconds / 60), defaultValue: `${Math.round(seconds / 60)} minutes` })
            : t('automation.triggerEditors.hours', { count: Math.round(seconds / 3600), defaultValue: `${Math.round(seconds / 3600)} hours` }),
    }))
  }, [currentSeconds, t])

  return (
    <div className="udt-trigger-fields">
      <label className="udt-trigger-field">
        <span className="udt-trigger-field__label">{t('automation.triggerEditors.inactivityTimeout', { defaultValue: 'Timeout' })}</span>
        <Select<number>
          className="udt-select"
          value={currentSeconds}
          onChange={(value) => onChange({ ...trigger, inactivityTimeSpan: formatTimeSpan(value) })}
          options={options.map((option) => ({
            value: option.value,
            label: option.label
          }))}
        />
      </label>
    </div>
  )
}

/** When Wi-Fi is connected → SSID list editor. */
export function WiFiEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const { t } = useTranslation()
  const ssids = useMemo<string[]>(
    () => (Array.isArray(trigger.ssids) ? (trigger.ssids as string[]) : []),
    [trigger.ssids]
  )

  const update = (index: number, value: string): void => {
    const next = [...ssids]
    next[index] = value
    onChange({ ...trigger, ssids: next })
  }

  const remove = (index: number): void => {
    const next = ssids.filter((_, i) => i !== index)
    if (next.length === 0) next.push('')
    onChange({ ...trigger, ssids: next })
  }

  const add = (): void => {
    onChange({ ...trigger, ssids: [...ssids, ''] })
  }

  const items = ssids.length === 0 ? [''] : ssids

  return (
    <div className="udt-trigger-fields">
      {items.map((ssid, index) => (
        <div key={index} className="udt-trigger-ssid-row">
          <input
            className="udt-input"
            value={ssid}
            placeholder={t('automation.triggerEditors.ssidPlaceholder', { defaultValue: 'Network name (SSID)' })}
            onChange={(e) => update(index, e.target.value)}
          />
          <button
            type="button"
            className="udt-icon-btn udt-icon-btn--danger"
            aria-label={t('automation.deleteStep')}
            title={t('automation.deleteStep')}
            onClick={() => remove(index)}
          >
            <Delete24Regular />
          </button>
        </div>
      ))}
      <div>
        <button type="button" className="udt-btn udt-btn--secondary udt-btn--sm" onClick={add}>
          <Add24Regular /> {t('automation.triggerEditors.addSsid', { defaultValue: 'Add network name' })}
        </button>
      </div>
    </div>
  )
}

/** At specified time → sunrise/sunset/time radios + day-of-week checkboxes. */
export function TimeEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const { t } = useTranslation()
  const isSunrise = trigger.isSunrise === true
  const isSunset = trigger.isSunset === true
  const time = trigger.time as { hour?: unknown; minute?: unknown } | null | undefined
  const hour = time != null && time.hour !== undefined ? Number(time.hour) : new Date().getHours()
  const minute = time != null && time.minute !== undefined ? Number(time.minute) : new Date().getMinutes()
  const days = Array.isArray(trigger.days) ? (trigger.days as number[]) : []
  const hasAllDays = DAY_KEYS.every((day) => days.includes(day)) || days.length === 0

  const setSunrise = (value: boolean): void => {
    onChange({ ...trigger, isSunrise: value, isSunset: value ? false : isSunset, time: value ? null : time })
  }
  const setSunset = (value: boolean): void => {
    onChange({ ...trigger, isSunset: value, isSunrise: value ? false : isSunrise, time: value ? null : time })
  }
  const setTime = (value: boolean): void => {
    onChange({ ...trigger, isSunrise: false, isSunset: false, time: value ? { hour, minute } : null })
  }

  const toggleDay = (day: number): void => {
    const next = days.includes(day) ? days.filter((d) => d !== day) : [...days, day]
    onChange({ ...trigger, days: next })
  }

  return (
    <div className="udt-trigger-fields">
      <div className="udt-trigger-radios">
        <label className="udt-trigger-radio">
          <input type="radio" name="udt-time-mode" checked={isSunrise} onChange={() => setSunrise(true)} />
          <span>{t('wpf.automationPipelineControlsubtitlePartatSunrise')}</span>
        </label>
        <label className="udt-trigger-radio">
          <input type="radio" name="udt-time-mode" checked={isSunset} onChange={() => setSunset(true)} />
          <span>{t('wpf.automationPipelineControlsubtitlePartatSunset')}</span>
        </label>
        <label className="udt-trigger-radio">
          <input type="radio" name="udt-time-mode" checked={time != null} onChange={() => setTime(true)} />
          <span>{t('automation.triggerEditors.atTime', { defaultValue: 'At time' })}</span>
        </label>
      </div>
      {time != null && (
        <div className="udt-trigger-fields udt-trigger-fields--row">
          <NumberField
            label={t('automation.triggerEditors.hour', { defaultValue: 'Hour' })}
            value={hour}
            min={0}
            max={23}
            onChange={(h) => onChange({ ...trigger, time: { hour: h, minute } })}
          />
          <NumberField
            label={t('automation.triggerEditors.minute', { defaultValue: 'Minute' })}
            value={minute}
            min={0}
            max={59}
            onChange={(m) => onChange({ ...trigger, time: { hour, minute: m } })}
          />
        </div>
      )}
      <div className="udt-trigger-days">
        {DAY_KEYS.map((day) => (
          <label key={day} className="udt-trigger-check">
            <input
              type="checkbox"
              checked={days.includes(day)}
              disabled={hasAllDays && days.includes(day) && days.length === DAY_KEYS.length}
              onChange={() => toggleDay(day)}
            />
            <span>{t(`automation.triggerEditors.day.${day}`, { defaultValue: dayName(day) })}</span>
          </label>
        ))}
      </div>
      {hasAllDays && <div className="udt-trigger-field__hint">{t('automation.triggerEditors.allDays', { defaultValue: 'Every day' })}</div>}
    </div>
  )
}

function dayName(day: number): string {
  return ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'][day] ?? ''
}

const SENSOR_METRICS = ['CpuTemperature', 'CpuLoad', 'GpuTemperature', 'GpuLoad', 'MemoryLoad', 'FanSpeed', 'BatteryLevel']
const COMPARISONS = ['GreaterThanOrEqual', 'LessThanOrEqual', 'GreaterThan', 'LessThan', 'Equal', 'NotEqual']

/** Hardware sensor trigger → metric/comparison/threshold/duration/cooldown. */
export function HardwareSensorEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const { t } = useTranslation()
  return (
    <div className="udt-trigger-fields">
      <DropdownField
        label={t('automation.triggerEditors.metric', { defaultValue: 'Metric' })}
        value={String(trigger.metric ?? 'CpuTemperature')}
        options={SENSOR_METRICS}
        onChange={(metric) => onChange({ ...trigger, metric })}
      />
      <DropdownField
        label={t('automation.triggerEditors.comparison', { defaultValue: 'Comparison' })}
        value={String(trigger.comparison ?? 'GreaterThanOrEqual')}
        options={COMPARISONS}
        onChange={(comparison) => onChange({ ...trigger, comparison })}
      />
      <NumberField
        label={t('automation.triggerEditors.threshold', { defaultValue: 'Threshold' })}
        value={Number(trigger.threshold ?? 90)}
        min={0}
        max={110}
        step={1}
        onChange={(threshold) => onChange({ ...trigger, threshold })}
      />
      <SecondsField
        label={t('automation.triggerEditors.durationSeconds', { defaultValue: 'Duration (seconds)' })}
        value={parseTimeSpanSeconds(String(trigger.duration ?? '00:00:05'))}
        onChange={(duration) => onChange({ ...trigger, duration: formatTimeSpan(duration) })}
      />
      <SecondsField
        label={t('automation.triggerEditors.cooldownSeconds', { defaultValue: 'Cooldown (seconds)' })}
        value={parseTimeSpanSeconds(String(trigger.cooldown ?? '00:01:00'))}
        onChange={(cooldown) => onChange({ ...trigger, cooldown: formatTimeSpan(cooldown) })}
      />
    </div>
  )
}

const BATTERY_COMPARISONS = ['BelowOrEqual', 'AboveOrEqual']
const CHARGE_FILTERS = ['Any', 'Charging', 'Discharging']

/** Battery percentage trigger → comparison/threshold/duration/cooldown/charge filter. */
export function BatteryPercentageEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const { t } = useTranslation()
  return (
    <div className="udt-trigger-fields">
      <DropdownField
        label={t('automation.triggerEditors.comparison', { defaultValue: 'Comparison' })}
        value={String(trigger.comparison ?? 'BelowOrEqual')}
        options={BATTERY_COMPARISONS}
        onChange={(comparison) => onChange({ ...trigger, comparison })}
      />
      <NumberField
        label={t('automation.triggerEditors.thresholdPercent', { defaultValue: 'Threshold (%)' })}
        value={Number(trigger.threshold ?? 20)}
        min={0}
        max={100}
        step={1}
        onChange={(threshold) => onChange({ ...trigger, threshold })}
      />
      <DropdownField
        label={t('automation.triggerEditors.chargeFilter', { defaultValue: 'Charge filter' })}
        value={String(trigger.chargeFilter ?? 'Any')}
        options={CHARGE_FILTERS}
        onChange={(chargeFilter) => onChange({ ...trigger, chargeFilter })}
      />
      <SecondsField
        label={t('automation.triggerEditors.durationSeconds', { defaultValue: 'Duration (seconds)' })}
        value={parseTimeSpanSeconds(String(trigger.duration ?? '00:00:05'))}
        onChange={(duration) => onChange({ ...trigger, duration: formatTimeSpan(duration) })}
      />
      <SecondsField
        label={t('automation.triggerEditors.cooldownSeconds', { defaultValue: 'Cooldown (seconds)' })}
        value={parseTimeSpanSeconds(String(trigger.cooldown ?? '00:05:00'))}
        onChange={(cooldown) => onChange({ ...trigger, cooldown: formatTimeSpan(cooldown) })}
      />
    </div>
  )
}

/** When device is connected / disconnected → device instance IDs. */
export function DeviceEditor({ trigger, onChange }: TriggerEditorProps): React.JSX.Element {
  const { t } = useTranslation()
  const [selected, setSelected] = useState('')
  const instanceIds = useMemo<string[]>(
    () => (Array.isArray(trigger.instanceIds) ? (trigger.instanceIds as string[]) : []),
    [trigger.instanceIds]
  )

  const add = (): void => {
    const id = selected.trim()
    if (id === '') return
    if (instanceIds.includes(id)) return
    onChange({ ...trigger, instanceIds: [...instanceIds, id] })
    setSelected('')
  }

  const remove = (index: number): void => {
    onChange({ ...trigger, instanceIds: instanceIds.filter((_, i) => i !== index) })
  }

  return (
    <div className="udt-trigger-fields">
      <div className="udt-trigger-processes__add">
        <input
          className="udt-input"
          value={selected}
          placeholder={t('automation.triggerEditors.deviceInstanceId', { defaultValue: 'Device instance ID' })}
          onChange={(e) => setSelected(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') add()
          }}
        />
        <button
          type="button"
          className="udt-btn udt-btn--secondary udt-btn--sm"
          disabled={!selected.trim()}
          onClick={add}
        >
          <Add24Regular /> {t('wpf.open', { defaultValue: 'Add' })}
        </button>
      </div>
      {instanceIds.length > 0 ? (
        <div className="udt-trigger-processes__list">
          {instanceIds.map((id, index) => (
            <div key={`${id}-${index}`} className="udt-trigger-process-row">
              <div className="udt-trigger-process-row__path">{id}</div>
              <button
                type="button"
                className="udt-icon-btn udt-icon-btn--danger"
                aria-label={t('automation.deleteStep')}
                title={t('automation.deleteStep')}
                onClick={() => remove(index)}
              >
                <Delete24Regular />
              </button>
            </div>
          ))}
        </div>
      ) : (
        <div className="udt-trigger-field__empty">
          {t('automation.triggerEditors.noDevices', { defaultValue: 'No devices selected.' })}
        </div>
      )}
    </div>
  )
}
