import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import test from 'node:test'
import vm from 'node:vm'
import ts from 'typescript'

const require = createRequire(import.meta.url)
const source = readFileSync(new URL('../src/main/update-downloader.ts', import.meta.url), 'utf8')

function policy(marker) {
  const module = { exports: {} }
  const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText
  vm.runInNewContext(compiled + '\nexports.policy = { assetPatternForPlatform, tryExtractExpectedHash, toReleaseInfo };', {
    module, exports: module.exports, console,
    process: { platform: 'win32', resourcesPath: 'C:/app/resources', env: {} },
    require(name) {
      if (name === 'electron') return { app: { getAppPath: () => 'C:/app' } }
      if (name === 'fs') return { ...require('node:fs'), existsSync: () => true, readFileSync: () => marker }
      return require(name)
    }
  })
  return module.exports.policy
}

test('compatibility updates select only the independent Electron installer', () => {
  const { assetPatternForPlatform } = policy('electron-compatibility')
  const pattern = assetPatternForPlatform()
  assert.equal(pattern.test('UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe'), true)
  assert.equal(pattern.test('UniversalDeviceToolkitWebView2Setup-6.1.4.exe'), false)
  assert.equal(pattern.test('UniversalDeviceToolkit_v6.1.4_Full_Setup.exe'), false)
  assert.equal(pattern.test('../UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe'), false)
})

test('legacy Full and Online clients retain their migration asset names', () => {
  assert.equal(policy('full').assetPatternForPlatform().test('UniversalDeviceToolkit_v6.1.4_Full_Setup.exe'), true)
  assert.equal(policy('online').assetPatternForPlatform().test('UniversalDeviceToolkit_v6.1.4_Online_Setup.exe'), true)
})

test('compatibility checksum cannot be borrowed from the primary installer', () => {
  const { tryExtractExpectedHash } = policy('electron-compatibility')
  const hash = 'a'.repeat(64)
  const name = 'UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe'
  assert.equal(tryExtractExpectedHash(`${hash}  UniversalDeviceToolkitWebView2Setup-6.1.4.exe`, name), null)
  assert.equal(tryExtractExpectedHash(`${hash}  ${name}`, name), hash)
})

test('mixed releases prefer the current installer checksum regardless of asset order', () => {
  const { toReleaseInfo } = policy('electron-compatibility')
  const name = 'UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe'
  const installer = { name, browser_download_url: `https://example.com/${name}` }
  const manifest = { name: 'UniversalDeviceToolkit_v6.1.4_SHA256.txt', browser_download_url: 'https://example.com/all.txt' }
  const primaryHash = { name: 'UniversalDeviceToolkitWebView2Setup-6.1.4.exe.sha256', browser_download_url: 'https://example.com/primary.sha256' }
  const compatibleHash = { name: `${name.toUpperCase()}.SHA256`, browser_download_url: 'https://example.com/compatibility.sha256' }
  const release = { tag_name: 'v6.1.4', assets: [primaryHash, manifest, compatibleHash, installer] }
  assert.equal(toReleaseInfo(release, installer, name).sha256Url, compatibleHash.browser_download_url)
})

test('installer checksum selection falls back to the shared manifest', () => {
  const { toReleaseInfo } = policy('electron-compatibility')
  const name = 'UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe'
  const installer = { name, browser_download_url: `https://example.com/${name}` }
  const manifest = { name: 'UniversalDeviceToolkit_v6.1.4_SHA256.txt', browser_download_url: 'https://example.com/all.txt' }
  const assets = [
    { name: 'UniversalDeviceToolkitWebView2Setup-6.1.4.exe.sha256', browser_download_url: 'https://example.com/primary.sha256' },
    { name: `${name}.sha256` }, manifest, installer
  ]
  assert.equal(toReleaseInfo({ assets }, installer, name).sha256Url, manifest.browser_download_url)
})

test('another installer checksum is not treated as a usable manifest', () => {
  const { toReleaseInfo } = policy('electron-compatibility')
  const name = 'UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe'
  const installer = { name, browser_download_url: `https://example.com/${name}` }
  const assets = [
    { name: 'UniversalDeviceToolkitWebView2Setup-6.1.4.exe.sha256', browser_download_url: 'https://example.com/primary.sha256' },
    installer
  ]
  assert.equal(toReleaseInfo({ assets }, installer, name).sha256Url, null)
})

test('legacy update clients also select the checksum for their chosen alias', () => {
  const { toReleaseInfo } = policy('full')
  const name = 'UniversalDeviceToolkit_v6.1.4_Full_Setup.exe'
  const installer = { name, browser_download_url: `https://example.com/${name}` }
  const assets = [
    { name: 'UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe.sha256', browser_download_url: 'https://example.com/compatibility.sha256' },
    { name: `${name}.sha256`, browser_download_url: 'https://example.com/full.sha256' },
    installer
  ]
  assert.equal(toReleaseInfo({ assets }, installer, name).sha256Url, 'https://example.com/full.sha256')
})
