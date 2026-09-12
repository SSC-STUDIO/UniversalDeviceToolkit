import { invokeObject } from '../../../../shared/bridge/bridge'

/** Custom cleanup rule (WindowsOptimizationPage.Cleanup.cs → CustomCleanupRule). */
export interface CustomCleanupRule {
  directoryPath: string
  recursive: boolean
  extensions: string[]
}

export interface CleanupApi {
  estimateCleanup(actionKeys: string[]): Promise<{ bytes: number }>
  runCleanup(actionKeys: string[]): Promise<{ done: boolean }>
  // Custom cleanup rules
  getCustomCleanupRules(): Promise<{ rules: CustomCleanupRule[] }>
  saveCustomCleanupRules(rules: CustomCleanupRule[]): Promise<{ saved: boolean }>
}

export const cleanupApi: CleanupApi = {
  async estimateCleanup(actionKeys) {
    return invokeObject<{ bytes: number }>('cleanup.estimate', { actionKeys })
  },

  async runCleanup(actionKeys) {
    return invokeObject<{ done: boolean }>('cleanup.run', { actionKeys })
  },

  async getCustomCleanupRules() {
    return invokeObject<{ rules: CustomCleanupRule[] }>('cleanup.getCustomRules', {})
  },

  async saveCustomCleanupRules(rules) {
    return invokeObject<{ saved: boolean }>('cleanup.saveCustomRules', { rules })
  }
}
