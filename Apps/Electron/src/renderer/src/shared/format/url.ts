/**
 * Safe external URL handling — port of Electron Extensions/UriExtensions.cs.
 * Only HTTP/HTTPS schemes are allowed to prevent command injection.
 */

const DANGEROUS_SCHEME_CHARS = [':', '/', '\\', '*', '?', '"', '<', '>', '|']

const BLOCKED_SCHEME_PREFIXES = [
  'ms-',
  'file',
  'javascript',
  'vbscript',
  'data',
  'about',
  'shell'
]

/**
 * SECURITY: Only allow HTTP and HTTPS schemes, mirroring the Electron allow-list.
 * Rejects file://, ms-* (ms-settings, ms-windows-store) and any custom scheme.
 */
export function isSafeExternalUrl(url: string): boolean {
  let scheme = ''
  try {
    const parsed = new URL(url)
    scheme = parsed.protocol.replace(/:$/, '').toLowerCase()
  } catch {
    return false
  }

  if (!scheme) return false
  if (scheme.split('').some((ch) => DANGEROUS_SCHEME_CHARS.includes(ch))) return false
  if (BLOCKED_SCHEME_PREFIXES.some((prefix) => scheme.startsWith(prefix))) return false
  if (scheme !== 'http' && scheme !== 'https') return false
  return true
}

/**
 * Opens an external URL through the OS default browser (main-process
 * 'shell:open-external' http/https whitelist). Only http/https URLs are ever
 * opened; anything else is ignored (returns false).
 */
export async function openExternalUrl(url: string): Promise<boolean> {
  if (!isSafeExternalUrl(url)) return false
  try {
    const result = await window.bridge?.openExternal(url)
    return result?.opened === true
  } catch {
    return false
  }
}
