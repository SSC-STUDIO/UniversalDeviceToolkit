import { useEffect, useMemo, useState } from 'react'
import { Button, Dropdown, Input, InputNumber, Modal, Select, Spin, Switch, message } from 'antd'
import {
  Delete24Regular,
  ChevronDown24Regular,
  Edit24Regular,
  Add24Regular,
  ArrowCounterclockwise24Regular
} from '../icons/fluent'
import { useTranslation } from 'react-i18next'
import {
  addPreset,
  deleteActivePreset,
  getUniquePresetName,
  godModeApi,
  renameActivePreset,
  type GodModeDefaults,
  type GodModePreset,
  type GodModeStore
} from '../../api/godMode'
import { useFeaturesStore } from '../../stores/featuresStore'
import FanCurveEditor from '../FanCurveEditor'
import GodModeValueControl from '../dashboard/GodModeValueControl'
import InfoBar from '../InfoBar'

/**
 * Parity modal for WPF Windows/Dashboard/GodModeSettingsWindow.
 * Business logic lives in Host GodModeController (godMode.getState / setState / apply).
 */

type StepperField =
  | 'cpuLongTermPowerLimit'
  | 'cpuShortTermPowerLimit'
  | 'cpuPeakPowerLimit'
  | 'cpuCrossLoadingPowerLimit'
  | 'cpuPL1Tau'
  | 'apUsPPTPowerLimit'
  | 'cpuTemperatureLimit'
  | 'gpuPowerBoost'
  | 'gpuConfigurableTGP'
  | 'gpuTemperatureLimit'
  | 'gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline'
  | 'gpuToCPUDynamicBoost'

const CPU_FIELDS: StepperField[] = [
  'cpuLongTermPowerLimit',
  'cpuShortTermPowerLimit',
  'cpuPeakPowerLimit',
  'cpuCrossLoadingPowerLimit',
  'cpuPL1Tau',
  'apUsPPTPowerLimit',
  'cpuTemperatureLimit'
]

const GPU_FIELDS: StepperField[] = [
  'gpuPowerBoost',
  'gpuConfigurableTGP',
  'gpuTemperatureLimit',
  'gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline',
  'gpuToCPUDynamicBoost'
]

const STEPPER_TITLE_KEYS: Record<StepperField, string> = {
  cpuLongTermPowerLimit: 'godMode.cpu.longTermPL',
  cpuShortTermPowerLimit: 'godMode.cpu.shortTermPL',
  cpuPeakPowerLimit: 'godMode.cpu.peakPL',
  cpuCrossLoadingPowerLimit: 'godMode.cpu.crossLoading',
  cpuPL1Tau: 'godMode.cpu.pl1Tau',
  apUsPPTPowerLimit: 'godMode.cpu.apuSppt',
  cpuTemperatureLimit: 'godMode.cpu.tempLimit',
  gpuPowerBoost: 'godMode.gpu.dynamicBoost',
  gpuConfigurableTGP: 'godMode.gpu.ctgp',
  gpuTemperatureLimit: 'godMode.gpu.tempLimit',
  gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline: 'godMode.gpu.totalProcessingPowerTarget',
  gpuToCPUDynamicBoost: 'godMode.gpu.toCpuDynamicBoost'
}

const STEPPER_DESC_KEYS: Record<StepperField, string> = {
  cpuLongTermPowerLimit: 'godMode.cpu.longTermPL.desc',
  cpuShortTermPowerLimit: 'godMode.cpu.shortTermPL.desc',
  cpuPeakPowerLimit: 'godMode.cpu.peakPL.desc',
  cpuCrossLoadingPowerLimit: 'godMode.cpu.crossLoading.desc',
  cpuPL1Tau: 'godMode.cpu.pl1Tau.desc',
  apUsPPTPowerLimit: 'godMode.cpu.apuSppt.desc',
  cpuTemperatureLimit: 'godMode.cpu.tempLimit.desc',
  gpuPowerBoost: 'godMode.gpu.dynamicBoost.desc',
  gpuConfigurableTGP: 'godMode.gpu.ctgp.desc',
  gpuTemperatureLimit: 'godMode.gpu.tempLimit.desc',
  gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline: 'godMode.gpu.totalProcessingPowerTarget.desc',
  gpuToCPUDynamicBoost: 'godMode.gpu.toCpuDynamicBoost.desc'
}

const STEPPER_UNITS: Partial<Record<StepperField, string>> = {
  cpuLongTermPowerLimit: 'W',
  cpuShortTermPowerLimit: 'W',
  cpuPeakPowerLimit: 'W',
  cpuCrossLoadingPowerLimit: 'W',
  cpuPL1Tau: 's',
  apUsPPTPowerLimit: 'W',
  cpuTemperatureLimit: '\u00B0C',
  gpuPowerBoost: 'W',
  gpuConfigurableTGP: 'W',
  gpuTemperatureLimit: '\u00B0C',
  gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline: 'W',
  gpuToCPUDynamicBoost: 'W'
}

const POWER_MODE_LABEL_KEYS: Record<string, string> = {
  Quiet: 'powerModeStateQuiet',
  Balance: 'powerModeStateBalance',
  Performance: 'powerModeStatePerformance',
  Extreme: 'powerModeStateExtreme',
  GodMode: 'powerModeStateGodMode'
}

interface WorkingValues {
  steppers: Partial<Record<StepperField, number>>
  fanTable: number[] | null
  fanFullSpeed: boolean | null
  maxValueOffset: number | null
  minValueOffset: number | null
}

function emptyWorkingValues(): WorkingValues {
  return { steppers: {}, fanTable: null, fanFullSpeed: null, maxValueOffset: null, minValueOffset: null }
}

function workingFromPreset(preset: GodModePreset): WorkingValues {
  const working = emptyWorkingValues()
  for (const field of [...CPU_FIELDS, ...GPU_FIELDS]) {
    const stepper = preset[field]
    if (stepper != null) working.steppers[field] = stepper.value
  }
  working.fanTable = preset.fanTable
  working.fanFullSpeed = preset.fanFullSpeed
  working.maxValueOffset = preset.maxValueOffset
  working.minValueOffset = preset.minValueOffset
  return working
}

/** WPF GodModeSettingsWindow.BuildActivePresetFromControls (store side only). */
function flushWorkingPreset(preset: GodModePreset, working: WorkingValues): GodModePreset {
  const next: GodModePreset = { ...preset }
  for (const field of [...CPU_FIELDS, ...GPU_FIELDS]) {
    const stepper = next[field]
    const value = working.steppers[field]
    if (stepper != null && value != null) {
      next[field] = { ...stepper, value }
    }
  }
  next.fanTable = working.fanTable
  next.fanFullSpeed = working.fanFullSpeed
  next.maxValueOffset = working.maxValueOffset
  next.minValueOffset = working.minValueOffset
  return next
}

function storeWithPreset(store: GodModeStore, preset: GodModePreset): GodModeStore {
  return { ...store, presets: { ...store.presets, [store.activePresetId]: preset } }
}

/** WPF CardHeaderControl warning text: strip the first line when it looks like a heading. */
function removeWarningHeading(messageText: string): string {
  const lines = messageText.replace(/\r\n/g, '\n').split('\n')
  if (lines.length > 1) {
    const heading = lines[0].trim()
    if (
      heading.length <= 32 &&
      (heading.includes('!') || heading.includes('\uFF01') ||
        heading.endsWith(':') || heading.endsWith('\uFF1A'))
    ) {
      return lines.slice(1).join('\n').trim()
    }
  }
  return messageText.trim()
}

/** WPF TryNormalizeOffsetValue: whole number within [min, max]. */
function normalizeOffset(raw: number | null, minimum: number, maximum: number): number | null {
  if (raw == null || !Number.isFinite(raw)) return null
  if (raw < minimum || raw > maximum || raw !== Math.trunc(raw)) return null
  return raw
}

function applyDefaultsToWorking(
  working: WorkingValues,
  defaults: GodModeDefaults,
  activePreset: GodModePreset
): WorkingValues {
  const steppers: Partial<Record<StepperField, number>> = { ...working.steppers }
  for (const field of [...CPU_FIELDS, ...GPU_FIELDS]) {
    if (activePreset[field] == null) continue
    const value = defaults[field]
    if (value != null) steppers[field] = value
  }
  return {
    steppers,
    fanTable: activePreset.fanTable != null && defaults.fanTable != null
      ? defaults.fanTable
      : working.fanTable,
    fanFullSpeed: working.fanFullSpeed != null && defaults.fanFullSpeed != null
      ? defaults.fanFullSpeed
      : working.fanFullSpeed,
    maxValueOffset: working.maxValueOffset != null ? 0 : null,
    minValueOffset: working.minValueOffset != null ? 0 : null
  }
}

interface NamePromptState {
  mode: 'add' | 'rename'
}

interface GodModeSettingsModalProps {
  open: boolean
  onClose: () => void
  onSaved?: () => void
}

export default function GodModeSettingsModal({
  open,
  onClose,
  onSaved
}: GodModeSettingsModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [store, setStore] = useState<GodModeStore | null>(null)
  const [working, setWorking] = useState<WorkingValues>(emptyWorkingValues())
  const [minimumFanTable, setMinimumFanTable] = useState<number[] | null>(null)
  const [defaultFanTable, setDefaultFanTable] = useState<number[] | null>(null)
  const [defaults, setDefaults] = useState<Record<string, GodModeDefaults>>({})
  const [warnVantage, setWarnVantage] = useState(false)
  const [warnLegionZone, setWarnLegionZone] = useState(false)
  const [namePrompt, setNamePrompt] = useState<NamePromptState | null>(null)
  const [nameInput, setNameInput] = useState('')

  useEffect(() => {
    if (!open) return
    let cancelled = false
    setLoading(true)
    godModeApi
      .load()
      .then((loaded) => {
        if (cancelled) return
        if (loaded == null) {
          setStore(null)
          return
        }
        setStore(loaded.store)
        setMinimumFanTable(loaded.minimumFanTable)
        setDefaultFanTable(loaded.defaultFanTable)
        setDefaults(loaded.defaults)
        setWarnVantage(loaded.warnVantage)
        setWarnLegionZone(loaded.warnLegionZone)
        const preset = loaded.store.presets[loaded.store.activePresetId]
        if (preset != null) setWorking(workingFromPreset(preset))
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          void message.error(`${t('godMode.errorLoad')}: ${(reason as Error).message}`)
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [open, t])

  const presetList = store == null
    ? []
    : Object.entries(store.presets).sort((a, b) => a[1].name.localeCompare(b[1].name))
  const activePreset = store == null ? undefined : store.presets[store.activePresetId]

  const cpuVisible = activePreset != null && CPU_FIELDS.some((field) => activePreset[field] != null)
  const gpuVisible = activePreset != null && GPU_FIELDS.some((field) => activePreset[field] != null)
  const fanCurveVisible = activePreset != null && working.fanTable != null
  const fanFullSpeedVisible = activePreset != null && working.fanFullSpeed != null
  const fanSectionVisible = fanCurveVisible || fanFullSpeedVisible
  const advancedVisible = activePreset != null &&
    (working.maxValueOffset != null || working.minValueOffset != null)

  const loadMenuItems = useMemo(
    () =>
      Object.entries(defaults)
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([mode, modeDefaults]) => ({
          key: mode,
          label: t(POWER_MODE_LABEL_KEYS[mode] ?? mode, { defaultValue: mode }),
          onClick: () => {
            if (activePreset == null) return
            setWorking((prev) => applyDefaultsToWorking(prev, modeDefaults, activePreset))
          }
        })),
    [defaults, t, activePreset]
  )

  function updateStepper(field: StepperField, value: number): void {
    setWorking((prev) => {
      const steppers: Partial<Record<StepperField, number>> = { ...prev.steppers, [field]: value }
      if (field === 'cpuLongTermPowerLimit') {
        const short = steppers.cpuShortTermPowerLimit
        if (short != null && value > short) steppers.cpuShortTermPowerLimit = value
      }
      if (field === 'cpuShortTermPowerLimit') {
        const long = steppers.cpuLongTermPowerLimit
        if (long != null && value < long) steppers.cpuLongTermPowerLimit = value
      }
      return { ...prev, steppers }
    })
  }

  async function persist(flushed: GodModeStore, apply: boolean): Promise<GodModeStore> {
    const next = await godModeApi.save(flushed, apply)
    setStore(next)
    return next
  }

  async function handlePresetSwitch(id: string): Promise<void> {
    if (store == null || activePreset == null || id === store.activePresetId) return
    const flushed = storeWithPreset(store, flushWorkingPreset(activePreset, working))
    const next = { ...flushed, activePresetId: id }
    try {
      const powerState = useFeaturesStore.getState().states.powerMode
      const saved = await persist(next, powerState === 'GodMode')
      const preset = saved.presets[id]
      if (preset != null) setWorking(workingFromPreset(preset))
    } catch (reason) {
      void message.error(`${t('godMode.errorApply')}: ${(reason as Error).message}`)
    }
  }

  function openNamePrompt(mode: 'add' | 'rename'): void {
    if (mode === 'rename') {
      setNameInput(activePreset?.name ?? '')
    } else {
      const defaultName = getUniquePresetName(
        t('godMode.defaultPresetName'),
        store?.presets ?? {}
      )
      setNameInput(defaultName)
    }
    setNamePrompt({ mode })
  }

  async function confirmNamePrompt(): Promise<void> {
    if (namePrompt == null || store == null || activePreset == null) return
    const name = nameInput.trim()
    setNamePrompt(null)
    if (name.length === 0) return

    try {
      let next: GodModeStore
      if (namePrompt.mode === 'add') {
        const flushed = storeWithPreset(store, flushWorkingPreset(activePreset, working))
        next = addPreset(flushed, name)
      } else {
        next = renameActivePreset(store, name)
      }
      const saved = await persist(next, false)
      if (namePrompt.mode === 'add') {
        const preset = saved.presets[saved.activePresetId]
        if (preset != null) setWorking(workingFromPreset(preset))
      }
    } catch (reason) {
      void message.error(`${t('godMode.errorApply')}: ${(reason as Error).message}`)
    }
  }

  async function handleDeletePreset(): Promise<void> {
    if (store == null || Object.keys(store.presets).length <= 1) return
    const next = deleteActivePreset(store)
    try {
      const powerState = useFeaturesStore.getState().states.powerMode
      const saved = await persist(next, powerState === 'GodMode')
      const preset = saved.presets[saved.activePresetId]
      if (preset != null) setWorking(workingFromPreset(preset))
    } catch (reason) {
      void message.error(`${t('godMode.errorApply')}: ${(reason as Error).message}`)
    }
  }

  function handleDefaultFanCurve(): void {
    if (defaultFanTable == null || working.fanTable == null) return
    setWorking((prev) => ({ ...prev, fanTable: [...defaultFanTable] }))
  }

  function validateOffsets(): string | null {
    if (advancedVisible) {
      const max = normalizeOffset(working.maxValueOffset, 0, 100)
      const min = normalizeOffset(working.minValueOffset, -100, 0)
      if (working.maxValueOffset != null && max == null) return t('godMode.advanced.invalidOffset')
      if (working.minValueOffset != null && min == null) return t('godMode.advanced.invalidOffset')
    }
    return null
  }

  async function apply(): Promise<boolean> {
    if (store == null || activePreset == null) return false
    const invalid = validateOffsets()
    if (invalid != null) {
      void message.error(invalid)
      return false
    }
    try {
      const flushed = storeWithPreset(store, flushWorkingPreset(activePreset, working))
      const powerState = useFeaturesStore.getState().states.powerMode
      if (powerState !== 'GodMode') {
        const switched = await useFeaturesStore.getState().setState('powerMode', 'GodMode')
        if (!switched) {
          throw new Error(t('godMode.errorApply'))
        }
      }
      await persist(flushed, true)
      message.success(t('godMode.applySuccess'))
      return true
    } catch (reason) {
      void message.error(`${t('godMode.errorApply')}: ${(reason as Error).message}`)
      return false
    }
  }

  async function handleSave(): Promise<void> {
    setSaving(true)
    try {
      await apply()
    } finally {
      setSaving(false)
    }
  }

  async function handleSaveAndClose(): Promise<void> {
    setSaving(true)
    try {
      if (await apply()) {
        onSaved?.()
        onClose()
      }
    } finally {
      setSaving(false)
    }
  }

  const maxOffsetWarningVisible =
    working.maxValueOffset != null && normalizeOffset(working.maxValueOffset, 0, 100) !== 0
  const minOffsetWarningVisible =
    working.minValueOffset != null && normalizeOffset(working.minValueOffset, -100, 0) !== 0

  return (
    <Modal
      centered
      open={open}
      title={t('godMode.title')}
      width={920}
      okText={t('common.saveAndClose')}
      cancelText={t('common.cancel')}
      confirmLoading={saving}
      onOk={() => void handleSaveAndClose()}
      onCancel={onClose}
      destroyOnHidden
      className="udt-god-mode-dialog"
      footer={[
        ...(loadMenuItems.length > 0
          ? [
              <Dropdown key="load" menu={{ items: loadMenuItems }} placement="topLeft" trigger={['click']}>
                <Button icon={<ChevronDown24Regular />}>
                  {t('common.load', { defaultValue: 'Load' })}
                </Button>
              </Dropdown>
            ]
          : []),
        <span key="spacer" className="udt-god-mode__footer-spacer" />,
        <Button key="save" loading={saving} onClick={() => void handleSave()}>
          {t('common.save')}
        </Button>,
        <Button key="save-close" type="primary" loading={saving} onClick={() => void handleSaveAndClose()}>
          {t('common.saveAndClose')}
        </Button>
      ]}
      styles={{ body: { maxHeight: 'calc(100vh - 200px)', overflowY: 'auto', paddingBottom: 8 } }}
    >
      {loading ? (
        <div className="udt-dashboard-edit__loading">
          <Spin size="large" />
        </div>
      ) : store == null || activePreset == null ? (
        <div className="udt-dashboard-edit__error">{t('godMode.errorLoad')}</div>
      ) : (
        <div className="udt-god-mode">
          {(warnVantage || warnLegionZone) && (
            <div className="udt-god-mode__warnings">
              {warnVantage && (
                <InfoBar
                  severity="warning"
                  message={t('godMode.vantageWarning', {
                    defaultValue:
                      'Custom Mode settings will not be applied correctly when Lenovo Vantage or its services are running.'
                  })}
                />
              )}
              {warnLegionZone && (
                <InfoBar
                  severity="warning"
                  message={t('godMode.legionZoneWarning', {
                    defaultValue:
                      'Custom Mode settings will not be applied correctly when Legion Zone or its services are running.'
                  })}
                />
              )}
            </div>
          )}

          <div className="udt-god-mode__preset-label">{t('godMode.activePreset')}</div>
          <div className="udt-god-mode__preset-row">
            <Select
              className="udt-god-mode__preset-select"
              aria-label={t('godMode.activePreset')}
              value={store.activePresetId}
              options={presetList.map(([id, preset]) => ({ value: id, label: preset.name }))}
              onChange={(value) => void handlePresetSwitch(value)}
            />
            <Button
              className="udt-god-mode__icon-btn"
              icon={<Edit24Regular />}
              title={t('common.rename')}
              onClick={() => openNamePrompt('rename')}
            />
            <Button
              className="udt-god-mode__icon-btn"
              icon={<Delete24Regular />}
              title={t('common.delete')}
              disabled={Object.keys(store.presets).length <= 1}
              onClick={() => void handleDeletePreset()}
            />
            <Button
              type="primary"
              icon={<Add24Regular />}
              onClick={() => openNamePrompt('add')}
            >
              {t('common.add')}
            </Button>
          </div>

          {cpuVisible && (
            <div className="udt-god-mode__section">
              <h3 className="udt-god-mode__section-title">{t('godMode.cpu.title')}</h3>
              {CPU_FIELDS.map((field) => {
                const stepper = activePreset[field]
                if (stepper == null) return null
                return (
                  <GodModeValueControl
                    key={field}
                    title={t(STEPPER_TITLE_KEYS[field])}
                    description={t(STEPPER_DESC_KEYS[field])}
                    unit={STEPPER_UNITS[field] ?? ''}
                    stepper={{
                      steps: stepper.steps.length > 0 ? stepper.steps : undefined,
                      min: stepper.min,
                      max: stepper.max,
                      step: stepper.step,
                      value: working.steppers[field],
                      defaultValue: stepper.defaultValue ?? undefined
                    }}
                    onChange={(value) => updateStepper(field, value)}
                  />
                )
              })}
            </div>
          )}

          {gpuVisible && (
            <div className="udt-god-mode__section">
              <h3 className="udt-god-mode__section-title">{t('godMode.gpu.title')}</h3>
              {GPU_FIELDS.map((field) => {
                const stepper = activePreset[field]
                if (stepper == null) return null
                return (
                  <GodModeValueControl
                    key={field}
                    title={t(STEPPER_TITLE_KEYS[field])}
                    description={t(STEPPER_DESC_KEYS[field])}
                    unit={STEPPER_UNITS[field] ?? ''}
                    stepper={{
                      steps: stepper.steps.length > 0 ? stepper.steps : undefined,
                      min: stepper.min,
                      max: stepper.max,
                      step: stepper.step,
                      value: working.steppers[field],
                      defaultValue: stepper.defaultValue ?? undefined
                    }}
                    onChange={(value) => updateStepper(field, value)}
                  />
                )
              })}
            </div>
          )}

          {fanSectionVisible && (
            <div className="udt-god-mode__section">
              <h3 className="udt-god-mode__section-title">{t('godMode.fans.title')}</h3>
              {fanCurveVisible && (
                <div className={`udt-god-mode__card${working.fanFullSpeed === true ? ' udt-god-mode__card--disabled' : ''}`}>
                  <div className="udt-god-mode__card-title">{t('godMode.fans.curve')}</div>
                  <div className="udt-god-mode__card-desc">{t('godMode.fans.curveMessage')}</div>
                  <FanCurveEditor
                    value={working.fanTable ?? []}
                    minimum={minimumFanTable ?? undefined}
                    sensors={activePreset.fanSensors}
                    disabled={working.fanFullSpeed === true}
                    onChange={(value) => setWorking((prev) => ({ ...prev, fanTable: value }))}
                  />
                  <div className="udt-god-mode__fan-default-row">
                    <Button
                      icon={<ArrowCounterclockwise24Regular />}
                      disabled={working.fanFullSpeed === true || defaultFanTable == null}
                      onClick={handleDefaultFanCurve}
                    >
                      {t('common.default')}
                    </Button>
                  </div>
                </div>
              )}
              {fanFullSpeedVisible && (
                <div className="udt-god-mode__card">
                  <div className="udt-god-mode__card-row udt-god-mode__card-row--top">
                    <div className="udt-god-mode__card-copy">
                      <div className="udt-god-mode__card-title">{t('godMode.fans.maxSpeed')}</div>
                      {working.fanFullSpeed === true && (
                        <div className="udt-god-mode__card-warning">
                          {removeWarningHeading(t('godMode.fans.maxSpeedWarning'))}
                        </div>
                      )}
                    </div>
                    <Switch
                      checked={working.fanFullSpeed === true}
                      aria-label={t('godMode.fans.maxSpeed')}
                      onChange={(checked) => {
                        setWorking((prev) => ({ ...prev, fanFullSpeed: checked }))
                      }}
                    />
                  </div>
                </div>
              )}
            </div>
          )}

          {advancedVisible && (
            <div className="udt-god-mode__section udt-god-mode__section--advanced">
              <h3 className="udt-god-mode__section-title">{t('godMode.advanced.title')}</h3>
              <div className="udt-god-mode__advanced-hint">{t('godMode.advanced.message')}</div>
              {working.maxValueOffset != null && (
                <div className="udt-god-mode__card">
                  <div className="udt-god-mode__card-row udt-god-mode__card-row--top">
                    <div className="udt-god-mode__card-copy">
                      <div className="udt-god-mode__card-title">{t('godMode.advanced.maxOffset')}</div>
                      {maxOffsetWarningVisible && (
                        <div className="udt-god-mode__card-warning">
                          {removeWarningHeading(t('godMode.advanced.maxOffsetWarning'))}
                        </div>
                      )}
                    </div>
                    <InputNumber
                      className="udt-god-mode__offset-input"
                      min={0}
                      max={100}
                      precision={0}
                      value={working.maxValueOffset}
                      onChange={(value) =>
                        setWorking((prev) => ({ ...prev, maxValueOffset: value ?? 0 }))
                      }
                    />
                  </div>
                </div>
              )}
              {working.minValueOffset != null && (
                <div className="udt-god-mode__card">
                  <div className="udt-god-mode__card-row udt-god-mode__card-row--top">
                    <div className="udt-god-mode__card-copy">
                      <div className="udt-god-mode__card-title">{t('godMode.advanced.minOffset')}</div>
                      {minOffsetWarningVisible && (
                        <div className="udt-god-mode__card-warning">
                          {removeWarningHeading(t('godMode.advanced.minOffsetWarning'))}
                        </div>
                      )}
                    </div>
                    <InputNumber
                      className="udt-god-mode__offset-input"
                      min={-100}
                      max={0}
                      precision={0}
                      value={working.minValueOffset}
                      onChange={(value) =>
                        setWorking((prev) => ({ ...prev, minValueOffset: value ?? 0 }))
                      }
                    />
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      <Modal
        centered
        open={namePrompt != null}
        title={t('godMode.presetName')}
        okText={t('common.ok')}
        cancelText={t('common.cancel')}
        onOk={() => void confirmNamePrompt()}
        onCancel={() => setNamePrompt(null)}
        destroyOnHidden
      >
        <Input
          autoFocus
          value={nameInput}
          onChange={(event) => setNameInput(event.target.value)}
          onPressEnter={() => void confirmNamePrompt()}
        />
      </Modal>
    </Modal>
  )
}
