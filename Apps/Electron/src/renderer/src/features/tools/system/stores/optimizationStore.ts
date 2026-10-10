import { create } from 'zustand'
import { optimizationApi, type OptimizationCategoryDefinition } from '../api/optimization'

export interface OptimizationStoreState {
  categories: OptimizationCategoryDefinition[]
  loading: boolean
  error: string | null
}

export interface OptimizationStoreActions {
  load: () => Promise<void>
  refresh: () => Promise<void>
  apply: (keys: string[]) => Promise<boolean>
  revert: (keys: string[]) => Promise<boolean>
  applyRecommended: () => Promise<boolean>
}

export type OptimizationStore = OptimizationStoreState & OptimizationStoreActions

export const useOptimizationStore = create<OptimizationStore>((set, get) => {
  let categoryRequestId = 0

  const loadCategories = async (force: boolean): Promise<void> => {
    if (!force && get().loading) return
    const requestId = ++categoryRequestId
    set({ loading: true, error: null })
    try {
      const { categories } = await optimizationApi.getCategories()
      if (requestId === categoryRequestId) set({ categories })
    } catch (error) {
      if (requestId === categoryRequestId) set({ error: (error as Error).message })
    } finally {
      if (requestId === categoryRequestId) set({ loading: false })
    }
  }

  return {
    categories: [],
    loading: false,
    error: null,

    load: () => loadCategories(false),
    refresh: () => loadCategories(true),

  async apply(keys) {
    if (keys.length === 0) return true
    try {
      const res = await optimizationApi.apply(keys)
      if (!res.applied) return false
      await get().load()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async revert(keys) {
    if (keys.length === 0) return true
    try {
      const res = await optimizationApi.revert(keys)
      if (!res.reverted) return false
      await get().load()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  async applyRecommended() {
    try {
      const res = await optimizationApi.applyRecommended()
      if (!res.applied) return false
      await get().load()
      return true
    } catch (error) {
      set({ error: (error as Error).message })
      return false
    }
  },

  }
})
