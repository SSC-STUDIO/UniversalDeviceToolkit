/**
 * Payload integrity helpers for the online installer. Kept dependency-free so
 * unit tests can import them directly (same pattern as features.mjs).
 */

const SHA256_HEX = /^[a-f0-9]{64}$/i

/** GitHub REST release asset `digest` values look like `sha256:<64 hex>`. */
export function parseSha256Digest(digest) {
  if (typeof digest !== 'string') return null
  const trimmed = digest.trim()
  const prefixed = /^sha256:([a-f0-9]{64})$/i.exec(trimmed)
  if (prefixed) return prefixed[1].toLowerCase()
  return SHA256_HEX.test(trimmed) ? trimmed.toLowerCase() : null
}

/**
 * Release `_SHA256.txt` manifests are `sha256sum`-style lines written by
 * Scripts/Build-LanguageAssets.ps1: `<64 hex>  <asset name>`. Returns the hash
 * recorded for `assetName`, or null when the manifest does not list it.
 */
export function parseSha256Manifest(content, assetName) {
  if (typeof content !== 'string' || typeof assetName !== 'string' || assetName.length === 0) return null
  const wanted = assetName.toLowerCase()
  for (const rawLine of content.split(/\r\n|\n|\r/)) {
    const match = /^([a-f0-9]{64})[ \t]+\*?(.+)$/i.exec(rawLine.trim())
    if (match && match[2].trim().toLowerCase() === wanted) return match[1].toLowerCase()
  }
  return null
}
