import { create } from 'zustand'
import { automationApi } from '../api/automation'
import {
  createAutomationStoreState,
  type AutomationStore
} from './automationStoreCore'

export type { AutomationStore } from './automationStoreCore'

export const useAutomationStore = create<AutomationStore>()(
  createAutomationStoreState({
    api: automationApi,
    refreshTrayMenu: () => window.bridge?.refreshTrayMenu?.()
  })
)
