import type { OptimizationActionDefinition } from '../api/optimization'

/*
 * Port of WindowsOptimizationPage/OptimizationToggleActionHelper.cs.
 *
 * Some optimization features are exposed as a pair of toggle actions:
 * "<base>.enable" (turn the feature on) and "<base>.disable" (turn it off).
 * The pair must be presented as a single row — the side matching the current
 * machine state — otherwise the user would see two contradictory checkboxes.
 */

const ENABLE_SUFFIX = '.enable'
const DISABLE_SUFFIX = '.disable'

export function isEnableAction(key: string): boolean {
  return key.toLowerCase().endsWith(ENABLE_SUFFIX)
}

export function isDisableAction(key: string): boolean {
  return key.toLowerCase().endsWith(DISABLE_SUFFIX)
}

export function getTogglePairBaseKey(key: string): string | null {
  if (isEnableAction(key)) return key.slice(0, -ENABLE_SUFFIX.length)
  if (isDisableAction(key)) return key.slice(0, -DISABLE_SUFFIX.length)
  return null
}

export interface ToggleActionPair {
  baseKey: string
  enable: OptimizationActionDefinition
  disable: OptimizationActionDefinition
}

export function findTogglePairs(actions: readonly OptimizationActionDefinition[]): ToggleActionPair[] {
  const byKey = new Map<string, OptimizationActionDefinition>()
  for (const action of actions) byKey.set(action.key.toLowerCase(), action)

  const pairs: ToggleActionPair[] = []
  const seen = new Set<string>()
  for (const action of actions) {
    const baseKey = getTogglePairBaseKey(action.key)
    if (baseKey === null || seen.has(baseKey.toLowerCase())) continue
    seen.add(baseKey.toLowerCase())

    const enable = byKey.get(`${baseKey}${ENABLE_SUFFIX}`.toLowerCase())
    const disable = byKey.get(`${baseKey}${DISABLE_SUFFIX}`.toLowerCase())
    if (enable && disable) pairs.push({ baseKey, enable, disable })
  }
  return pairs
}

export function findTogglePair(
  action: OptimizationActionDefinition,
  actions: readonly OptimizationActionDefinition[]
): ToggleActionPair | null {
  if (getTogglePairBaseKey(action.key) === null) return null
  return (
    findTogglePairs(actions).find(
      (pair) => pair.enable.key === action.key || pair.disable.key === action.key
    ) ?? null
  )
}

/**
 * The feature's applied state, derived from the pair's reported states.
 * Enable reports the feature state while disable reports its inverse. A probe
 * may be unavailable on either side; contradictory known probes are unknown.
 */
export function getTogglePairFeatureState(pair: ToggleActionPair): boolean | null {
  const { enable, disable } = pair
  const stateFromEnable = enable.applied
  const stateFromDisable = disable.applied === null ? null : !disable.applied
  if (
    stateFromEnable !== null &&
    stateFromDisable !== null &&
    stateFromEnable !== stateFromDisable
  ) {
    return null
  }
  return stateFromEnable ?? stateFromDisable
}

export interface TogglePairPresentation {
  showEnable: boolean
  showDisable: boolean
  /** False when the feature state is unknown: the pair is not editable. */
  canEdit: boolean
  /** The visible action key, i.e. the key that should be sent when applying. */
  visibleKey: string
}

export function resolveTogglePairPresentation(pair: ToggleActionPair): TogglePairPresentation {
  const featureEnabled = getTogglePairFeatureState(pair)
  if (featureEnabled == null) {
    return { showEnable: true, showDisable: false, canEdit: false, visibleKey: pair.enable.key }
  }
  if (featureEnabled) {
    return { showEnable: false, showDisable: true, canEdit: true, visibleKey: pair.disable.key }
  }
  return { showEnable: true, showDisable: false, canEdit: true, visibleKey: pair.enable.key }
}

/** Recommended state of an action, preferring the enable side of a pair. */
export function getRecommendedSelectedState(
  action: OptimizationActionDefinition,
  actions: readonly OptimizationActionDefinition[]
): boolean {
  const pair = findTogglePair(action, actions)
  if (pair === null) return action.recommended
  if (pair.enable.recommended !== pair.disable.recommended) return pair.enable.recommended
  return action.recommended
}

export interface PresentedCategoryAction {
  action: OptimizationActionDefinition
  editable: boolean
}

export interface CategoryActionPresentation {
  visible: PresentedCategoryAction[]
  recommendedKeys: string[]
}

/**
 * Renders a category's action list as the user should see it: toggle pairs
 * collapse to the single row matching the current feature state, rows with an
 * unknown applied state are read-only, and recommended keys account for pairs.
 */
export function presentCategoryActions(
  actions: readonly OptimizationActionDefinition[],
  busy = false
): CategoryActionPresentation {
  const pairs = findTogglePairs(actions)
  const pairByKey = new Map<string, ToggleActionPair>()
  for (const pair of pairs) {
    pairByKey.set(pair.enable.key, pair)
    pairByKey.set(pair.disable.key, pair)
  }

  const visible: PresentedCategoryAction[] = []
  const recommendedKeys: string[] = []
  for (const action of actions) {
    const pair = pairByKey.get(action.key)
    if (pair) {
      const presentation = resolveTogglePairPresentation(pair)
      const isVisibleAction =
        (presentation.showEnable && action.key === pair.enable.key) ||
        (presentation.showDisable && action.key === pair.disable.key)
      if (!isVisibleAction) continue
      visible.push({ action, editable: presentation.canEdit && !busy })
      if (getRecommendedSelectedState(action, actions)) recommendedKeys.push(action.key)
    } else {
      const cleanup = action.key.toLowerCase().startsWith('cleanup.')
      visible.push({ action, editable: (cleanup || action.applied !== null) && !busy })
      if (action.recommended) recommendedKeys.push(action.key)
    }
  }
  return { visible, recommendedKeys }
}
