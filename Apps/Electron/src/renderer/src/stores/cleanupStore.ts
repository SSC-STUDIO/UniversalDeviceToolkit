import { create } from 'zustand'
import { optimizationApi, type CustomCleanupRule } from '../api/optimization'

/** Custom cleanup rule with a stable client-side id for React keys. */
export interface CleanupRule extends CustomCleanupRule {
  id: string
}

export interface CleanupStoreState {
  rules: CleanupRule[]
  loaded: boolean
  loading: boolean
  error: string | null
}

export interface CleanupStoreActions {
  load: () => Promise<void>
  addRule: (directoryPath: string) => Promise<boolean>
  updateRulePath: (id: string, directoryPath: string) => Promise<boolean>
  removeRule: (id: string) => Promise<boolean>
  clearRules: () => Promise<boolean>
}

export type CleanupStore = CleanupStoreState & CleanupStoreActions

function toCleanupRule(rule: CustomCleanupRule): CleanupRule {
  return {
    id: rule.directoryPath,
    directoryPath: rule.directoryPath,
    recursive: rule.recursive,
    extensions: [...(rule.extensions ?? [])]
  }
}

function toModels(rules: CleanupRule[]): CustomCleanupRule[] {
  return rules.map(({ directoryPath, recursive, extensions }) => ({
    directoryPath,
    recursive,
    extensions
  }))
}

export const useCleanupStore = create<CleanupStore>((set, get) => {
  const persist = async (rules: CleanupRule[]): Promise<boolean> => {
    try {
      const res = await optimizationApi.saveCustomCleanupRules(toModels(rules))
      return res.saved
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  }

  return {
    rules: [],
    loaded: false,
    loading: false,
    error: null,

    async load() {
      if (get().loading) return
      set({ loading: true, error: null })
      try {
        const { rules } = await optimizationApi.getCustomCleanupRules()
        set({ rules: rules.map(toCleanupRule), loaded: true })
      } catch (error) {
        set({ error: (error as Error).message })
      } finally {
        set({ loading: false })
      }
    },

    async addRule(directoryPath) {
      if (!directoryPath) return false
      const rules = [...get().rules, toCleanupRule({ directoryPath, recursive: false, extensions: [] })]
      set({ rules })
      return persist(rules)
    },

    async updateRulePath(id, directoryPath) {
      if (!directoryPath) return false
      const rules = get().rules.map((rule) =>
        rule.id === id ? { ...rule, directoryPath } : rule
      )
      set({ rules })
      return persist(rules)
    },

    async removeRule(id) {
      const rules = get().rules.filter((rule) => rule.id !== id)
      set({ rules })
      return persist(rules)
    },

    async clearRules() {
      set({ rules: [] })
      return persist([])
    }
  }
})
