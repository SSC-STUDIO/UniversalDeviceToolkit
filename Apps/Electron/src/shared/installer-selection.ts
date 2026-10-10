/**
 * Values exchanged between the setup wizard and the Electron processes.
 * Keep this module platform-neutral so both the main and preload bundles can
 * validate installer input without sharing filesystem or Electron code.
 */
export const INSTALLER_LANGUAGES = [
  'en',
  'zh-CN',
  'zh-Hant',
  'ja',
  'de',
  'fr',
  'es',
  'it',
  'pt-BR',
  'pt',
  'ru',
  'uk',
  'pl',
  'cs',
  'sk',
  'hu',
  'ro',
  'bg',
  'tr',
  'el',
  'ar',
  'lv',
  'nl-NL',
  'vi',
  'uz-Latn-UZ'
] as const

export type InstallerLanguage = (typeof INSTALLER_LANGUAGES)[number]
export type InstallerDeviceMode = 'auto' | 'basic'

/** Optional modules the installer can omit. Required pieces are never listed. */
export const INSTALLER_OPTIONAL_FEATURES = [
  'windowsOptimization',
  'networkAcceleration',
  'automation',
  'macro',
  'keyboard'
] as const

export type InstallerOptionalFeature = (typeof INSTALLER_OPTIONAL_FEATURES)[number]

export type InstallerFeatures = Record<InstallerOptionalFeature, boolean>

export interface InstallerSelection {
  language: InstallerLanguage
  deviceMode: InstallerDeviceMode
  features: InstallerFeatures
}

export function defaultInstallerFeatures(): InstallerFeatures {
  return {
    windowsOptimization: true,
    networkAcceleration: true,
    automation: true,
    macro: true,
    keyboard: true
  }
}

export function isInstallerOptionalFeature(value: string): value is InstallerOptionalFeature {
  return (INSTALLER_OPTIONAL_FEATURES as readonly string[]).includes(value)
}

/**
 * Missing keys stay on (full install / older INI files). Network acceleration
 * is a tab plus the NetworkProxy sidecar, so it cannot be installed without
 * the System Optimization page that hosts it.
 */
export function normalizeInstallerFeatures(
  raw: Partial<Record<string, boolean>> | null | undefined
): InstallerFeatures {
  const features = defaultInstallerFeatures()
  if (raw == null) return features
  for (const key of INSTALLER_OPTIONAL_FEATURES) {
    if (raw[key] === false) features[key] = false
  }
  if (!features.windowsOptimization) features.networkAcceleration = false
  return features
}

/** Unrecognized or missing feature names are treated as installed. */
export function isInstallerOptionalFeatureEnabled(
  features: InstallerFeatures | null | undefined,
  feature: string
): boolean {
  if (features == null || !isInstallerOptionalFeature(feature)) return true
  return features[feature] !== false
}

export function parseInstallerFeatureFlag(value: string | undefined): boolean | undefined {
  if (value == null) return undefined
  const normalized = value.trim().toLowerCase()
  if (normalized === '0' || normalized === 'false' || normalized === 'no' || normalized === 'off') {
    return false
  }
  if (normalized === '1' || normalized === 'true' || normalized === 'yes' || normalized === 'on') {
    return true
  }
  return undefined
}

export function serializeInstallerFeaturesArgument(features: InstallerFeatures): string {
  const enabled = INSTALLER_OPTIONAL_FEATURES.filter((key) => features[key])
  return `--udt-installer-features=${enabled.join(',')}`
}

export function parseInstallerFeaturesArgument(value: string): InstallerFeatures {
  const selected = new Set(
    value
      .split(',')
      .map((item) => item.trim())
      .filter((item) => item.length > 0)
  )
  const raw: Partial<Record<InstallerOptionalFeature, boolean>> = {}
  for (const key of INSTALLER_OPTIONAL_FEATURES) {
    raw[key] = selected.has(key)
  }
  return normalizeInstallerFeatures(raw)
}

export function normalizeInstallerLanguage(value: string | undefined): InstallerLanguage | null {
  if (value == null) return null
  return (INSTALLER_LANGUAGES as readonly string[]).includes(value)
    ? (value as InstallerLanguage)
    : null
}

export function normalizeInstallerDeviceMode(value: string | undefined): InstallerDeviceMode | null {
  if (value === 'auto' || value === 'basic') return value
  return null
}

/** Sidecar files the installer can omit when Network Acceleration is unchecked. */
export function isNetworkProxySidecarFile(relativePath: string): boolean {
  const name = relativePath.replaceAll('\\', '/').split('/').pop()?.toLowerCase() ?? ''
  return name === 'universaldevicetoolkit.networkproxy' || name.startsWith('universaldevicetoolkit.networkproxy.')
}

/** Parse the explicit, validated switches passed by Electron main. */
export function parseInstallerSelectionArguments(args: readonly string[]): InstallerSelection | null {
  let language: InstallerLanguage | null = null
  let deviceMode: InstallerDeviceMode | null = null
  let features: InstallerFeatures | undefined

  for (const argument of args) {
    const languagePrefix = '--udt-installer-language='
    const deviceModePrefix = '--udt-installer-device-mode='
    const featuresPrefix = '--udt-installer-features='
    if (argument.startsWith(languagePrefix)) {
      language = normalizeInstallerLanguage(argument.slice(languagePrefix.length))
    } else if (argument.startsWith(deviceModePrefix)) {
      deviceMode = normalizeInstallerDeviceMode(argument.slice(deviceModePrefix.length))
    } else if (argument.startsWith(featuresPrefix)) {
      features = parseInstallerFeaturesArgument(argument.slice(featuresPrefix.length))
    }
  }

  if (language == null || deviceMode == null) return null
  return { language, deviceMode, features: features ?? defaultInstallerFeatures() }
}
