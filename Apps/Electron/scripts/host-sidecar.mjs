import { existsSync } from 'node:fs'
import { join } from 'node:path'

export const WINDOWS_HOST_TFM = 'net10.0-windows10.0.26100.0'
export const PORTABLE_HOST_TFM = 'net10.0'

export function hostExecutableName(platform = process.platform) {
  return platform === 'win32' ? 'UniversalDeviceToolkit.Host.exe' : 'UniversalDeviceToolkit.Host'
}

/**
 * Framework-dependent .NET apphost requires Foo.runtimeconfig.json (and Foo.deps.json)
 * next to Foo.exe. Without them the runtime treats the exe as self-contained and
 * looks for hostpolicy.dll under Program Files\dotnet.
 */
export function hostSidecarPath(hostPath, extension, platform = process.platform) {
  if (platform === 'win32' && hostPath.toLowerCase().endsWith('.exe')) {
    return `${hostPath.slice(0, -4)}.${extension}`
  }
  return `${hostPath}.${extension}`
}

/**
 * A Host binary is runnable when it is framework-dependent (sidecars present)
 * or a Unix single-file self-contained publish (native executable, no sibling .dll).
 * Incomplete FDD layouts (apphost + .dll without runtimeconfig) are rejected.
 */
export function isRunnableHost(hostPath, exists = existsSync, platform = process.platform) {
  if (!exists(hostPath)) return false
  if (
    exists(hostSidecarPath(hostPath, 'runtimeconfig.json', platform)) &&
    exists(hostSidecarPath(hostPath, 'deps.json', platform))
  ) {
    return true
  }
  if (platform !== 'win32' && !exists(`${hostPath}.dll`)) {
    return true
  }
  return false
}

/** NetworkProxy worker lives beside Host.exe in Debug output and packaged extraResources/host. */
export function networkProxyPathBesideHost(hostPath, platform = process.platform) {
  const workerName =
    platform === 'win32' ? 'UniversalDeviceToolkit.NetworkProxy.exe' : 'UniversalDeviceToolkit.NetworkProxy'
  const slash = Math.max(hostPath.lastIndexOf('\\'), hostPath.lastIndexOf('/'))
  if (slash < 0) {
    return workerName
  }
  return `${hostPath.slice(0, slash + 1)}${workerName}`
}

/**
 * Dev Host is ready when the Host apphost can run. NetworkProxy is Windows-only
 * (network acceleration); Linux/macOS must not wait for it.
 */
export function isDevHostLayoutReady(hostPath, exists = existsSync, platform = process.platform) {
  if (!isRunnableHost(hostPath, exists, platform)) return false
  if (platform === 'win32') {
    return isRunnableHost(networkProxyPathBesideHost(hostPath, platform), exists, platform)
  }
  return true
}

/**
 * Candidate Host binaries for Electron / the dev bridge.
 * @param {object} options
 * @param {string} options.electronRoot Electron project root (UniversalDeviceToolkit.Electron)
 * @param {string} [options.platform]
 * @param {string} [options.arch]
 * @param {string | null} [options.packagedResourcesPath] process.resourcesPath when packaged
 */
export function listHostCandidates({
  electronRoot,
  platform = process.platform,
  arch = process.arch,
  packagedResourcesPath = null
} = {}) {
  if (!electronRoot) {
    throw new Error('listHostCandidates requires electronRoot')
  }

  const hostExeName = hostExecutableName(platform)
  const hostRoot = join(electronRoot, '..', 'Host')
  const candidates = []

  if (packagedResourcesPath) {
    candidates.push(join(packagedResourcesPath, 'host', hostExeName))
  }

  if (platform === 'win32') {
    candidates.push(
      join(hostRoot, 'bin', 'x64', 'Debug', WINDOWS_HOST_TFM, 'win-x64', hostExeName),
      join(hostRoot, 'bin', 'x64', 'Release', WINDOWS_HOST_TFM, 'win-x64', hostExeName),
      join(hostRoot, 'publish', 'win-x64', hostExeName)
    )
  } else {
    const ridPrefix = platform === 'darwin' ? 'osx' : 'linux'
    const rid = `${ridPrefix}-${arch}`
    candidates.push(
      join(hostRoot, 'bin', 'Debug', PORTABLE_HOST_TFM, hostExeName),
      join(hostRoot, 'bin', 'Release', PORTABLE_HOST_TFM, hostExeName),
      join(hostRoot, 'bin', 'Debug', PORTABLE_HOST_TFM, rid, hostExeName),
      join(hostRoot, 'bin', 'Release', PORTABLE_HOST_TFM, rid, hostExeName),
      join(hostRoot, 'publish', rid, hostExeName),
      join(hostRoot, 'publish', `${ridPrefix}-x64`, hostExeName),
      join(hostRoot, 'publish', `${ridPrefix}-arm64`, hostExeName)
    )
  }

  candidates.push(join(electronRoot, 'host', hostExeName))
  return candidates
}
