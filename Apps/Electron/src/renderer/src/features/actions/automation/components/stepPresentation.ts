import type { TFunction } from 'i18next'
import type { AutomationStepType } from '../api/automation'

export function stepTitleKey(type: string): string {
  return `automation.stepEditors.${type}.title`
}

export function stepDescKey(type: string): string {
  return `automation.stepEditors.${type}.desc`
}

/** Static enum option lists (mirrors GetAllStatesAsync of the feature steps). */
export const ENUM_OPTIONS: Record<string, string[]> = {
  alwaysOnUsb: ['Off', 'OnWhenSleeping', 'OnAlways'],
  battery: ['Conservation', 'Normal', 'RapidCharge'],
  batteryNightCharge: ['On', 'Off'],
  deactivateGPU: ['KillApps', 'RestartGPU'],
  flipToStart: ['Off', 'On'],
  fnLock: ['Off', 'On'],
  hdr: ['Off', 'On']
}

/** Default enum state per step — mirrors default(T) used by the Electron palette factories. */
export const DEFAULT_ENUM_STATE: Record<string, string> = {
  alwaysOnUsb: 'Off',
  battery: 'Conservation',
  batteryNightCharge: 'On',
  deactivateGPU: 'KillApps',
  flipToStart: 'Off',
  fnLock: 'Off',
  hdr: 'Off'
}

export function enumStateLabelKey(type: string, value: string): string {
  if (value === 'On' || value === 'Off') return `automation.state.${value.toLowerCase()}`
  return `automation.stepEditors.${type}.options.${value}`
}

/**
 * Default serialized payload per step type — mirrors the Electron AddStep palette
 * factories (DisplayBrightnessAutomationStep(50), DelayAutomationStep(1), ...).
 */
export function createDefaultStep(type: string): AutomationStepType {
  const step: AutomationStepType = { $type: type }
  if (DEFAULT_ENUM_STATE[type] !== undefined) {
    step.state = DEFAULT_ENUM_STATE[type]
  } else if (type === 'delay') {
    step.state = { delaySeconds: 1 }
  } else if (type === 'displayBrightness') {
    step.brightness = 50
  } else if (type === 'dpiScale') {
    step.state = { scale: 0 }
  } else if (type === 'godModePreset') {
    step.presetId = ''
  }
  return step
}

/** Localized one-line summary of a step's parameters (card subtitle parity). */
export function stepSummaryText(step: AutomationStepType, t: TFunction): string {
  const type = String(step.$type)
  if (ENUM_OPTIONS[type] !== undefined) {
    const state = typeof step.state === 'string' ? step.state : ''
    if (state === '') return ''
    return t(enumStateLabelKey(type, state), { defaultValue: state })
  }
  if (type === 'delay') {
    const state = step.state as { delaySeconds?: unknown } | undefined
    const seconds = typeof state?.delaySeconds === 'number' ? state.delaySeconds : undefined
    return seconds === undefined ? '' : t('automation.stepEditors.delay.second', { count: seconds })
  }
  if (type === 'displayBrightness' || type === 'dpiScale') {
    const raw = type === 'displayBrightness'
      ? step.brightness
      : (step.state as Record<string, unknown> | undefined)?.['scale'] ?? (step.state as Record<string, unknown> | undefined)?.['Scale']
    const value = typeof raw === 'number' && Number.isFinite(raw) ? raw : undefined
    return value === undefined ? '' : t(`automation.stepEditors.${type}.percent`, { value })
  }
  return ''
}
