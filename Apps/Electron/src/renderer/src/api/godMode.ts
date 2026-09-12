import { invoke } from './bridge'

/**
 * God Mode settings bridge — mirrors GodModeController / GodModeSettingsWindow.
 * Prefer godMode.getState / setState / apply over raw settings.godMode so the
 * Host can enrich FanTableInfo sensor data and apply values to hardware.
 */

export type GodModeFanSensorType = 'CPU' | 'CPUSensor' | 'GPU' | 'GPU2'

export interface GodModeFanSensor {
  type: GodModeFanSensorType
  fanSpeeds: number[]
  temps: number[]
}

export interface GodModeStepperValue {
  value: number
  min: number
  max: number
  step: number
  steps: number[]
  defaultValue: number | null
}

export interface GodModePreset {
  name: string
  powerPlanGuid: string | null
  powerMode: string | null
  sourcePowerMode: string | null
  cpuLongTermPowerLimit: GodModeStepperValue | null
  cpuShortTermPowerLimit: GodModeStepperValue | null
  cpuPeakPowerLimit: GodModeStepperValue | null
  cpuCrossLoadingPowerLimit: GodModeStepperValue | null
  cpuPL1Tau: GodModeStepperValue | null
  apUsPPTPowerLimit: GodModeStepperValue | null
  cpuTemperatureLimit: GodModeStepperValue | null
  gpuPowerBoost: GodModeStepperValue | null
  gpuConfigurableTGP: GodModeStepperValue | null
  gpuTemperatureLimit: GodModeStepperValue | null
  gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline: GodModeStepperValue | null
  gpuToCPUDynamicBoost: GodModeStepperValue | null
  /** FanTable.GetTable() — 10 fan speed steps (0-10). */
  fanTable: number[] | null
  /** FanTableInfo.Data — per-sensor temps / RPM for the curve axis + tooltip. */
  fanSensors: GodModeFanSensor[]
  fanFullSpeed: boolean | null
  minValueOffset: number | null
  maxValueOffset: number | null
}

export interface GodModeStore {
  activePresetId: string
  presets: Record<string, GodModePreset>
}

/** Defaults loaded from Quiet / Balance / Performance (Load menu). */
export interface GodModeDefaults {
  cpuLongTermPowerLimit: number | null
  cpuShortTermPowerLimit: number | null
  cpuPeakPowerLimit: number | null
  cpuCrossLoadingPowerLimit: number | null
  cpuPL1Tau: number | null
  apUsPPTPowerLimit: number | null
  cpuTemperatureLimit: number | null
  gpuPowerBoost: number | null
  gpuConfigurableTGP: number | null
  gpuTemperatureLimit: number | null
  gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline: number | null
  gpuToCPUDynamicBoost: number | null
  fanTable: number[] | null
  fanFullSpeed: boolean | null
}

export interface GodModeLoadResult {
  store: GodModeStore
  minimumFanTable: number[] | null
  defaultFanTable: number[] | null
  defaults: Record<string, GodModeDefaults>
  warnVantage: boolean
  warnLegionZone: boolean
}

export type GodModeStepperFieldName =
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

export const GOD_MODE_STEPPER_FIELDS: GodModeStepperFieldName[] = [
  'cpuLongTermPowerLimit',
  'cpuShortTermPowerLimit',
  'cpuPeakPowerLimit',
  'cpuCrossLoadingPowerLimit',
  'cpuPL1Tau',
  'apUsPPTPowerLimit',
  'cpuTemperatureLimit',
  'gpuPowerBoost',
  'gpuConfigurableTGP',
  'gpuTemperatureLimit',
  'gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline',
  'gpuToCPUDynamicBoost'
]

const STEPPER_FIELD_WIRE_MAP: Record<GodModeStepperFieldName, string[]> = {
  cpuLongTermPowerLimit: ['CPULongTermPowerLimit', 'cpuLongTermPowerLimit', 'CpuLongTermPowerLimit'],
  cpuShortTermPowerLimit: ['CPUShortTermPowerLimit', 'cpuShortTermPowerLimit', 'CpuShortTermPowerLimit'],
  cpuPeakPowerLimit: ['CPUPeakPowerLimit', 'cpuPeakPowerLimit', 'CpuPeakPowerLimit'],
  cpuCrossLoadingPowerLimit: ['CPUCrossLoadingPowerLimit', 'cpuCrossLoadingPowerLimit', 'CpuCrossLoadingPowerLimit'],
  cpuPL1Tau: ['CPUPL1Tau', 'cpuPL1Tau', 'CpuPL1Tau'],
  apUsPPTPowerLimit: ['APUsPPTPowerLimit', 'apUsPPTPowerLimit', 'ApUsPPTPowerLimit', 'apusPPTPowerLimit'],
  cpuTemperatureLimit: ['CPUTemperatureLimit', 'cpuTemperatureLimit', 'CpuTemperatureLimit'],
  gpuPowerBoost: ['GPUPowerBoost', 'gpuPowerBoost', 'GpuPowerBoost'],
  gpuConfigurableTGP: ['GPUConfigurableTGP', 'gpuConfigurableTGP', 'GpuConfigurableTGP'],
  gpuTemperatureLimit: ['GPUTemperatureLimit', 'gpuTemperatureLimit', 'GpuTemperatureLimit'],
  gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline: [
    'GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline',
    'gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline',
    'GpuTotalProcessingPowerTargetOnAcOffsetFromBaseline'
  ],
  gpuToCPUDynamicBoost: ['GPUToCPUDynamicBoost', 'gpuToCPUDynamicBoost', 'GpuToCPUDynamicBoost', 'gpuToCpuDynamicBoost']
}

/** Wire (PascalCase) name of a stepper field matching C# GodModePreset / GodModeHandlers. */
export function stepperWireName(field: GodModeStepperFieldName): string {
  return STEPPER_FIELD_WIRE_MAP[field]?.[0] ?? (field[0].toUpperCase() + field.slice(1))
}

type JsonObject = Record<string, unknown>

function readObject(value: unknown): JsonObject | null {
  if (value == null || typeof value !== 'object' || Array.isArray(value)) return null
  return value as JsonObject
}

function getKey(record: JsonObject | null, ...names: string[]): unknown {
  if (record == null) return undefined
  for (const name of names) {
    if (name in record) return record[name]
  }
  const lowerNames = names.map((n) => n.toLowerCase())
  for (const [k, v] of Object.entries(record)) {
    if (lowerNames.includes(k.toLowerCase())) return v
  }
  return undefined
}

function readString(value: unknown): string | null {
  return typeof value === 'string' ? value : null
}

function readNumber(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null
}

function readBool(value: unknown): boolean | null {
  return typeof value === 'boolean' ? value : null
}

function readArray(value: unknown): unknown[] | null {
  return Array.isArray(value) ? value : null
}

function readIntArray(value: unknown): number[] | null {
  const arr = readArray(value)
  if (arr == null) return null
  const numbers = arr.map((item) => readNumber(item))
  if (numbers.some((n) => n == null)) return null
  return numbers as number[]
}

function readStepper(value: unknown): GodModeStepperValue | null {
  const obj = readObject(value)
  if (obj == null) return null
  const rawValue = readNumber(getKey(obj, 'Value', 'value'))
  if (rawValue == null) return null
  const steps = readArray(getKey(obj, 'Steps', 'steps')) ?? []
  const defaultValue = readNumber(getKey(obj, 'DefaultValue', 'defaultValue'))
  return {
    value: rawValue,
    min: readNumber(getKey(obj, 'Min', 'min')) ?? 0,
    max: readNumber(getKey(obj, 'Max', 'max')) ?? 0,
    step: readNumber(getKey(obj, 'Step', 'step')) ?? 1,
    steps: steps.map((step) => readNumber(step)).filter((step): step is number => step != null),
    defaultValue: defaultValue == null ? null : defaultValue
  }
}

function readFanTable(value: unknown): number[] | null {
  const fromArray = readIntArray(value)
  if (fromArray != null && fromArray.length === 10) return fromArray

  const obj = readObject(value)
  if (obj == null) return null
  const speeds: number[] = []
  for (let i = 0; i < 10; i++) {
    const speed = readNumber(getKey(obj, `FSS${i}`, `fss${i}`))
    if (speed == null) return null
    speeds.push(speed)
  }
  return speeds
}

const FAN_SENSOR_TYPES: GodModeFanSensorType[] = ['CPU', 'CPUSensor', 'GPU', 'GPU2']

function isFanSensorType(value: string): value is GodModeFanSensorType {
  return (FAN_SENSOR_TYPES as string[]).includes(value)
}

function readFanSensors(value: unknown): GodModeFanSensor[] {
  const arr = readArray(value)
  if (arr == null) return []
  const sensors: GodModeFanSensor[] = []
  for (const item of arr) {
    const obj = readObject(item)
    if (obj == null) continue
    const typeRaw = readString(getKey(obj, 'Type', 'type'))
    if (typeRaw == null || !isFanSensorType(typeRaw)) continue
    const fanSpeeds = readIntArray(getKey(obj, 'FanSpeeds', 'fanSpeeds'))
    const temps = readIntArray(getKey(obj, 'Temps', 'temps'))
    if (fanSpeeds == null || temps == null) continue
    sensors.push({ type: typeRaw, fanSpeeds, temps })
  }
  return sensors
}

function emptyPreset(name = ''): GodModePreset {
  return {
    name,
    powerPlanGuid: null,
    powerMode: null,
    sourcePowerMode: null,
    cpuLongTermPowerLimit: null,
    cpuShortTermPowerLimit: null,
    cpuPeakPowerLimit: null,
    cpuCrossLoadingPowerLimit: null,
    cpuPL1Tau: null,
    apUsPPTPowerLimit: null,
    cpuTemperatureLimit: null,
    gpuPowerBoost: null,
    gpuConfigurableTGP: null,
    gpuTemperatureLimit: null,
    gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline: null,
    gpuToCPUDynamicBoost: null,
    fanTable: null,
    fanSensors: [],
    fanFullSpeed: null,
    minValueOffset: null,
    maxValueOffset: null
  }
}

function readPreset(value: unknown): GodModePreset | null {
  const obj = readObject(value)
  if (obj == null) return null
  const preset = emptyPreset(readString(getKey(obj, 'Name', 'name')) ?? '')
  preset.powerPlanGuid = readString(getKey(obj, 'PowerPlanGuid', 'powerPlanGuid'))
  preset.powerMode = readString(getKey(obj, 'PowerMode', 'powerMode'))
  preset.sourcePowerMode = readString(getKey(obj, 'SourcePowerMode', 'sourcePowerMode'))
  preset.fanTable = readFanTable(getKey(obj, 'FanTable', 'fanTable'))
  preset.fanSensors = readFanSensors(getKey(obj, 'FanSensors', 'fanSensors'))
  preset.fanFullSpeed = readBool(getKey(obj, 'FanFullSpeed', 'fanFullSpeed'))
  preset.minValueOffset = readNumber(getKey(obj, 'MinValueOffset', 'minValueOffset'))
  preset.maxValueOffset = readNumber(getKey(obj, 'MaxValueOffset', 'maxValueOffset'))
  for (const field of GOD_MODE_STEPPER_FIELDS) {
    const candidateKeys = STEPPER_FIELD_WIRE_MAP[field] ?? [stepperWireName(field), field]
    preset[field] = readStepper(getKey(obj, ...candidateKeys))
  }
  return preset
}

export function parseGodModeStore(value: unknown): GodModeStore | null {
  const obj = readObject(value)
  if (obj == null) return null
  const activePresetId = readString(getKey(obj, 'ActivePresetId', 'activePresetId')) ?? ''
  const presetsObj = readObject(getKey(obj, 'Presets', 'presets'))
  const presets: Record<string, GodModePreset> = {}
  if (presetsObj != null) {
    for (const [id, presetValue] of Object.entries(presetsObj)) {
      const preset = readPreset(presetValue)
      if (preset != null) presets[id] = preset
    }
  }
  return { activePresetId, presets }
}

function readDefaults(value: unknown): GodModeDefaults {
  const obj = readObject(value)
  const result: Partial<GodModeDefaults> = {}
  for (const field of GOD_MODE_STEPPER_FIELDS) {
    const candidateKeys = STEPPER_FIELD_WIRE_MAP[field] ?? [stepperWireName(field), field]
    result[field] = readNumber(getKey(obj, ...candidateKeys))
  }
  result.fanTable = readFanTable(getKey(obj, 'FanTable', 'fanTable'))
  result.fanFullSpeed = readBool(getKey(obj, 'FanFullSpeed', 'fanFullSpeed'))
  return result as GodModeDefaults
}

function serializeStepper(stepper: GodModeStepperValue): Record<string, unknown> {
  return {
    Value: stepper.value,
    Min: stepper.min,
    Max: stepper.max,
    Step: stepper.step,
    Steps: stepper.steps,
    DefaultValue: stepper.defaultValue
  }
}

function serializeFanTable(table: number[]): Record<string, unknown> {
  const result: Record<string, unknown> = { FSTM: 1, FSID: 0, FSTL: 0 }
  for (let i = 0; i < 10; i++) result[`FSS${i}`] = table[i]
  return result
}

function serializePreset(preset: GodModePreset): Record<string, unknown> {
  const result: Record<string, unknown> = {
    Name: preset.name,
    PowerPlanGuid: preset.powerPlanGuid,
    PowerMode: preset.powerMode,
    SourcePowerMode: preset.sourcePowerMode,
    FanTable: preset.fanTable != null ? serializeFanTable(preset.fanTable) : null,
    FanFullSpeed: preset.fanFullSpeed,
    MinValueOffset: preset.minValueOffset,
    MaxValueOffset: preset.maxValueOffset
  }
  for (const field of GOD_MODE_STEPPER_FIELDS) {
    const stepper = preset[field]
    result[stepperWireName(field)] = stepper != null ? serializeStepper(stepper) : null
  }
  return result
}

export function serializeGodModeStore(store: GodModeStore): Record<string, unknown> {
  const presets: Record<string, unknown> = {}
  for (const [id, preset] of Object.entries(store.presets)) {
    presets[id] = serializePreset(preset)
  }
  return {
    ActivePresetId: store.activePresetId,
    Presets: presets
  }
}

function parseLoadResult(raw: unknown): GodModeLoadResult | null {
  const obj = readObject(raw)
  if (obj == null) return null
  const store = parseGodModeStore(getKey(obj, 'state', 'State'))
  if (store == null) return null
  const defaultsObj = readObject(getKey(obj, 'defaults', 'Defaults'))
  const defaults: Record<string, GodModeDefaults> = {}
  if (defaultsObj != null) {
    for (const [key, value] of Object.entries(defaultsObj)) {
      defaults[key] = readDefaults(value)
    }
  }
  return {
    store,
    minimumFanTable: readIntArray(getKey(obj, 'minimumFanTable', 'MinimumFanTable')),
    defaultFanTable: readIntArray(getKey(obj, 'defaultFanTable', 'DefaultFanTable')),
    defaults,
    warnVantage: readBool(getKey(obj, 'warnVantage', 'WarnVantage')) === true,
    warnLegionZone: readBool(getKey(obj, 'warnLegionZone', 'WarnLegionZone')) === true
  }
}

export const godModeApi = {
  async load(): Promise<GodModeLoadResult> {
    const result = await invoke<unknown>('godMode.getState')
    const parsed = parseLoadResult(result)
    if (parsed == null) {
      throw new Error('godMode.getState returned an invalid payload')
    }
    return parsed
  },
  async save(store: GodModeStore, apply = false): Promise<GodModeStore> {
    const result = await invoke<unknown>('godMode.setState', {
      state: serializeGodModeStore(store),
      apply
    })
    const obj = readObject(result)
    const next = parseGodModeStore(getKey(obj, 'state', 'State'))
    if (next == null) {
      throw new Error('godMode.setState returned an invalid payload')
    }
    return next
  },
  async apply(): Promise<void> {
    await invoke('godMode.apply')
  }
}

/** Electron GodModeSettingsWindow.GetUniquePresetName — "Name (2)", "Name (3)", ... */
export function getUniquePresetName(
  requestedName: string,
  presets: Record<string, GodModePreset>,
  excludePresetId?: string
): string {
  const normalized = requestedName.trim() || 'Preset'
  const existing = new Set(
    Object.entries(presets)
      .filter(([id]) => id !== excludePresetId)
      .map(([, preset]) => preset.name.trim())
      .filter((name) => name.length > 0)
      .map((name) => name.toLowerCase())
  )
  if (!existing.has(normalized.toLowerCase())) return normalized
  let suffix = 2
  while (true) {
    const candidate = `${normalized} (${suffix})`
    if (!existing.has(candidate.toLowerCase())) return candidate
    suffix++
  }
}

/** Electron GodModeSettingsWindow.AddPreset — copy of the active preset under a new id. */
export function addPreset(
  store: GodModeStore,
  requestedName: string,
  newPresetId?: string
): GodModeStore {
  const presetId = newPresetId ?? crypto.randomUUID()
  if (store.presets[presetId] != null) throw new Error(`Preset with ID ${presetId} already exists.`)
  const active = store.presets[store.activePresetId]
  if (active == null) throw new Error('Active preset not found.')
  const name = getUniquePresetName(requestedName, store.presets)
  return {
    activePresetId: presetId,
    presets: {
      ...store.presets,
      [presetId]: { ...active, name, sourcePowerMode: null }
    }
  }
}

/** Electron GodModeSettingsWindow.RenameActivePreset. */
export function renameActivePreset(store: GodModeStore, requestedName: string): GodModeStore {
  const preset = store.presets[store.activePresetId]
  if (preset == null) return store
  const name = getUniquePresetName(requestedName, store.presets, store.activePresetId)
  return {
    ...store,
    presets: {
      ...store.presets,
      [store.activePresetId]: { ...preset, name, sourcePowerMode: null }
    }
  }
}

/** Electron GodModeSettingsWindow.DeleteActivePreset — keep at least one preset. */
export function deleteActivePreset(store: GodModeStore): GodModeStore {
  const preset = store.presets[store.activePresetId]
  if (preset == null || Object.keys(store.presets).length <= 1) return store
  const presets: Record<string, GodModePreset> = {}
  for (const [id, value] of Object.entries(store.presets)) {
    if (id !== store.activePresetId) presets[id] = value
  }
  const nextActiveId = Object.entries(presets)
    .sort((a, b) => a[1].name.localeCompare(b[1].name))
    .map(([id]) => id)[0]
  return { activePresetId: nextActiveId ?? store.activePresetId, presets }
}
