import { spawnSync } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  hostExecutableName,
  isDevHostLayoutReady,
  isRunnableHost,
  networkProxyPathBesideHost,
  PORTABLE_HOST_TFM,
  WINDOWS_HOST_TFM
} from './host-sidecar.mjs'

const electronRoot = dirname(dirname(fileURLToPath(import.meta.url)))
const repoRoot = dirname(dirname(electronRoot))
const hostProject = join(repoRoot, 'Apps/Host', 'UniversalDeviceToolkit.Host.csproj')
const isWindows = process.platform === 'win32'
const hostExeName = hostExecutableName()
const debugHost = isWindows
  ? join(
      repoRoot,
      'Apps/Host',
      'bin',
      'x64',
      'Debug',
      WINDOWS_HOST_TFM,
      'win-x64',
      hostExeName
    )
  : join(repoRoot, 'Apps/Host', 'bin', 'Debug', PORTABLE_HOST_TFM, hostExeName)

if (isDevHostLayoutReady(debugHost)) {
  process.exit(0)
}

console.log(
  isWindows
    ? '[ensure-host] Debug Host layout is incomplete (Host and/or NetworkProxy missing runtimeconfig.json/deps.json). Building UniversalDeviceToolkit.Host...'
    : '[ensure-host] Debug Host is missing. Building UniversalDeviceToolkit.Host (portable net10.0)...'
)

const args = ['build', hostProject, '-c', 'Debug', '--nologo']
if (!isWindows) {
  args.push('-p:UDTWindows=false')
}

const result = spawnSync('dotnet', args, {
  stdio: 'inherit',
  cwd: repoRoot
})

if ((result.status ?? 1) !== 0) {
  process.exit(result.status ?? 1)
}

if (!isRunnableHost(debugHost)) {
  console.error(
    `[ensure-host] Host build finished but ${debugHost} is still not runnable (need the apphost plus runtimeconfig.json/deps.json, or a Unix single-file publish).`
  )
  process.exit(1)
}

if (isWindows) {
  const debugWorker = networkProxyPathBesideHost(debugHost)
  if (!isRunnableHost(debugWorker)) {
    console.error(
      `[ensure-host] Host build finished but NetworkProxy worker is missing beside Host (need ${debugWorker} plus runtimeconfig.json/deps.json).`
    )
    process.exit(1)
  }
}
