/**
 * Update downloader — Electron-side counterpart of the Host UpdateChecker.
 * The host only reports availability/version; the main process resolves the
 * release asset from the GitHub API, downloads it with progress events and
 * launches the installer (Windows: NSIS `/S`; macOS: `open` the .dmg;
 * Linux: run the AppImage).
 */
import { createHash } from 'crypto'
import { app } from 'electron'
import {
  chmodSync,
  createReadStream,
  createWriteStream,
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  unlinkSync
} from 'fs'
import { homedir } from 'os'
import { join, resolve, sep } from 'path'
import { spawn } from 'child_process'
import { get as httpsGet } from 'https'

const REPO_OWNER = 'SSC-STUDIO'
const REPO_NAME = 'UniversalDeviceToolkit'
const API_RELEASES = `https://api.github.com/repos/${REPO_OWNER}/${REPO_NAME}/releases?per_page=10`
const USER_AGENT = 'UniversalDeviceToolkit-Electron'
// Legacy plugin-catalog release tags. The plugin system is retired, but these
// tags still exist on GitHub and must never surface as application updates.
const CATALOG_STABLE = 'plugin-catalog'
const CATALOG_PREVIEW = 'plugin-catalog-preview'
const SHA256_TOKEN = /(?<![a-fA-F0-9])([a-fA-F0-9]{64})(?![a-fA-F0-9])/i

export type InstallChannel = 'full' | 'online'

interface GitHubReleaseAsset {
  name?: string
  browser_download_url?: string
  size?: number
  digest?: string
}

interface GitHubRelease {
  tag_name?: string
  draft?: boolean
  prerelease?: boolean
  published_at?: string
  body?: string
  assets?: GitHubReleaseAsset[]
}

interface VerifiedInstaller {
  path: string
  sha256: string
}

let verifiedInstaller: VerifiedInstaller | null = null

/** Packed by Scripts/Build-ElectronInstaller.ps1 (`full` or `online`). */
export function readInstallChannel(): InstallChannel {
  const candidates = [
    join(process.resourcesPath, 'install-channel'),
    join(app.getAppPath(), 'resources', 'install-channel')
  ]
  for (const candidate of candidates) {
    try {
      if (!existsSync(candidate)) continue
      const value = readFileSync(candidate, 'utf8').trim().toLowerCase()
      if (value === 'online') return 'online'
      if (value === 'full') return 'full'
    } catch {
      // Keep looking; missing markers default to Full.
    }
  }
  return 'full'
}

/** electron-builder artifact name per platform: NSIS .exe / .dmg / .AppImage. */
function assetPatternForPlatform(): RegExp {
  if (process.platform === 'darwin') return /UniversalDeviceToolkit.*\.dmg$/i
  if (process.platform === 'linux') return /UniversalDeviceToolkit.*\.AppImage$/i
  if (readInstallChannel() === 'online') {
    return /UniversalDeviceToolkit_v.+_Online_Setup\.exe$|UniversalDeviceToolkitOnlineSetup-.+\.exe$/i
  }
  return /UniversalDeviceToolkit_v.+_Full_Setup\.exe$|UniversalDeviceToolkitSetup-.+\.exe$/i
}

export interface UpdateReleaseInfo {
  version: string
  url: string
  assetUrl: string
  assetName: string
  assetSize: number
  releaseNotes: string | null
  releaseDate: string | null
  assetDigest: string | null
  sha256Url: string | null
}

export interface DownloadProgress {
  percent: number
  receivedBytes: number
  totalBytes: number
  done: boolean
  error?: string
}

function isCatalogTag(tag: string | undefined): boolean {
  if (tag == null || tag.length === 0) return false
  return tag.toLowerCase() === CATALOG_STABLE || tag.toLowerCase() === CATALOG_PREVIEW
}

function isPrereleaseApplicationVersion(tag: string | undefined): boolean {
  if (tag == null || tag.trim().length === 0) return false
  let core = tag.trim()
  if (core.startsWith('v') || core.startsWith('V')) core = core.slice(1)
  const plus = core.indexOf('+')
  if (plus >= 0) core = core.slice(0, plus)
  return core.includes('-')
}

function isPublicApplicationRelease(release: GitHubRelease, includePrerelease: boolean): boolean {
  if (release.draft === true || isCatalogTag(release.tag_name)) return false
  const prerelease = release.prerelease === true || isPrereleaseApplicationVersion(release.tag_name)
  return includePrerelease || !prerelease
}

function hostAppDataDirectory(): string {
  const override = process.env['UDT_APPDATA_OVERRIDE']
  if (override != null && override.trim().length > 0) return override
  if (process.platform === 'win32') {
    return join(process.env['LOCALAPPDATA'] ?? app.getPath('userData'), 'UniversalDeviceToolkit')
  }
  const xdg = process.env['XDG_CONFIG_HOME']
  const configHome = xdg != null && xdg.trim().length > 0 ? xdg : join(homedir(), '.config')
  return join(configHome, 'UniversalDeviceToolkit')
}

/** Mirrors Host UpdateCheckSettings.IncludePrereleaseUpdates (update_check.json). */
function readIncludePrereleaseUpdates(): boolean {
  const settingsPath = join(hostAppDataDirectory(), 'update_check.json')
  try {
    if (!existsSync(settingsPath)) return false
    const parsed = JSON.parse(readFileSync(settingsPath, 'utf8')) as {
      IncludePrereleaseUpdates?: unknown
    }
    return parsed.IncludePrereleaseUpdates === true
  } catch {
    return false
  }
}

function parseNumericVersion(tag: string): { major: number; minor: number; patch: number } | null {
  let core = tag.trim()
  if (core.startsWith('v') || core.startsWith('V')) core = core.slice(1)
  const plus = core.indexOf('+')
  if (plus >= 0) core = core.slice(0, plus)
  const hyphen = core.indexOf('-')
  if (hyphen >= 0) core = core.slice(0, hyphen)
  const parts = core.split('.')
  const major = Number(parts[0])
  const minor = Number(parts[1] ?? '0')
  const patch = Number(parts[2] ?? '0')
  if (!Number.isFinite(major) || !Number.isFinite(minor) || !Number.isFinite(patch)) return null
  return { major, minor, patch }
}

function compareReleaseVersion(left: GitHubRelease, right: GitHubRelease): number {
  const a = parseNumericVersion(left.tag_name ?? '')
  const b = parseNumericVersion(right.tag_name ?? '')
  if (a == null && b == null) return 0
  if (a == null) return 1
  if (b == null) return -1
  if (a.major !== b.major) return b.major - a.major
  if (a.minor !== b.minor) return b.minor - a.minor
  return b.patch - a.patch
}

function safeAssetFileName(name: string): string {
  const normalized = name.replace(/\\/g, '/')
  const slash = normalized.lastIndexOf('/')
  const base = slash >= 0 ? normalized.slice(slash + 1) : normalized
  if (base.length === 0 || base === '.' || base === '..' || base.includes('\0')) {
    throw new Error('Invalid installer asset name')
  }
  return base
}

function samePath(left: string, right: string): boolean {
  const a = resolve(left)
  const b = resolve(right)
  return process.platform === 'win32' ? a.toLowerCase() === b.toLowerCase() : a === b
}

function assertInsideDirectory(filePath: string, directory: string): string {
  const root = resolve(directory)
  const resolved = resolve(filePath)
  const prefix = root.endsWith(sep) ? root : root + sep
  const comparablePath = process.platform === 'win32' ? resolved.toLowerCase() : resolved
  const comparablePrefix = process.platform === 'win32' ? prefix.toLowerCase() : prefix
  const comparableRoot = process.platform === 'win32' ? root.toLowerCase() : root
  if (comparablePath !== comparableRoot && !comparablePath.startsWith(comparablePrefix)) {
    throw new Error('Installer path escapes the updates directory')
  }
  return resolved
}

function parseSha256Digest(digest: string | null | undefined): string | null {
  if (digest == null || digest.trim().length === 0) return null
  const trimmed = digest.trim()
  const prefixed = /^sha256:([a-fA-F0-9]{64})$/i.exec(trimmed)
  if (prefixed?.[1] != null) return prefixed[1].toLowerCase()
  if (/^[a-fA-F0-9]{64}$/.test(trimmed)) return trimmed.toLowerCase()
  return null
}

function tryExtractFirstSha256Hash(text: string): string | null {
  const match = SHA256_TOKEN.exec(text)
  return match?.[1] != null ? match[1].toLowerCase() : null
}

function lineReferencesFileName(line: string, fileName: string): boolean {
  const index = line.toLowerCase().indexOf(fileName.toLowerCase())
  if (index < 0) return false
  if (index > 0) {
    const before = line[index - 1]
    if (before != null && !/\s/.test(before) && !['(', '/', '\\', '*', '=', '"', "'"].includes(before)) {
      return false
    }
  }
  const afterIndex = index + fileName.length
  if (afterIndex < line.length) {
    const after = line[afterIndex]
    if (after != null && !/\s/.test(after) && ![')', '"', "'", ',', ';'].includes(after)) {
      return false
    }
  }
  return true
}

function tryExtractExpectedHash(hashContent: string, packageFileName: string): string | null {
  const lines = hashContent.split(/\r\n|\n|\r/).map((line) => line.trim()).filter((line) => line.length > 0)
  for (const line of lines) {
    if (!lineReferencesFileName(line, packageFileName)) continue
    const lineHash = tryExtractFirstSha256Hash(line)
    if (lineHash != null) return lineHash
  }
  for (const line of lines) {
    const lineHash = tryExtractFirstSha256Hash(line)
    if (lineHash != null && (line.toLowerCase().includes('sha256') || lines.length === 1)) {
      return lineHash
    }
  }
  const unique = new Set<string>()
  for (const line of lines) {
    const lineHash = tryExtractFirstSha256Hash(line)
    if (lineHash != null) unique.add(lineHash)
  }
  if (unique.size === 1) {
    const only = unique.values().next().value
    return only ?? null
  }
  return null
}

function isSha256AssetName(name: string): boolean {
  return name.toLowerCase().endsWith('.sha256') || name.toLowerCase().endsWith('_sha256.txt')
}

function fetchJson(url: string): Promise<unknown> {
  return new Promise((resolveJson, reject) => {
    const request = httpsGet(
      url,
      { headers: { 'User-Agent': USER_AGENT, Accept: 'application/vnd.github+json' }, timeout: 20000 },
      (response) => {
        if (response.statusCode !== 200) {
          response.resume()
          reject(new Error(`GitHub API returned ${response.statusCode ?? 'no status'}`))
          return
        }
        let body = ''
        response.setEncoding('utf8')
        response.on('data', (chunk: string) => {
          body += chunk
        })
        response.on('end', () => {
          try {
            resolveJson(JSON.parse(body))
          } catch (error) {
            reject(error instanceof Error ? error : new Error('Invalid JSON from GitHub API'))
          }
        })
      }
    )
    request.on('error', reject)
    request.on('timeout', () => {
      request.destroy(new Error('GitHub API request timed out'))
    })
  })
}

function fetchText(url: string, maxBytes = 65536): Promise<string> {
  return new Promise((resolveText, reject) => {
    let settled = false
    const settleReject = (error: Error): void => {
      if (settled) return
      settled = true
      reject(error)
    }
    const download = (target: string, redirectsLeft: number): void => {
      const request = httpsGet(target, { headers: { 'User-Agent': USER_AGENT }, timeout: 20000 }, (response) => {
        const status = response.statusCode ?? 0
        if (status >= 300 && status < 400 && response.headers.location != null) {
          response.resume()
          if (redirectsLeft <= 0) {
            settleReject(new Error('Too many redirects'))
            return
          }
          download(new URL(response.headers.location, target).toString(), redirectsLeft - 1)
          return
        }
        if (status !== 200) {
          response.resume()
          settleReject(new Error(`Hash download failed with status ${status}`))
          return
        }
        let body = ''
        let received = 0
        response.setEncoding('utf8')
        response.on('data', (chunk: string) => {
          received += Buffer.byteLength(chunk)
          if (received > maxBytes) {
            request.destroy(new Error('Hash file exceeded size limit'))
            return
          }
          body += chunk
        })
        response.on('end', () => {
          if (settled) return
          settled = true
          resolveText(body)
        })
        response.on('error', settleReject)
      })
      request.on('error', settleReject)
      request.on('timeout', () => {
        request.destroy(new Error('Hash download timed out'))
      })
    }
    download(url, 5)
  })
}

function sha256File(filePath: string): Promise<string> {
  return new Promise((resolveHash, reject) => {
    const hash = createHash('sha256')
    const stream = createReadStream(filePath)
    stream.on('data', (chunk: string | Buffer) => {
      hash.update(chunk)
    })
    stream.on('error', reject)
    stream.on('end', () => resolveHash(hash.digest('hex')))
  })
}

async function resolveExpectedSha256(release: UpdateReleaseInfo): Promise<string> {
  const digestHash = parseSha256Digest(release.assetDigest)
  if (digestHash != null) return digestHash

  if (release.sha256Url != null && release.sha256Url.length > 0) {
    try {
      const hashContent = await fetchText(release.sha256Url)
      const fromFile = tryExtractExpectedHash(hashContent, release.assetName)
      if (fromFile != null) return fromFile
    } catch {
      // Fall through to release notes / fail closed.
    }
  }

  if (release.releaseNotes != null && release.releaseNotes.length > 0) {
    const fromNotes = tryExtractExpectedHash(release.releaseNotes, release.assetName)
    if (fromNotes != null) return fromNotes
  }

  throw new Error(`Update package integrity check failed: no SHA256 hash available for ${release.assetName}`)
}

function tryUnlink(filePath: string): void {
  try {
    if (existsSync(filePath)) unlinkSync(filePath)
  } catch {
    // Best-effort cleanup of a partial or rejected installer.
  }
}

function downloadToFile(url: string, destination: string, onProgress: (progress: DownloadProgress) => void): Promise<string> {
  return new Promise((resolveDownload, reject) => {
    const fail = (error: Error): void => {
      tryUnlink(destination)
      reject(error)
    }
    const download = (target: string, redirectsLeft: number): void => {
      const request = httpsGet(target, { headers: { 'User-Agent': USER_AGENT }, timeout: 60000 }, (response) => {
        const status = response.statusCode ?? 0
        if (status >= 300 && status < 400 && response.headers.location != null) {
          response.resume()
          if (redirectsLeft <= 0) {
            fail(new Error('Too many redirects'))
            return
          }
          download(new URL(response.headers.location, target).toString(), redirectsLeft - 1)
          return
        }
        if (status !== 200) {
          response.resume()
          fail(new Error(`Download failed with status ${status}`))
          return
        }
        const totalBytes = Number(response.headers['content-length'] ?? 0)
        let receivedBytes = 0
        const output = createWriteStream(destination)
        response.on('data', (chunk: Buffer) => {
          receivedBytes += chunk.length
          onProgress({
            percent: totalBytes > 0 ? Math.min(100, (receivedBytes / totalBytes) * 100) : 0,
            receivedBytes,
            totalBytes,
            done: false
          })
        })
        response.on('error', (error) => {
          output.destroy()
          fail(error)
        })
        output.on('error', fail)
        output.on('finish', () => {
          if (totalBytes > 0 && receivedBytes < totalBytes) {
            fail(new Error(`Incomplete download: ${receivedBytes}/${totalBytes} bytes received.`))
            return
          }
          onProgress({ percent: 100, receivedBytes, totalBytes, done: true })
          resolveDownload(destination)
        })
        response.pipe(output)
      })
      request.on('error', fail)
      request.on('timeout', () => {
        request.destroy(new Error('Download timed out'))
      })
    }
    download(url, 5)
  })
}

function updatesDirectory(): string {
  const dir = join(app.getPath('userData'), 'updates')
  mkdirSync(dir, { recursive: true })
  return dir
}

function recordVerifiedInstaller(filePath: string, sha256: string): string {
  const recorded = assertInsideDirectory(filePath, updatesDirectory())
  verifiedInstaller = { path: recorded, sha256 }
  return recorded
}

async function verifyExistingInstaller(destination: string, expectedHash: string): Promise<boolean> {
  if (!existsSync(destination)) return false
  const actual = await sha256File(destination)
  if (actual !== expectedHash) {
    tryUnlink(destination)
    return false
  }
  recordVerifiedInstaller(destination, expectedHash)
  return true
}

function atomicRename(partialPath: string, destination: string): void {
  try {
    renameSync(partialPath, destination)
  } catch {
    tryUnlink(destination)
    renameSync(partialPath, destination)
  }
}

function toReleaseInfo(release: GitHubRelease, asset: GitHubReleaseAsset, assetName: string): UpdateReleaseInfo {
  const sha256Asset = (release.assets ?? []).find(
    (item) => item.name != null && isSha256AssetName(item.name) && item.browser_download_url != null
  )
  return {
    version: release.tag_name ?? 'latest',
    url: `https://github.com/${REPO_OWNER}/${REPO_NAME}/releases/tag/${encodeURIComponent(release.tag_name ?? 'latest')}`,
    assetUrl: asset.browser_download_url ?? '',
    assetName,
    assetSize: asset.size ?? 0,
    releaseNotes: release.body ?? null,
    releaseDate: release.published_at ?? null,
    assetDigest: asset.digest ?? null,
    sha256Url: sha256Asset?.browser_download_url ?? null
  }
}

/** Resolves the newest public application release + Electron setup asset. */
export async function getLatestRelease(): Promise<UpdateReleaseInfo | null> {
  try {
    const payload = await fetchJson(API_RELEASES)
    if (!Array.isArray(payload)) return null
    const includePrerelease = readIncludePrereleaseUpdates()
    const publicReleases = (payload as GitHubRelease[])
      .filter((release) => isPublicApplicationRelease(release, includePrerelease))
      .filter((release) => parseNumericVersion(release.tag_name ?? '') != null)
      .sort(compareReleaseVersion)
    const release = publicReleases[0]
    if (release == null) return null
    const assetPattern = assetPatternForPlatform()
    const asset = (release.assets ?? []).find((item) => item.name != null && assetPattern.test(item.name))
    if (asset?.browser_download_url == null || asset.name == null) {
      return null
    }
    return toReleaseInfo(release, asset, safeAssetFileName(asset.name))
  } catch {
    return null
  }
}

/**
 * Downloads the latest installer to a `.partial` file, verifies SHA256, then
 * atomically renames it into place. Existing same-name files are reused only
 * after the same hash check. Returns the verified local path on success.
 */
export async function downloadLatestUpdate(onProgress: (progress: DownloadProgress) => void): Promise<string> {
  verifiedInstaller = null
  const release = await getLatestRelease()
  if (release == null) {
    throw new Error('No compatible installer asset found in the latest release')
  }
  const expectedHash = await resolveExpectedSha256(release)
  const destination = assertInsideDirectory(join(updatesDirectory(), release.assetName), updatesDirectory())
  if (await verifyExistingInstaller(destination, expectedHash)) {
    onProgress({ percent: 100, receivedBytes: release.assetSize, totalBytes: release.assetSize, done: true })
    return destination
  }
  const partialPath = `${destination}.partial`
  tryUnlink(partialPath)
  try {
    await downloadToFile(release.assetUrl, partialPath, onProgress)
    const actualHash = await sha256File(partialPath)
    if (actualHash !== expectedHash) {
      throw new Error(`Update package integrity check failed. Expected SHA256: ${expectedHash}, computed: ${actualHash}`)
    }
    atomicRename(partialPath, destination)
    return recordVerifiedInstaller(destination, expectedHash)
  } catch (error) {
    tryUnlink(partialPath)
    throw error
  }
}

export interface LaunchInstallerResult {
  ok: boolean
  error?: string
}

/** 6.0.0+ in-app updates always pass NSIS `/S`; the custom installer honors it too. */
export function windowsInstallerLaunchArgs(): string[] {
  return ['/S']
}

/** Mark-of-the-Web ADS that Windows attaches to files downloaded over HTTPS. */
export function zoneIdentifierStreamPath(installerPath: string): string {
  return `${installerPath}:Zone.Identifier`
}

function unblockDownloadedInstaller(filePath: string): void {
  if (process.platform !== 'win32') return
  tryUnlink(zoneIdentifierStreamPath(filePath))
}

function quotePowerShell(value: string): string {
  return `'${value.replaceAll("'", "''")}'`
}

function spawnDetached(
  command: string,
  args: string[],
  options: { windowsHide?: boolean; windowsVerbatimArguments?: boolean } = {}
): Promise<LaunchInstallerResult> {
  return new Promise((resolveLaunch) => {
    const child = spawn(command, args, {
      detached: true,
      stdio: 'ignore',
      windowsHide: options.windowsHide ?? false,
      windowsVerbatimArguments: options.windowsVerbatimArguments ?? false
    })
    child.on('error', (error) => resolveLaunch({ ok: false, error: error.message }))
    child.on('spawn', () => resolveLaunch({ ok: true }))
  })
}

/**
 * 6.0.0 clients `spawn(setup.exe, ['/S'])`. That CreateProcess call fails when
 * the PE requests requireAdministrator (the 6.1 portable/online installer).
 * Current clients try spawn first (keeps 6.0.0-compatible asInvoker builds
 * working), then ShellExecute via Start-Process -Verb RunAs.
 */
async function launchWindowsInstaller(recordedPath: string): Promise<LaunchInstallerResult> {
  unblockDownloadedInstaller(recordedPath)
  const args = windowsInstallerLaunchArgs()
  const direct = await spawnDetached(recordedPath, args)
  if (direct.ok) return direct

  const argumentList = args.map(quotePowerShell).join(',')
  const command =
    args.length > 0
      ? `Start-Process -FilePath ${quotePowerShell(recordedPath)} -Verb RunAs -ArgumentList ${argumentList}`
      : `Start-Process -FilePath ${quotePowerShell(recordedPath)} -Verb RunAs`
  return spawnDetached('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', command], {
    windowsHide: true
  })
}

/**
 * Launches the installer recorded by this session's verified download only.
 * The renderer-supplied path is accepted solely as a match check; any other
 * path is rejected. Windows uses NSIS `/S` (custom installer honors it).
 */
export async function launchInstaller(installerPath: string): Promise<LaunchInstallerResult> {
  const recorded = verifiedInstaller
  if (recorded == null || installerPath.length === 0 || !samePath(installerPath, recorded.path)) {
    return { ok: false, error: 'Installer path is not the verified download.' }
  }
  let recordedPath: string
  try {
    recordedPath = assertInsideDirectory(recorded.path, updatesDirectory())
  } catch {
    return { ok: false, error: 'Installer path escapes the updates directory.' }
  }
  if (!existsSync(recordedPath)) {
    verifiedInstaller = null
    return { ok: false, error: 'The downloaded installer is no longer on disk.' }
  }
  try {
    const actualHash = await sha256File(recordedPath)
    if (actualHash !== recorded.sha256) {
      verifiedInstaller = null
      return { ok: false, error: 'The downloaded installer failed the SHA256 check.' }
    }
  } catch (error) {
    return { ok: false, error: error instanceof Error ? error.message : 'Installer hash check failed.' }
  }

  let result: LaunchInstallerResult
  if (process.platform === 'darwin') {
    result = await spawnDetached('open', [recordedPath])
  } else if (process.platform === 'linux') {
    try {
      chmodSync(recordedPath, 0o755)
    } catch {
      // The spawn error carries the real reason if the bit cannot be set.
    }
    result = await spawnDetached(recordedPath, [])
  } else {
    result = await launchWindowsInstaller(recordedPath)
  }

  if (result.ok) {
    app.quit()
  }
  return result
}
