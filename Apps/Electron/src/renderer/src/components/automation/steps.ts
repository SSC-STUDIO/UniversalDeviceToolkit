import type { TFunction } from 'i18next'
import type { AutomationPipeline, AutomationStepType } from '../../api/automation'
import type { FeatureKey } from '../../api/features'

/**
 * State payload of a step, mirroring the CLR enum-name / struct encoding
 * used by UniversalDeviceToolkit.Lib.Automation serialization
 * (enums → string names, structs → { frequency } / { width, height }).
 */
export type StepState = string | { frequency: number } | { width: number; height: number }

export interface StepOption {
  value: StepState
  labelText?: string
}

export type StepEditorKind = 'select' | 'text' | 'file' | 'pipeline'

export interface StepDef {
  /** $type discriminator written to the serialized step (camelCase class name). */
  discriminator: string
  kind: StepEditorKind
  /** feature domain key when options come from the host's feature.getStates. */
  featureKey?: FeatureKey
  /** i18n key suffix under `automation.steps`. */
  i18nKey: string
  /** Static fallback options (identical to the CLR enum values). */
  staticOptions?: StepOption[]
}

const enumOption = (value: string): StepOption => ({ value })

/** All 16 step definitions assigned for the Electron port. */
export const STEP_DEFS: Record<string, StepDef> = {
  hybridMode: {
    discriminator: 'hybridMode',
    kind: 'select',
    featureKey: 'hybridMode',
    i18nKey: 'hybridMode',
    staticOptions: ['On', 'OnIGPUOnly', 'OnAuto', 'Off'].map(enumOption),
  },
  instantBoot: {
    discriminator: 'instantBoot',
    kind: 'select',
    featureKey: 'instantBoot',
    i18nKey: 'instantBoot',
    staticOptions: ['Off', 'AcAdapter', 'UsbPowerDelivery', 'AcAdapterAndUsbPowerDelivery'].map(enumOption),
  },
  macro: {
    discriminator: 'macro',
    kind: 'select',
    i18nKey: 'macro',
    staticOptions: ['Off', 'On'].map(enumOption),
  },
  microphone: {
    discriminator: 'microphone',
    kind: 'select',
    featureKey: 'microphone',
    i18nKey: 'microphone',
    staticOptions: ['Off', 'On'].map(enumOption),
  },
  notification: {
    discriminator: 'notification',
    kind: 'text',
    i18nKey: 'notification',
  },
  oneLevelWhiteKeyboardBacklight: {
    discriminator: 'oneLevelWhiteKeyboardBacklight',
    kind: 'select',
    featureKey: 'oneLevelWhiteKeyboard',
    i18nKey: 'oneLevelWhiteKeyboardBacklight',
    staticOptions: ['Off', 'On'].map(enumOption),
  },
  osd: {
    discriminator: 'osd',
    kind: 'select',
    i18nKey: 'osd',
    staticOptions: ['Hidden', 'Show', 'Toggle'].map(enumOption),
  },
  overclockDiscreteGPU: {
    discriminator: 'overclockDiscreteGPU',
    kind: 'select',
    i18nKey: 'overclockDiscreteGPU',
    staticOptions: ['Off', 'On'].map(enumOption),
  },
  overDrive: {
    discriminator: 'overDrive',
    kind: 'select',
    featureKey: 'overDrive',
    i18nKey: 'overDrive',
    staticOptions: ['Off', 'On'].map(enumOption),
  },
  panelLogoBacklight: {
    discriminator: 'panelLogoBacklight',
    kind: 'select',
    featureKey: 'panelLogo',
    i18nKey: 'panelLogoBacklight',
    staticOptions: ['Off', 'On'].map(enumOption),
  },
  playSound: {
    discriminator: 'playSound',
    kind: 'file',
    i18nKey: 'playSound',
  },
  portsBacklight: {
    discriminator: 'portsBacklight',
    kind: 'select',
    featureKey: 'portsBacklight',
    i18nKey: 'portsBacklight',
    staticOptions: ['Off', 'On'].map(enumOption),
  },
  powerMode: {
    discriminator: 'powerMode',
    kind: 'select',
    featureKey: 'powerMode',
    i18nKey: 'powerMode',
    staticOptions: ['Quiet', 'Balance', 'Performance', 'Extreme', 'GodMode'].map(enumOption),
  },
  quickAction: {
    discriminator: 'quickAction',
    kind: 'pipeline',
    i18nKey: 'quickAction',
  },
  refreshRate: {
    discriminator: 'refreshRate',
    kind: 'select',
    featureKey: 'refreshRate',
    i18nKey: 'refreshRate',
  },
  resolution: {
    discriminator: 'resolution',
    kind: 'select',
    featureKey: 'resolution',
    i18nKey: 'resolution',
  },
}

/** Shared display-name mapping for CLR enum values. */
export const ENUM_LABEL_KEYS: Record<string, string> = {
  On: 'automation.state.on',
  Off: 'automation.state.off',
  OnIGPUOnly: 'automation.state.hybridIgpu',
  OnAuto: 'automation.state.hybridAuto',
  AcAdapter: 'automation.state.acAdapter',
  UsbPowerDelivery: 'automation.state.usbPd',
  AcAdapterAndUsbPowerDelivery: 'automation.state.acAndUsbPd',
  Hidden: 'automation.state.hidden',
  Show: 'automation.state.show',
  Toggle: 'automation.state.toggle',
  Quiet: 'automation.state.quiet',
  Balance: 'automation.state.balance',
  Performance: 'automation.state.performance',
  Extreme: 'automation.state.extreme',
  GodMode: 'automation.state.godMode',
}

/**
 * Normalizes a serialized $type into a known discriminator. Accepts the
 * camelCase discriminator ("hybridMode"), the CLR type name
 * ("HybridModeAutomationStep") or a lower-cased variant.
 */
export function normalizeStepDiscriminator($type: string): string {
  const trimmed = $type.replace(/AutomationStep$/i, '')
  const first = trimmed.charAt(0).toLowerCase() + trimmed.slice(1)
  if (STEP_DEFS[first]) return first
  const found = Object.keys(STEP_DEFS).find((key) => key.toLowerCase() === trimmed.toLowerCase())
  return found ?? first
}

export function getStepDef($type: string): StepDef | undefined {
  return STEP_DEFS[normalizeStepDiscriminator($type)]
}

/** Coerces host-provided state values into the canonical StepState shape. */
export function normalizeState(raw: unknown): StepState {
  if (raw !== null && typeof raw === 'object') {
    const obj = raw as Record<string, unknown>
    const frequency = obj.frequency ?? obj.Frequency
    if (typeof frequency === 'number') return { frequency }
    const width = obj.width ?? obj.Width
    const height = obj.height ?? obj.Height
    if (typeof width === 'number' && typeof height === 'number') return { width, height }
    return JSON.stringify(raw)
  }
  return typeof raw === 'string' ? raw : String(raw ?? '')
}

export function statesEqual(a: StepState, b: StepState): boolean {
  return JSON.stringify(a) === JSON.stringify(b)
}

/** Localized display name of a state value (mirrors CLR DisplayName). */
export function stateLabel(state: StepState, def: StepDef, t: TFunction): string {
  if (typeof state === 'string') {
    const sharedKey = ENUM_LABEL_KEYS[state]
    if (sharedKey) return t(sharedKey, { defaultValue: state })
    return t(`automation.stepEditors.${def.i18nKey}.options.${state}`, { defaultValue: state })
  }
  if ('frequency' in state) return t('automation.state.hz', { frequency: state.frequency })
  return t('automation.state.resolution', { width: state.width, height: state.height })
}

function fileBaseName(path: string): string {
  const normalized = path.replace(/[\\/]+$/, '')
  const parts = normalized.split(/[\\/]/)
  return parts[parts.length - 1] || normalized
}

/**
 * Friendly one-line summary of a step's parameters, matching what the Electron
 * card shows next to the step title (selected state / text / file name).
 */
export function formatStepSummary(
  step: AutomationStepType,
  t: TFunction,
  pipelines?: AutomationPipeline[],
): string {
  const def = getStepDef(String(step.$type))
  if (!def) return ''

  switch (def.kind) {
    case 'select': {
      const state = step.state
      if (state === undefined || state === null || state === '') return ''
      return stateLabel(normalizeState(state), def, t)
    }
    case 'text': {
      const text = typeof step.text === 'string' ? step.text : ''
      return text.trim() ? text : ''
    }
    case 'file': {
      const path = typeof step.path === 'string' ? step.path : ''
      return path ? fileBaseName(path) : ''
    }
    case 'pipeline': {
      const id = typeof step.pipelineId === 'string' ? step.pipelineId : null
      if (!id || !pipelines) return ''
      const pipeline = pipelines.find((p) => p.id === id)
      return pipeline ? (pipeline.name ?? t('automation.quickAction')) : ''
    }
  }
}
