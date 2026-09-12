import { existsSync, readFileSync } from 'fs'
import { join } from 'path'
import {
  isInstallerOptionalFeature,
  normalizeInstallerDeviceMode,
  normalizeInstallerFeatures,
  normalizeInstallerLanguage,
  parseInstallerFeatureFlag,
  serializeInstallerFeaturesArgument,
  type InstallerOptionalFeature,
  type InstallerSelection
} from '../shared/installer-selection'

export const INSTALLER_SELECTION_FILE_NAME = 'installer-selection.ini'

function installerSelectionPath(): string {
  // The file lives beside the installed resources directory. Keeping it beside
  // the application makes the selection follow a custom install directory and
  // avoids requiring the renderer to read or write a machine-wide settings ACL.
  return join(process.resourcesPath, '..', INSTALLER_SELECTION_FILE_NAME)
}

/** Parse the small INI file written by the per-machine installer. */
export function parseInstallerSelectionIni(contents: string): InstallerSelection | null {
  let section = ''
  let language: string | undefined
  let deviceMode: string | undefined
  const rawFeatures: Partial<Record<InstallerOptionalFeature, boolean>> = {}

  for (const rawLine of contents.split(/\r?\n/)) {
    const line = rawLine.trim()
    if (line.length === 0 || line.startsWith(';') || line.startsWith('#')) continue
    if (line.startsWith('[') && line.endsWith(']')) {
      section = line.slice(1, -1).trim().toLowerCase()
      continue
    }
    if (section !== 'installation') continue
    const separator = line.indexOf('=')
    if (separator < 0) continue
    const key = line.slice(0, separator).trim()
    const value = line.slice(separator + 1).trim()
    const normalizedKey = key.toLowerCase()
    if (normalizedKey === 'language') language = value
    if (normalizedKey === 'devicemode') deviceMode = value
    if (isInstallerOptionalFeature(key)) {
      const flag = parseInstallerFeatureFlag(value)
      if (flag !== undefined) rawFeatures[key] = flag
    }
  }

  const normalizedLanguage = normalizeInstallerLanguage(language)
  const normalizedDeviceMode = normalizeInstallerDeviceMode(deviceMode)
  if (normalizedLanguage == null || normalizedDeviceMode == null) return null
  return {
    language: normalizedLanguage,
    deviceMode: normalizedDeviceMode,
    features: normalizeInstallerFeatures(rawFeatures)
  }
}

export function readInstallerSelection(): InstallerSelection | null {
  const path = installerSelectionPath()
  try {
    if (!existsSync(path)) return null
    return parseInstallerSelectionIni(readFileSync(path, 'utf8'))
  } catch (error) {
    console.warn(`[main] unable to read installer selection from ${path}:`, error)
    return null
  }
}

export function buildInstallerRendererArguments(selection: InstallerSelection): string[] {
  return [
    `--udt-installer-language=${selection.language}`,
    `--udt-installer-device-mode=${selection.deviceMode}`,
    serializeInstallerFeaturesArgument(selection.features)
  ]
}

export function buildInstallerHostArguments(selection: InstallerSelection | null): string[] {
  const args: string[] = []
  if (selection?.deviceMode === 'basic') args.push('--no-hardware')
  return args
}
