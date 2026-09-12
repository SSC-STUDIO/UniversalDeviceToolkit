import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { formatUsageInGigabytes } from '../src/renderer/src/utils/format.ts'

const MIB_PER_GIB = 1024

test('memory usage formats Host MiB values instead of rounding GB-as-MiB to 0.0', () => {
  const usedMb = 15.36 * MIB_PER_GIB
  const totalMb = 16 * MIB_PER_GIB
  assert.equal(formatUsageInGigabytes(usedMb, totalMb, 96), '15.4 / 16.0 GB (96%)')
})

test('VRAM usage formats matching used and total instead of 0.0 GB with a stale percent', () => {
  const usedMb = 2.12 * MIB_PER_GIB
  const totalMb = 5.9 * MIB_PER_GIB
  assert.equal(formatUsageInGigabytes(usedMb, totalMb, 36), '2.1 / 5.9 GB (36%)')
})

test('percent is used/total and is omitted when total is 0', () => {
  assert.equal(formatUsageInGigabytes(0, 0, 96), '-')
  assert.equal(formatUsageInGigabytes(512, 0, 96), '0.5 GB')
  assert.equal(formatUsageInGigabytes(null, 5.9 * MIB_PER_GIB, 36), '2.1 / 5.9 GB (36%)')
  assert.equal(formatUsageInGigabytes(null, null, 36), '36%')
  assert.equal(formatUsageInGigabytes(null, null, -1), '-')
})

test('Host snapshot converts LHM gigabyte readings to *Mb for used and total', () => {
  const handler = readFileSync(
    new URL('../../Host/Rpc/Handlers/SensorsHandlers.cs', import.meta.url),
    'utf8'
  )
  assert.match(handler, /vramUsedMb = GigabytesToMegabytes\(gpuVramUsedTask\.Result\)/)
  assert.match(handler, /vramTotalMb = GigabytesToMegabytes\(gpuVramTotalTask\.Result\)/)
  assert.match(handler, /usedMb = GigabytesToMegabytes\(memUsedTask\.Result\)/)
  assert.match(handler, /totalMb = GigabytesToMegabytes\(memTotalTask\.Result\)/)
  assert.match(handler, /internal static float\? GigabytesToMegabytes\(float gigabytes\)/)
  assert.match(handler, /gigabytes \* 1024f/)
})
