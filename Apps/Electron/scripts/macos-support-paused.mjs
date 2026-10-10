import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

export const MACOS_SUPPORT_PAUSED_REASON =
  'macOS support is temporarily paused. Source code is retained for future restoration; use Windows or Linux.'

/** Reject paused targets, including cross-compilation from another OS. */
export function assertMacosPackagingAllowed(platform) {
  if (typeof platform !== 'string' || platform.length === 0) {
    throw new TypeError('Packaging requires an explicit platform name.')
  }
  const target = platform.toLowerCase()
  if (target === 'darwin' || target === 'mac' || target === 'macos' || target === 'osx' || target.startsWith('osx-')) {
    throw new Error(MACOS_SUPPORT_PAUSED_REASON)
  }
}

/** electron-builder calls this before assembling any platform payload. */
export function beforePack(context) {
  if (context == null || typeof context !== 'object' || Array.isArray(context)) {
    throw new TypeError('Packaging requires a valid beforePack context.')
  }
  assertMacosPackagingAllowed(context.electronPlatformName)
}

export default beforePack

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    assertMacosPackagingAllowed(process.argv[2] ?? process.platform)
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exitCode = 1
  }
}
