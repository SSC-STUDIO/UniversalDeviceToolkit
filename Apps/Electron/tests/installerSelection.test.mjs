import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { URL, fileURLToPath } from 'node:url'
import { createRequire } from 'node:module'
import test from 'node:test'
import ts from 'typescript'
import vm from 'node:vm'

const nodeRequire = createRequire(import.meta.url)

function compileModule(fileUrl) {
  const fileName = fileURLToPath(fileUrl)
  const result = ts.transpileModule(readFileSync(fileUrl, 'utf8'), {
    fileName,
    reportDiagnostics: true,
    compilerOptions: {
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

function loadModule(fileUrl, mocks = {}) {
  const module = { exports: {} }
  const sourcePath = fileURLToPath(fileUrl)
  vm.runInNewContext(compileModule(fileUrl), {
    exports: module.exports,
    module,
    require(specifier) {
      if (Object.prototype.hasOwnProperty.call(mocks, specifier)) return mocks[specifier]
      return nodeRequire(specifier)
    },
    console
  }, { filename: sourcePath })
  return module.exports
}

const sharedUrl = new URL('../src/shared/installer-selection.ts', import.meta.url)
const mainUrl = new URL('../src/main/installer-selection.ts', import.meta.url)
const shared = loadModule(sharedUrl)
const main = loadModule(mainUrl, { '../shared/installer-selection': shared })

test('installer selection arguments accept only supported language and device values', () => {
  assert.equal(JSON.stringify(
    shared.parseInstallerSelectionArguments([
      '--udt-installer-language=ja',
      '--udt-installer-device-mode=basic'
    ])),
    JSON.stringify({ language: 'ja', deviceMode: 'basic', features: shared.defaultInstallerFeatures() })
  )
  assert.equal(
    shared.parseInstallerSelectionArguments([
      '--udt-installer-language=../../evil',
      '--udt-installer-device-mode=basic'
    ]),
    null
  )
})

test('installer INI parser ignores unrelated sections and rejects incomplete input', () => {
  assert.equal(JSON.stringify(
    main.parseInstallerSelectionIni([
      '[other]',
      'language=ru',
      '[installation]',
      'language=ru',
      'deviceMode=auto'
    ].join('\n'))),
    JSON.stringify({ language: 'ru', deviceMode: 'auto', features: shared.defaultInstallerFeatures() })
  )
  assert.equal(main.parseInstallerSelectionIni('[installation]\nlanguage=en'), null)
})

test('installer feature flags default on and treat missing keys as a full install', () => {
  const omitted = shared.parseInstallerFeaturesArgument('automation,macro')
  assert.equal(omitted.automation, true)
  assert.equal(omitted.macro, true)
  assert.equal(omitted.windowsOptimization, false)
  assert.equal(omitted.networkAcceleration, false)
  assert.equal(omitted.keyboard, false)
  assert.equal(shared.isInstallerOptionalFeatureEnabled(null, 'windowsOptimization'), true)
  assert.equal(shared.isInstallerOptionalFeatureEnabled(omitted, 'windowsOptimization'), false)
  assert.equal(shared.isInstallerOptionalFeatureEnabled(omitted, 'dashboard'), true)
  assert.equal(shared.isNetworkProxySidecarFile('resources/host/UniversalDeviceToolkit.NetworkProxy.exe'), true)
  assert.equal(shared.isNetworkProxySidecarFile('resources/host/UniversalDeviceToolkit.Host.exe'), false)
})

test('installer INI parser honors optional feature checkboxes and caps network acceleration', () => {
  const parsed = main.parseInstallerSelectionIni([
    '[installation]',
    'language=en',
    'deviceMode=auto',
    'windowsOptimization=0',
    'networkAcceleration=1',
    'automation=1',
    'macro=0',
    'keyboard=1'
  ].join('\n'))
  assert.equal(parsed.features.windowsOptimization, false)
  assert.equal(parsed.features.networkAcceleration, false)
  assert.equal(parsed.features.automation, true)
  assert.equal(parsed.features.macro, false)
  assert.equal(JSON.stringify(main.buildInstallerHostArguments(parsed)), JSON.stringify([]))
  assert.ok(main.buildInstallerRendererArguments(parsed).some((argument) => argument.startsWith('--udt-installer-features=')))
})
