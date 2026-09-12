import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'
import test from 'node:test'

import { parseSha256Digest, parseSha256Manifest } from '../installer/integrity.mjs'

const mainSource = readFileSync(
  fileURLToPath(new URL('../installer/main.mjs', import.meta.url)),
  'utf8'
)

const HASH_A = 'a'.repeat(64)
const HASH_B = 'b'.repeat(64)
const ZIP_NAME = 'UniversalDeviceToolkit_v1.2.3_Online_win-x64.zip'

test('parseSha256Digest accepts GitHub asset digests and bare hashes only', () => {
  assert.equal(parseSha256Digest(`sha256:${HASH_A}`), HASH_A)
  assert.equal(parseSha256Digest(HASH_A.toUpperCase()), HASH_A)
  assert.equal(parseSha256Digest(` sha256:${HASH_A} `), HASH_A)
  assert.equal(parseSha256Digest(`md5:${'c'.repeat(32)}`), null)
  assert.equal(parseSha256Digest(HASH_A.slice(0, 63)), null)
  assert.equal(parseSha256Digest(''), null)
  assert.equal(parseSha256Digest(null), null)
})

test('parseSha256Manifest resolves the hash recorded for the named asset', () => {
  const manifest = [
    `${HASH_B}  UniversalDeviceToolkit_v1.2.3_Full_Setup.exe`,
    `${HASH_A.toUpperCase()}  ${ZIP_NAME}`,
    ''
  ].join('\r\n')
  assert.equal(parseSha256Manifest(manifest, ZIP_NAME), HASH_A)
  assert.equal(parseSha256Manifest(manifest, ZIP_NAME.toUpperCase()), HASH_A)
  assert.equal(parseSha256Manifest(manifest, 'other.zip'), null)
})

test('parseSha256Manifest tolerates sha256sum binary markers and rejects noise', () => {
  assert.equal(parseSha256Manifest(`${HASH_A}  *${ZIP_NAME}`, ZIP_NAME), HASH_A)
  assert.equal(parseSha256Manifest(`${HASH_A.slice(0, 63)}  ${ZIP_NAME}`, ZIP_NAME), null)
  assert.equal(parseSha256Manifest(`see release notes for ${ZIP_NAME}`, ZIP_NAME), null)
  assert.equal(parseSha256Manifest('', ZIP_NAME), null)
  assert.equal(parseSha256Manifest(null, ZIP_NAME), null)
  assert.equal(parseSha256Manifest(`${HASH_A}  ${ZIP_NAME}`, ''), null)
})

test('online payload download verifies SHA256 before extraction and fails closed', () => {
  assert.match(mainSource, /resolveExpectedPayloadHash/)
  assert.match(mainSource, /parseSha256Digest/)
  assert.match(mainSource, /parseSha256Manifest/)
  assert.match(mainSource, /createHash\('sha256'\)/)
  // No expected hash -> abort before any archive byte is trusted.
  assert.match(mainSource, /if \(!expectedHash\) \{\s*\n\s*throw new Error/)
  // Mismatch -> the downloaded file is discarded, never extracted.
  assert.match(mainSource, /actualHash !== expectedHash/)
})

test('online payload download applies backpressure and a stall watchdog', () => {
  assert.match(mainSource, /pipeline\(Readable\.fromWeb\(response\.body\), progress, nodeFs\.createWriteStream\(destinationFile\)\)/)
  assert.match(mainSource, /PAYLOAD_STALL_TIMEOUT_MS/)
  assert.doesNotMatch(mainSource, /fileStream\.write/)
})
