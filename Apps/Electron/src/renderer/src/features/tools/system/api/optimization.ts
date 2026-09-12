import { invokeObject } from '../../../../shared/bridge/bridge'

export interface OptimizationActionDefinition {
  key: string
  title: string
  description: string
  recommended: boolean
  /** applied state: true = applied, false = not applied, null = unknown */
  applied: boolean | null
}

export interface OptimizationCategoryDefinition {
  key: string
  title: string
  description: string
  hasSettings: boolean
  actions: OptimizationActionDefinition[]
}

export interface OptimizationApi {
  getCategories(): Promise<{ categories: OptimizationCategoryDefinition[] }>
  apply(actionKeys: string[]): Promise<{ applied: boolean }>
  revert(actionKeys: string[]): Promise<{ reverted: boolean }>
  applyRecommended(): Promise<{ applied: boolean }>
  getActionStatus(actionKey: string): Promise<{ applied: boolean | null }>
}

export const optimizationApi: OptimizationApi = {
  async getCategories() {
    return invokeObject<{ categories: OptimizationCategoryDefinition[] }>('optimization.getCategories', {})
  },

  async apply(actionKeys) {
    return invokeObject<{ applied: boolean }>('optimization.apply', { actionKeys })
  },

  async revert(actionKeys) {
    return invokeObject<{ reverted: boolean }>('optimization.revert', { actionKeys })
  },

  async applyRecommended() {
    return invokeObject<{ applied: boolean }>('optimization.applyRecommended', {})
  },

  async getActionStatus(actionKey) {
    return invokeObject<{ applied: boolean | null }>('optimization.getActionStatus', { actionKey })
  }
}
