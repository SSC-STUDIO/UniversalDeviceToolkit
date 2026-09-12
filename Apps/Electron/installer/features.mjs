/** Optional modules the custom installer can omit. Keep IDs aligned with src/shared/installer-selection.ts. */
export const OPTIONAL_FEATURES = [
  'windowsOptimization',
  'networkAcceleration',
  'automation',
  'macro',
  'keyboard'
]

export function defaultFeatures() {
  return {
    windowsOptimization: true,
    networkAcceleration: true,
    automation: true,
    macro: true,
    keyboard: true
  }
}

export function normalizeFeatures(raw) {
  const features = defaultFeatures()
  if (raw == null || typeof raw !== 'object') return features
  for (const key of OPTIONAL_FEATURES) {
    if (raw[key] === false || raw[key] === 0 || raw[key] === '0') features[key] = false
  }
  if (!features.windowsOptimization) features.networkAcceleration = false
  return features
}

export function featureFlag(value) {
  return value ? '1' : '0'
}

export function isNetworkProxySidecarFile(relativePath) {
  const name = String(relativePath).replaceAll('\\', '/').split('/').pop()?.toLowerCase() ?? ''
  return name === 'universaldevicetoolkit.networkproxy' || name.startsWith('universaldevicetoolkit.networkproxy.')
}
