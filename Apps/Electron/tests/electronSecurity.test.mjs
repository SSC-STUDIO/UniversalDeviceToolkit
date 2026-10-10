import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const main = readFileSync(
  new URL('../src/main/index.ts', import.meta.url),
  'utf8'
)
const safeOpenPath = readFileSync(
  new URL('../src/main/safe-open-path.ts', import.meta.url),
  'utf8'
)
const systemPower = readFileSync(
  new URL('../src/main/system-power.ts', import.meta.url),
  'utf8'
)

test('main renderer is sandboxed and blocks unexpected navigation', () => {
  assert.match(main, /sandbox: true/)
  assert.match(main, /setWindowOpenHandler\(\(\) => \(\{ action: 'deny' \}\)\)/)
  assert.match(main, /webContents\.on\('will-navigate'/)
})

test('privileged bridge requests require the current main frame', () => {
  assert.match(main, /function assertMainFrame\(/)
  assert.match(main, /event\.senderFrame !== win\.webContents\.mainFrame/)
  assert.match(main, /ipcMain\.handle\('bridge:invoke'[\s\S]*?assertMainFrame\(event\)/)
})

test('renderer path opening rejects executable and script files', () => {
  for (const extension of ['.exe', '.cmd', '.ps1', '.vbs', '.lnk']) {
    assert.match(safeOpenPath, new RegExp(`'\\${extension}'`))
  }
  assert.match(safeOpenPath, /stats\.isDirectory\(\)/)
  assert.match(safeOpenPath, /BLOCKED_FILE_EXTENSIONS\.has/)
})

test('power actions are rate limited and child completion settles once', () => {
  assert.match(main, /function claimPowerAction\(/)
  assert.match(main, /now - lastPowerActionAt < 2000/)
  assert.match(systemPower, /function singleSettlement\(/)
  assert.match(systemPower, /if \(settled\) return/)
})
