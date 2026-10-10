import { isInstallerOptionalFeatureEnabled, type InstallerFeatures } from './installer-selection'

export type NavigationFeature = 'automation' | 'macro' | 'keyboard' | 'windowsOptimization'
export type NavigationCapabilities = Partial<Record<'automation' | 'macro' | 'keyboard' | 'optimization', boolean>>

const CAPABILITY_KEYS = ['automation', 'macro', 'keyboard', 'optimization'] as const

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === 'object' && !Array.isArray(value)
}

/** Missing capabilities keep older Hosts usable; only explicit false hides an entry. */
export function readNavigationCapabilities(value: unknown): NavigationCapabilities {
  if (!isRecord(value) || !isRecord(value.capabilities)) return {}
  const capabilities: NavigationCapabilities = {}
  for (const key of CAPABILITY_KEYS) {
    const supported = value.capabilities[key]
    if (typeof supported === 'boolean') capabilities[key] = supported
  }
  return capabilities
}

export function isNavigationFeatureVisible(
  feature: NavigationFeature,
  installedFeatures: InstallerFeatures | null | undefined,
  visibility: Readonly<Record<string, boolean>>,
  capabilities: NavigationCapabilities | null | undefined
): boolean {
  const capability = feature === 'windowsOptimization' ? 'optimization' : feature
  return isInstallerOptionalFeatureEnabled(installedFeatures, feature)
    && visibility[feature] !== false
    && capabilities?.[capability] !== false
}

/** The merged Actions entry remains visible while either child entry is usable. */
export function isActionsNavigationVisible(
  installedFeatures: InstallerFeatures | null | undefined,
  visibility: Readonly<Record<string, boolean>>,
  capabilities: NavigationCapabilities | null | undefined
): boolean {
  return visibility.actions !== false && (
    isNavigationFeatureVisible('automation', installedFeatures, visibility, capabilities)
    || isNavigationFeatureVisible('macro', installedFeatures, visibility, capabilities)
  )
}
