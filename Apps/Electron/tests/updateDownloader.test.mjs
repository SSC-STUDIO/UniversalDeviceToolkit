import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'
import test from 'node:test'

const downloaderSource = readFileSync(
  fileURLToPath(new URL('../src/main/update-downloader.ts', import.meta.url)),
  'utf8'
)
const indexSource = readFileSync(
  fileURLToPath(new URL('../src/main/index.ts', import.meta.url)),
  'utf8'
)
const bannersSource = readFileSync(
  fileURLToPath(new URL('../src/renderer/src/components/AppStatusBanners.tsx', import.meta.url)),
  'utf8'
)

test('Windows in-app update keeps the 6.0.0 spawn /S contract and ShellExecute fallback', () => {
  assert.match(downloaderSource, /export function windowsInstallerLaunchArgs/)
  assert.match(downloaderSource, /return \['\/S'\]/)
  assert.match(downloaderSource, /Zone\.Identifier/)
  assert.match(downloaderSource, /Start-Process -FilePath/)
  assert.match(downloaderSource, /-Verb RunAs/)
  assert.match(downloaderSource, /assetPatternForPlatform/)
  assert.match(downloaderSource, /UniversalDeviceToolkit_v\.\+_Full_Setup\\.exe/)
  assert.match(downloaderSource, /UniversalDeviceToolkit_v\.\+_Online_Setup\\.exe/)
})

test('verified installer path comparison is case-insensitive on Windows', () => {
  assert.match(indexSource, /toLowerCase\(\)/)
  assert.match(indexSource, /Installer path is not the verified download/)
})

test('startup update banner exposes Update and dismiss actions', () => {
  assert.match(bannersSource, /openUpdateModal/)
  assert.match(bannersSource, /actionLabel: t\('wpf\.update'\)/)
  assert.match(bannersSource, /closable: true/)
  assert.doesNotMatch(bannersSource, /closable: false/)
  assert.doesNotMatch(bannersSource, /navigate\('\/settings'\)/)
})
