import { create } from 'zustand'
import { settingsApi, type SettingsScope } from '../api/settings'

const KNOWN_SETTINGS_SCOPES: ReadonlySet<string> = new Set<string>([
  'application',
  'osd',
  'hardwareSensors',
  'balanceMode',
  'godMode',
  'gpuOverclock',
  'integrations',
  'lampArray',
  'fanCurves',
  'packageDownloader',
  'rgbKeyboard',
  'spectrumKeyboard',
  'sunriseSunset',
  'updateCheck',
  'networkAcceleration',
  'batteryHealthAlerts',
  'dashboard'
] satisfies SettingsScope[])

function isSettingsScope(scope: string): scope is SettingsScope {
  return KNOWN_SETTINGS_SCOPES.has(scope)
}

interface SettingsStoreState {
  scopes: Record<string, unknown>
  loading: boolean
  load: (scopes?: SettingsScope[]) => Promise<void>
  setScope: (scope: SettingsScope, value: unknown) => void
  save: (scopes?: SettingsScope[]) => Promise<void>
}

let nextLoadGeneration = 0
let latestFullLoadGeneration = 0
let activeLoadCount = 0
const latestPartialLoadGenerations = new Map<SettingsScope, number>()

export const useSettingsStore = create<SettingsStoreState>((set, get) => ({
  scopes: {},
  loading: false,

  load: async (scopes) => {
    const generation = ++nextLoadGeneration
    const requestedScopes = scopes?.filter(isSettingsScope)
    if (requestedScopes === undefined) {
      latestFullLoadGeneration = generation
    } else {
      for (const scope of requestedScopes) {
        latestPartialLoadGenerations.set(scope, generation)
      }
    }

    activeLoadCount += 1
    set({ loading: true })
    try {
      const result = await settingsApi.getAll(requestedScopes)

      if (requestedScopes === undefined) {
        if (generation !== latestFullLoadGeneration) return

        set((state) => {
          const nextScopes = { ...result.scopes }
          for (const [scope, partialGeneration] of latestPartialLoadGenerations) {
            if (partialGeneration <= generation) continue

            if (Object.hasOwn(state.scopes, scope)) {
              nextScopes[scope] = state.scopes[scope]
            } else {
              delete nextScopes[scope]
            }
          }
          return { scopes: nextScopes }
        })
        return
      }

      if (generation < latestFullLoadGeneration) return

      set((state) => {
        const nextScopes = { ...state.scopes }
        for (const scope of requestedScopes) {
          if (latestPartialLoadGenerations.get(scope) !== generation) continue

          if (Object.hasOwn(result.scopes, scope)) {
            nextScopes[scope] = result.scopes[scope]
          }
        }
        return { scopes: nextScopes }
      })
    } finally {
      activeLoadCount -= 1
      set({ loading: activeLoadCount > 0 })
    }
  },

  setScope: (scope, value) => {
    latestPartialLoadGenerations.set(scope, ++nextLoadGeneration)
    set((state) => ({
      scopes: { ...state.scopes, [scope]: value }
    }))
  },

  save: async (scopes) => {
    const target = scopes ?? (Object.keys(get().scopes) as SettingsScope[])
    await settingsApi.save(target)
    await get().load(target)
  }
}))

/** Subscribe to settings.changed and auto-reload affected scopes. */
export function initSettingsSync(): () => void {
  return settingsApi.onChanged(({ scope }) => {
    if (!isSettingsScope(scope)) return

    void useSettingsStore.getState().load([scope])
  })
}
