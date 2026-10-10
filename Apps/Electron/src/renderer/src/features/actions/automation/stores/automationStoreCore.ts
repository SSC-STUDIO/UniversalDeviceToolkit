import type { StateCreator } from 'zustand'
import type {
  AutomationApi,
  AutomationPipeline,
  AutomationState
} from '../api/automation'

export interface AutomationStore {
  state: AutomationState
  /** $type discriminators of steps supported on this machine (e.g. "powerMode"). */
  steps: string[]
  loaded: boolean
  loading: boolean
  error: string | null
  load: () => Promise<boolean>
  setEnabled: (enabled: boolean) => Promise<boolean>
  save: (pipelines: AutomationPipeline[], isEnabled?: boolean) => Promise<boolean>
  runNow: (pipelineId: string) => Promise<boolean>
  clearError: () => void
}

type AutomationStoreApi = Pick<
  AutomationApi,
  'getState' | 'getSupportedSteps' | 'setEnabled' | 'savePipelines' | 'runNow'
>

export interface AutomationStoreDependencies {
  api: AutomationStoreApi
  refreshTrayMenu: () => void
}

const FAILURE_MESSAGES = {
  load: 'Failed to load automation state',
  setEnabled: 'Failed to update automation state',
  save: 'Failed to save automation pipelines',
  runNow: 'Failed to run automation pipeline'
} as const

function errorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message.trim() !== '') return error.message
  const text = String(error ?? '').trim()
  return text === '' ? fallback : text
}

function resultError(
  result: { error?: string; message?: string },
  fallback: string
): string {
  const explicit = result.error?.trim() || result.message?.trim() || ''
  return explicit === '' ? fallback : explicit
}

function accepted(flag: boolean | undefined): boolean {
  return flag === true
}

export function createAutomationStoreState(
  dependencies: AutomationStoreDependencies
): StateCreator<AutomationStore> {
  return (set, get) => ({
    state: { isEnabled: false, pipelines: [] },
    steps: [],
    loaded: false,
    loading: false,
    error: null,

    async load() {
      if (get().loading) return false
      set({ loading: true, error: null })
      try {
        const [state, supported] = await Promise.all([
          dependencies.api.getState(),
          dependencies.api.getSupportedSteps()
        ])
        set({ state, steps: supported.steps, loaded: true })
        return true
      } catch (error) {
        set({ error: errorMessage(error, FAILURE_MESSAGES.load) })
        return false
      } finally {
        set({ loading: false })
      }
    },

    async setEnabled(enabled) {
      set({ error: null })
      try {
        const result = await dependencies.api.setEnabled(enabled)
        if (!accepted(result.ok)) {
          set({ error: resultError(result, FAILURE_MESSAGES.setEnabled) })
          return false
        }
        set({ state: { ...get().state, isEnabled: enabled } })
        return true
      } catch (error) {
        set({ error: errorMessage(error, FAILURE_MESSAGES.setEnabled) })
        return false
      }
    },

    async save(pipelines, isEnabled) {
      set({ error: null })
      try {
        const result = await dependencies.api.savePipelines(pipelines, isEnabled)
        if (!accepted(result.saved)) {
          set({ error: resultError(result, FAILURE_MESSAGES.save) })
          return false
        }
        if (!(await get().load())) return false
        dependencies.refreshTrayMenu()
        return true
      } catch (error) {
        set({ error: errorMessage(error, FAILURE_MESSAGES.save) })
        return false
      }
    },

    async runNow(pipelineId) {
      set({ error: null })
      try {
        const result = await dependencies.api.runNow(pipelineId)
        if (!accepted(result.ok)) {
          set({ error: resultError(result, FAILURE_MESSAGES.runNow) })
          return false
        }
        return true
      } catch (error) {
        set({ error: errorMessage(error, FAILURE_MESSAGES.runNow) })
        return false
      }
    },

    clearError() {
      set({ error: null })
    }
  })
}
