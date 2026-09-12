import { listPackage } from '@electron/asar'
import { existsSync } from 'node:fs'
import { mkdir, readdir, stat, writeFile } from 'node:fs/promises'
import { basename, dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MEBIBYTE = 1024 * 1024
const moduleDirectory = dirname(fileURLToPath(import.meta.url))

export const CHROMIUM_LOCALES = Object.freeze([
  'en-US', 'zh-CN', 'zh-TW', 'ja', 'de', 'fr', 'es', 'it', 'pt-BR', 'pt-PT',
  'ru', 'uk', 'pl', 'cs', 'sk', 'hu', 'ro', 'bg', 'tr', 'el', 'ar', 'lv', 'nl', 'vi'
])
const CHROMIUM_LOCALE_SET = new Set(CHROMIUM_LOCALES)
export const BUDGETS = Object.freeze({
  appAsar: 15 * MEBIBYTE,
  chromiumLocales: 20 * MEBIBYTE,
  // The Windows Desktop (WinForms) stack is no longer shipped with the host;
  // screen geometry and message windows come from CsWin32 P/Invoke instead.
  host: Object.freeze({ 'win-x64': 130 * MEBIBYTE, 'linux-x64': 92 * MEBIBYTE, 'osx-x64': 100 * MEBIBYTE, 'osx-arm64': 100 * MEBIBYTE }),
  unpacked: Object.freeze({ 'win-x64': 470 * MEBIBYTE, 'linux-x64': 450 * MEBIBYTE, 'osx-x64': 500 * MEBIBYTE, 'osx-arm64': 500 * MEBIBYTE }),
  distributable: 185 * MEBIBYTE,
  onlineBootstrap: 85 * MEBIBYTE
})

function toMebibytes(bytes) {
  return Number((bytes / MEBIBYTE).toFixed(2))
}

export function expectedRuntimeIdentifier(platform, architecture) {
  const normalizedArchitecture = architecture === 3 || architecture === '3' || architecture === 'arm64' ? 'arm64' : architecture === 1 || architecture === '1' || architecture === 'x64' ? 'x64' : architecture
  if (platform === 'win32' || platform === 'win' || platform === 'windows') return 'win-x64'
  if (platform === 'linux') return 'linux-x64'
  if ((platform === 'darwin' || platform === 'mac' || platform === 'osx') && (normalizedArchitecture === 'x64' || normalizedArchitecture === 'arm64')) return `osx-${normalizedArchitecture}`
  throw new Error(`Unsupported Electron package target: platform=${platform} arch=${architecture}`)
}

async function directorySize(directory) {
  let total = 0
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) total += await directorySize(path)
    else if (entry.isFile()) total += (await stat(path)).size
  }
  return total
}

async function filesNamed(directory, predicate) {
  const matches = []
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) matches.push(...await filesNamed(path, predicate))
    else if (entry.isFile() && predicate(entry.name, path)) matches.push(path)
  }
  return matches
}

async function resolveResourcesDirectory(appOutDirectory) {
  const candidates = [
    join(appOutDirectory, 'resources'),
    join(appOutDirectory, 'Contents', 'Resources')
  ]
  if (!candidates.some(existsSync)) {
    try {
      const entries = await readdir(appOutDirectory, { withFileTypes: true })
      for (const entry of entries) {
        if (entry.isDirectory() && entry.name.endsWith('.app')) {
          candidates.push(join(appOutDirectory, entry.name, 'Contents', 'Resources'))
        }
      }
    } catch {
      /* ignore */
    }
  }
  const resourcesDirectory = candidates.find(existsSync)
  if (!resourcesDirectory) throw new Error(`Packaged application has no resources directory: ${appOutDirectory}`)
  return resourcesDirectory
}

async function findChromiumLocaleDirectory(appOutDirectory) {
  const candidates = []
  async function visit(directory, depth) {
    if (depth > 8) return
    const entries = await readdir(directory, { withFileTypes: true })
    const localeFiles = entries.filter(entry => entry.isFile() && entry.name.endsWith('.pak'))
    if (localeFiles.length > 0 && basename(directory).toLowerCase() === 'locales') candidates.push(directory)
    const lprojDirs = entries.filter(entry => entry.isDirectory() && entry.name.endsWith('.lproj'))
    if (lprojDirs.length > 0 && candidates.length === 0 && (basename(directory).toLowerCase() === 'resources' || basename(directory).toLowerCase().includes('electron framework'))) {
      candidates.push(directory)
    }
    for (const entry of entries) {
      if (entry.isDirectory()) await visit(join(directory, entry.name), depth + 1)
    }
  }
  await visit(appOutDirectory, 0)
  if (candidates.length === 0) {
    return resolveResourcesDirectory(appOutDirectory)
  }
  return candidates[0]
}

function asarEntries(asarPath) {
  return listPackage(asarPath, { isPack: true })
    .map(entry => entry.replace(/^(?:pack|unpack)\s*:\s*/i, '').replace(/^[\\/]+/, '').replaceAll('\\', '/'))
}

export function artifactBudgetFor(path) {
  const name = basename(path).toLowerCase()
  return name.includes('onlinesetup') || name.includes('_online_setup') ? BUDGETS.onlineBootstrap : BUDGETS.distributable
}

export async function auditArtifactFiles(artifactPaths, reportDirectory) {
  const artifacts = []
  const failures = []
  for (const artifactPath of artifactPaths) {
    const absolutePath = resolve(artifactPath)
    const bytes = (await stat(absolutePath)).size
    const budgetBytes = artifactBudgetFor(absolutePath)
    const result = { path: absolutePath, bytes, mebibytes: toMebibytes(bytes), budgetBytes, budgetMebibytes: toMebibytes(budgetBytes) }
    artifacts.push(result)
    if (bytes > budgetBytes) failures.push(`${basename(absolutePath)} is ${result.mebibytes} MiB; budget is ${result.budgetMebibytes} MiB`)
  }
  const report = { schemaVersion: 1, kind: 'artifacts', generatedAt: new Date().toISOString(), artifacts, failures }
  if (reportDirectory) await writeReport(reportDirectory, 'artifacts.json', report)
  if (failures.length > 0) throw new Error(`Package footprint audit failed:\n${failures.join('\n')}`)
  return report
}

async function writeReport(reportDirectory, filename, report) {
  await mkdir(reportDirectory, { recursive: true })
  await writeFile(join(reportDirectory, filename), `${JSON.stringify(report, null, 2)}\n`, 'utf8')
}

export async function auditPackagedApplication(options) {
  const appOutDirectory = resolve(options.appOutDirectory)
  const runtimeIdentifier = options.runtimeIdentifier ?? expectedRuntimeIdentifier(options.electronPlatformName, options.arch)
  if (!Object.hasOwn(BUDGETS.host, runtimeIdentifier) || !Object.hasOwn(BUDGETS.unpacked, runtimeIdentifier)) {
    throw new Error(`Unsupported package footprint runtime identifier: ${runtimeIdentifier}`)
  }
  const reportDirectory = options.reportDirectory ?? join(dirname(moduleDirectory), 'dist', 'footprint')
  const failures = []
  const resourcesDirectory = await resolveResourcesDirectory(appOutDirectory)
  const asarPath = join(resourcesDirectory, 'app.asar')
  const hostDirectory = join(resourcesDirectory, 'host')
  const localeDirectory = await findChromiumLocaleDirectory(appOutDirectory)

  const appAsarBytes = (await stat(asarPath)).size
  const appAsarEntries = asarEntries(asarPath)
  const nodeModuleEntries = appAsarEntries.filter(entry => entry.split('/').includes('node_modules'))
  const localeEntries = await readdir(localeDirectory, { withFileTypes: true })
  const pakFiles = localeEntries.filter(entry => entry.isFile() && entry.name.endsWith('.pak'))
  const lprojDirs = localeEntries.filter(entry => entry.isDirectory() && entry.name.endsWith('.lproj'))
  const localeNames = (pakFiles.length > 0 ? pakFiles.map(entry => basename(entry.name, '.pak')) : lprojDirs.map(entry => basename(entry.name, '.lproj'))).sort()
  const localeBytes = pakFiles.length > 0
    ? (await Promise.all(pakFiles.map(entry => stat(join(localeDirectory, entry.name))))).reduce((total, info) => total + info.size, 0)
    : (await Promise.all(lprojDirs.map(entry => directorySize(join(localeDirectory, entry.name))))).reduce((total, size) => total + size, 0)
  const hostBytes = await directorySize(hostDirectory)
  const hostPdbPaths = await filesNamed(hostDirectory, name => name.toLowerCase().endsWith('.pdb'))
  const unpackedBytes = await directorySize(appOutDirectory)
  const expectedLocales = [...CHROMIUM_LOCALES].sort()
  const missingLocales = expectedLocales.filter(locale => !localeNames.includes(locale))
  const unexpectedLocales = localeNames.filter(locale => !CHROMIUM_LOCALE_SET.has(locale))

  const isDarwin = runtimeIdentifier.startsWith('osx-')
  if (nodeModuleEntries.length > 0) failures.push(`app.asar contains node_modules entries: ${nodeModuleEntries.slice(0, 10).join(', ')}`)
  if (appAsarBytes > BUDGETS.appAsar) failures.push(`app.asar is ${toMebibytes(appAsarBytes)} MiB; budget is ${toMebibytes(BUDGETS.appAsar)} MiB`)
  if (localeBytes > BUDGETS.chromiumLocales) failures.push(`Chromium locales are ${toMebibytes(localeBytes)} MiB; budget is ${toMebibytes(BUDGETS.chromiumLocales)} MiB`)
  if (!isDarwin && (missingLocales.length > 0 || unexpectedLocales.length > 0)) failures.push(`Chromium locale mismatch; missing=${missingLocales.join(',') || 'none'} extra=${unexpectedLocales.join(',') || 'none'}`)
  if (hostPdbPaths.length > 0) failures.push(`Host contains PDB files: ${hostPdbPaths.map(path => relative(hostDirectory, path)).join(', ')}`)
  if (hostBytes > BUDGETS.host[runtimeIdentifier]) failures.push(`Host is ${toMebibytes(hostBytes)} MiB; budget is ${toMebibytes(BUDGETS.host[runtimeIdentifier])} MiB`)
  if (unpackedBytes > BUDGETS.unpacked[runtimeIdentifier]) failures.push(`Unpacked application is ${toMebibytes(unpackedBytes)} MiB; budget is ${toMebibytes(BUDGETS.unpacked[runtimeIdentifier])} MiB`)

  const report = {
    schemaVersion: 1,
    kind: 'application',
    generatedAt: new Date().toISOString(),
    runtimeIdentifier,
    appOutDirectory,
    paths: { appAsar: asarPath, host: hostDirectory, chromiumLocales: localeDirectory },
    budgets: BUDGETS,
    measurements: {
      appAsarBytes,
      appAsarMebibytes: toMebibytes(appAsarBytes),
      appAsarEntryCount: appAsarEntries.length,
      nodeModuleEntries,
      chromiumLocaleBytes: localeBytes,
      chromiumLocaleMebibytes: toMebibytes(localeBytes),
      chromiumLocales: localeNames,
      hostBytes,
      hostMebibytes: toMebibytes(hostBytes),
      hostPdbPaths: hostPdbPaths.map(path => relative(hostDirectory, path)),
      unpackedBytes,
      unpackedMebibytes: toMebibytes(unpackedBytes)
    },
    failures
  }
  await writeReport(reportDirectory, `${options.reportName ?? runtimeIdentifier}.json`, report)
  if (failures.length > 0) throw new Error(`Package footprint audit failed:\n${failures.join('\n')}`)
  return report
}

export async function afterPack(context) {
  await auditPackagedApplication({
    appOutDirectory: context.appOutDir,
    electronPlatformName: context.electronPlatformName,
    arch: context.arch
  })
}

async function runCli() {
  const cliArguments = process.argv.slice(2)
  const reportDirectory = join(dirname(moduleDirectory), 'dist', 'footprint')
  if (cliArguments[0] === '--app') {
    const appOutDirectory = cliArguments[1]
    const runtimeIdentifierIndex = cliArguments.indexOf('--rid')
    const runtimeIdentifier = runtimeIdentifierIndex === -1 ? undefined : cliArguments[runtimeIdentifierIndex + 1]
    if (!appOutDirectory || (runtimeIdentifierIndex !== -1 && !runtimeIdentifier)) {
      throw new Error('Usage: node scripts/package-footprint.mjs --app <app-out-dir> [--rid <runtime-identifier>]')
    }
    await auditPackagedApplication({
      appOutDirectory,
      runtimeIdentifier,
      reportDirectory,
      reportName: `payload-${basename(resolve(appOutDirectory))}`
    })
    return
  }
  if (cliArguments.length === 0) throw new Error('Usage: node scripts/package-footprint.mjs <artifact> [<artifact>...]')
  await auditArtifactFiles(cliArguments, reportDirectory)
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  runCli().catch(error => {
    console.error(error.stack ?? error.message)
    process.exitCode = 1
  })
}
