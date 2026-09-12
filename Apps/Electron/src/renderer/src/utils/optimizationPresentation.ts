import type {
  NetworkAccelerationConfig,
  NetworkAccelerationMode,
  NetworkAccelerationStatus,
  OptimizationActionDefinition,
  OptimizationCategoryDefinition
} from '../api/optimization'

export type OptimizationTabKey =
  | 'optimization'
  | 'cleanup'
  | 'driverDownload'
  | 'networkAcceleration'
  | 'gameBoost'
  | 'cursor'

export function visibleOptimizationTabs(
  tabs: readonly OptimizationTabKey[],
  networkAccelerationInstalled: boolean
): OptimizationTabKey[] {
  if (networkAccelerationInstalled) return [...tabs]
  return tabs.filter((tab) => tab !== 'networkAcceleration')
}

export const NETWORK_ACCELERATION_MODES = [
  'Off',
  'SystemProxy',
  'Hosts',
  'DiagnosticsOnly'
] as const satisfies readonly NetworkAccelerationMode[]

export interface ActionSelectionPresentation {
  checked: boolean
  indeterminate: boolean
}

export function getActionSelectionPresentation(
  action: OptimizationActionDefinition,
  selected: boolean
): ActionSelectionPresentation {
  return {
    checked: action.applied === true || selected,
    indeterminate: action.applied === null
  }
}

export function collectRecommendedActionKeys(
  categories: readonly OptimizationCategoryDefinition[],
  predicate: (categoryKey: string) => boolean
): string[] {
  return categories
    .filter((category) => predicate(category.key))
    .flatMap((category) => category.actions)
    .filter((action) => action.recommended && action.applied !== true)
    .map((action) => action.key)
}

export function getNetworkSelectedTargetCount(
  config: NetworkAccelerationConfig | null
): number {
  if (!config) return 0
  return config.domainGroups.reduce((sum, group) => {
    if (!group.enabled) return sum
    const direct = (group.domains ?? []).filter((domain) => domain.trim().length > 0).length
    const subItems = (group.subItems ?? []).filter((subItem) => subItem.enabled).length
    return sum + direct + subItems
  }, 0)
}

export interface OptimizationPlayState {
  tab: OptimizationTabKey
  busy: boolean
  cleanupSelectedCount: number
  driverSelectedCount: number
  networkStatus: NetworkAccelerationStatus | null
}

export function isOptimizationPlayDisabled({
  tab,
  busy,
  cleanupSelectedCount,
  driverSelectedCount,
  networkStatus
}: OptimizationPlayState): boolean {
  if (busy) return true
  if (tab === 'cleanup') return cleanupSelectedCount === 0
  if (tab === 'driverDownload') return driverSelectedCount === 0
  if (tab !== 'networkAcceleration') return false

  return (
    networkStatus == null ||
    !networkStatus.isBackendReady ||
    networkStatus.config.mode === 'Hosts'
  )
}

/** Skip a new poll while the previous tick is still running. */
export async function runExclusivePoll(
  inFlight: { current: boolean },
  poll: () => Promise<void>
): Promise<boolean> {
  if (inFlight.current) return false
  inFlight.current = true
  try {
    await poll()
    return true
  } finally {
    inFlight.current = false
  }
}

/**
 * Host estimate failures return 0 and set store.error. A real 0-byte scan
 * has no error; treat 0 + error as a failed estimate, not a successful size.
 */
export function isFailedCleanupEstimate(
  bytes: number,
  error: string | null | undefined
): boolean {
  if (!Number.isFinite(bytes) || bytes < 0) return true
  return bytes === 0 && error != null && error !== ''
}

export function resolveActionError(
  error: string | null | undefined,
  fallback: string
): string {
  if (typeof error === 'string' && error.trim() !== '') return error
  return fallback
}

/**
 * Notification-center payload for action failures. Title stays short (the
 * notification item title is single-line ellipsis); the host detail wraps in
 * the message body.
 */
export function presentActionNotification(
  localizedMessage: string,
  fallbackTitle: string
): { title: string; message?: string } {
  const title = fallbackTitle.trim() !== '' ? fallbackTitle.trim() : localizedMessage.trim()
  const detail = localizedMessage.trim()
  if (detail !== '' && detail !== title) {
    return { title, message: detail }
  }
  return { title }
}

/** Hide "no items" placeholders when the list is empty because a load failed. */
export function shouldShowEmptyPlaceholder(options: {
  loading: boolean
  itemCount: number
  error: string | null | undefined
  loaded?: boolean
}): boolean {
  if (options.loading || options.loaded === false) return false
  if (options.error != null && options.error !== '') return false
  return options.itemCount === 0
}
