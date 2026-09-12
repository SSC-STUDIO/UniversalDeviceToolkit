import { invoke } from './bridge'

/** A pipeline step/trigger payload. Kept flexible: $type discriminator + opaque fields. */
export interface AutomationStepType extends Record<string, unknown> {
  $type: string
}

/** A single automation pipeline, as serialized by AutomationSettings ($type-preserving). */
export interface AutomationPipeline extends Record<string, unknown> {
  id?: string
  iconName?: string
  name?: string
  trigger?: Record<string, unknown> | null
  steps?: AutomationStepType[]
  isExclusive?: boolean
}

export interface AutomationState {
  isEnabled: boolean
  pipelines: AutomationPipeline[]
}

export interface AutomationApi {
  getState(): Promise<AutomationState>
  setEnabled(enabled: boolean): Promise<{ ok: boolean; error?: string; message?: string }>
  savePipelines(
    pipelines: AutomationPipeline[],
    isEnabled?: boolean
  ): Promise<{ saved: boolean; error?: string; message?: string }>
  runNow(pipelineId: string): Promise<{ ok: boolean; error?: string; message?: string }>
  getSupportedSteps(): Promise<{ steps: string[] }>
  /** Open a native file picker for a backlight profile (.json); null when cancelled. */
  selectProfileJson(): Promise<string | null>
  /** Feature state list, e.g. feature.getStates("dpiScale") → [{ Scale: 100 }]. */
  getFeatureStates(feature: string): Promise<{ states: unknown[] }>
  /** God Mode preset store (settings.get "godMode" scope) for preset step options. */
  getGodModePresets(): Promise<{ scope: string; value: unknown }>
}

export const automationApi: AutomationApi = {
  async getState() {
    return invoke<AutomationState>('automation.getState', {})
  },

  async setEnabled(enabled) {
    return invoke<{ ok: boolean }>('automation.setEnabled', { enabled })
  },

  async savePipelines(pipelines, isEnabled) {
    const params: Record<string, unknown> = { pipelines }
    if (isEnabled !== undefined) params.isEnabled = isEnabled
    return invoke<{ saved: boolean }>('automation.savePipelines', params)
  },

  async runNow(pipelineId) {
    return invoke<{ ok: boolean }>('automation.runNow', { pipelineId })
  },

  async getSupportedSteps() {
    return invoke<{ steps: string[] }>('automation.getSupportedSteps', {})
  },

  async selectProfileJson() {
    return invoke<string | null>('dialog:select-json-file', {})
  },

  async getFeatureStates(feature) {
    return invoke<{ states: unknown[] }>('feature.getStates', { feature })
  },

  async getGodModePresets() {
    return invoke<{ scope: string; value: unknown }>('settings.get', { scope: 'godMode' })
  },
}
