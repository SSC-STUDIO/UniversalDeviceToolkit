import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { URL, fileURLToPath } from 'node:url'
import { setImmediate } from 'node:timers'
import test from 'node:test'
import ts from 'typescript'
import vm from 'node:vm'

const utilsDir = new URL('../src/renderer/src/components/utils/', import.meta.url)
const gateUrl = new URL('../src/renderer/src/components/utils/UnsupportedDeviceGate.tsx', import.meta.url)
const layoutUrl = new URL('../src/renderer/src/layout/AppLayout.tsx', import.meta.url)
const dialogHookUrl = new URL('../src/renderer/src/components/utils/useUtilsDialog.ts', import.meta.url)
const utilsCssUrl = new URL('../src/renderer/src/components/utils/utils.css', import.meta.url)
const loadingCssUrl = new URL('../src/renderer/src/components/custom.css', import.meta.url)
const notificationCssUrl = new URL('../src/renderer/src/notifications/notifications.css', import.meta.url)
const i18nUrl = new URL('../src/renderer/src/i18n/index.ts', import.meta.url)

const REMOVED_GATE_FILES = [
  'StartupGates.tsx',
  'LanguageSelectorModal.tsx',
  'DeviceSetupModal.tsx'
]

const DIALOG_HOSTS = [
  'ActionDetailsModal.tsx',
  'CrashReportNotificationModal.tsx',
  'StatusModal.tsx',
  'SymbolPickerModal.tsx',
  'UnsupportedDeviceModal.tsx'
]

function compileModule(fileUrl) {
  const fileName = fileURLToPath(fileUrl)
  const result = ts.transpileModule(readFileSync(fileUrl, 'utf8'), {
    fileName,
    reportDiagnostics: true,
    compilerOptions: {
      jsx: ts.JsxEmit.ReactJSX,
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  })
  const errors = (result.diagnostics ?? []).filter(
    (diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error
  )
  if (errors.length > 0) {
    throw new Error(errors.map((diagnostic) => ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n')).join('\n'))
  }
  return result.outputText
}

function firstBlockZIndex(source, selector) {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const block = source.match(new RegExp(`${escaped}\\s*\\{[^}]+\\}`))
  assert.ok(block, `${selector} must declare a CSS block`)
  const zIndex = block[0].match(/z-index:\s*(\d+)/)
  assert.ok(zIndex, `${selector} must declare z-index`)
  return Number(zIndex[1])
}

function createGateHarness(info, continueOnWarning = true) {
  const effects = []
  const opened = []
  let quitCount = 0
  const module = { exports: {} }
  vm.runInNewContext(compileModule(gateUrl), {
    exports: module.exports,
    module,
    require(specifier) {
      if (specifier === 'react') return { useEffect: (effect) => effects.push(effect) }
      if (specifier === 'react/jsx-runtime') {
        return { Fragment: Symbol('Fragment'), jsx: () => ({}), jsxs: () => ({}) }
      }
      if (specifier === '../../api/bridge') return { waitForHostReady: async () => undefined }
      if (specifier === '../../api/system') return { systemApi: { info: async () => info } }
      if (specifier === './UnsupportedDeviceModal') {
        return {
          openUnsupportedDevice: async (options) => {
            opened.push(options)
            return continueOnWarning
          }
        }
      }
      throw new Error(`Unexpected import "${specifier}"`)
    },
    console,
    Promise,
    setTimeout,
    window: { setTimeout, bridge: { quitApp: () => { quitCount += 1 } } }
  }, { filename: fileURLToPath(gateUrl) })

  return {
    opened,
    get quitCount() { return quitCount },
    run: () => {
      module.exports.default()
      return effects[0]()
    }
  }
}

test('first-run language and device setup gates stay completed', () => {
  const layout = readFileSync(layoutUrl, 'utf8')
  const gate = readFileSync(gateUrl, 'utf8')
  const i18n = readFileSync(i18nUrl, 'utf8')

  for (const file of REMOVED_GATE_FILES) {
    assert.equal(existsSync(new URL(file, utilsDir)), false, `${file} must stay removed`)
  }

  assert.match(layout, /UnsupportedDeviceGate/)
  assert.doesNotMatch(layout, /StartupGates|LanguageSelector|DeviceSetup/)
  assert.doesNotMatch(gate, /LanguageSelector|DeviceSetup|localStorage|udt\.lang|udt\.deviceSetup/)
  assert.match(gate, /openUnsupportedDevice/)
  assert.match(i18n, /import\.meta\.glob<LocaleBundle>/)
  assert.match(i18n, /localeModulePath\(lng\)/)
  assert.doesNotMatch(i18n, /import\(`\.\/locales\/\$\{lng\}`\)/)
})

test('unsupported devices are checked after Host readiness and can exit the app', async () => {
  const harness = createGateHarness({ vendor: 'Acme', model: 'Test', machineType: 'ABC', isCompatible: false }, false)
  const cleanup = harness.run()
  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(harness.opened.length, 1)
  assert.equal(harness.opened[0].machineType, 'ABC')
  assert.equal(harness.quitCount, 1)
  cleanup()
})

test('compatible devices do not open the warning', async () => {
  const harness = createGateHarness({ vendor: 'Acme', model: 'Test', machineType: 'ABC', isCompatible: true })
  const cleanup = harness.run()
  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(harness.opened.length, 0)
  assert.equal(harness.quitCount, 0)
  cleanup()
})

test('utils dialogs keep a11y dialog semantics, focus trap, and Escape', () => {
  const hook = readFileSync(dialogHookUrl, 'utf8')
  assert.match(hook, /role: 'dialog'/)
  assert.match(hook, /'aria-modal': true/)
  assert.match(hook, /aria-labelledby/)
  assert.match(hook, /event\.key === 'Escape'/)
  assert.match(hook, /event\.key !== 'Tab'/)
  assert.match(hook, /getFocusableElements/)

  for (const file of DIALOG_HOSTS) {
    const source = readFileSync(new URL(file, utilsDir), 'utf8')
    assert.match(source, /useUtilsDialog/, `${file} must use the shared dialog chrome`)
  }

  const unsupported = readFileSync(new URL('UnsupportedDeviceModal.tsx', utilsDir), 'utf8')
  assert.match(unsupported, /useUtilsDialog\(request != null, null\)/)

  const update = readFileSync(new URL('UpdateModal.tsx', utilsDir), 'utf8')
  assert.doesNotMatch(update, /useUtilsDialog/)
})

test('utils backdrops sit above loading and notification overlays', () => {
  const utilsZ = firstBlockZIndex(readFileSync(utilsCssUrl, 'utf8'), '.udt-utils-backdrop')
  const loadingZ = firstBlockZIndex(readFileSync(loadingCssUrl, 'utf8'), '.udt-loading-overlay')
  const notificationZ = firstBlockZIndex(readFileSync(notificationCssUrl, 'utf8'), '.udt-notification-center')
  const dropdownZ = firstBlockZIndex(readFileSync(utilsCssUrl, 'utf8'), '.udt-device-setup-select-dropdown.ant-select-dropdown')

  assert.ok(utilsZ > loadingZ, `utils backdrop ${utilsZ} must beat loading overlay ${loadingZ}`)
  assert.ok(utilsZ > notificationZ, `utils backdrop ${utilsZ} must beat notification stack ${notificationZ}`)
  assert.ok(dropdownZ > utilsZ, `select dropdown ${dropdownZ} must beat utils backdrop ${utilsZ}`)
})

test('utils directory no longer ships first-run language or device setup hosts', () => {
  const files = readdirSync(utilsDir)
  assert.ok(files.includes('UnsupportedDeviceGate.tsx'))
  assert.ok(files.includes('UnsupportedDeviceModal.tsx'))
  assert.ok(files.includes('useUtilsDialog.ts'))
  assert.equal(files.includes('StartupGates.tsx'), false)
  assert.equal(files.includes('LanguageSelectorModal.tsx'), false)
  assert.equal(files.includes('DeviceSetupModal.tsx'), false)
})
