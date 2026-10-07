import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import test from 'node:test'
import vm from 'node:vm'
import ts from 'typescript'

const nodeRequire = createRequire(import.meta.url)

function loadModule(sourceUrl, imports = {}) {
  const module = { exports: {} }
  const compiled = ts.transpileModule(readFileSync(sourceUrl, 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
  }).outputText
  vm.runInNewContext(compiled, {
    module, exports: module.exports,
    require: (specifier) => imports[specifier] ?? nodeRequire(specifier),
    __dirname: '/udt-test/main',
    process: { platform: 'win32' },
    console: { error: () => undefined },
    setTimeout, clearTimeout
  })
  return module.exports
}

const installer = loadModule(new URL('../src/shared/installer-selection.ts', import.meta.url))
const navigation = loadModule(new URL('../src/shared/navigation-visibility.ts', import.meta.url), {
  './installer-selection': installer
})

test('navigation keeps normal, partial and unknown Host capabilities visible', () => {
  for (const capabilities of [null, undefined, {}, { keyboard: false }]) {
    assert.equal(navigation.isActionsNavigationVisible(undefined, {}, capabilities), true)
  }
  for (const payload of [null, {}, { capabilities: {} }, { capabilities: { automation: 'false' } }]) {
    const capabilities = navigation.readNavigationCapabilities(payload)
    assert.equal(navigation.isNavigationFeatureVisible('automation', undefined, {}, capabilities), true)
  }
})

test('merged Actions needs one child permitted by installation, navigation settings and Host', () => {
  const features = installer.defaultInstallerFeatures()
  assert.equal(navigation.isActionsNavigationVisible(features, { automation: false, macro: false }, {}), false)
  assert.equal(navigation.isActionsNavigationVisible(features, {}, { automation: false, macro: false }), false)
  assert.equal(navigation.isActionsNavigationVisible({ ...features, automation: false, macro: false }, {}, {}), false)
  assert.equal(navigation.isActionsNavigationVisible(features, { actions: false }, {}), false)
  assert.equal(navigation.isActionsNavigationVisible(features, { automation: false }, { macro: true }), true)
  assert.equal(navigation.isActionsNavigationVisible(features, { macro: false }, { automation: true }), true)
  assert.equal(navigation.isActionsNavigationVisible({ ...features, automation: false }, { macro: false }, {}), false)
  assert.equal(navigation.isActionsNavigationVisible(features, { automation: false }, { macro: false }), false)
})

test('individual navigation entries use explicit Host flags and the optimization alias', () => {
  assert.equal(navigation.isNavigationFeatureVisible('keyboard', undefined, {}, { keyboard: false }), false)
  assert.equal(navigation.isNavigationFeatureVisible('macro', undefined, { macro: false }, { macro: true }), false)
  assert.equal(navigation.isNavigationFeatureVisible('windowsOptimization', undefined, {}, { optimization: false }), false)
  const capabilities = navigation.readNavigationCapabilities({ capabilities: { optimization: false, unrelated: false } })
  assert.equal(capabilities.optimization, false)
  assert.equal(Object.hasOwn(capabilities, 'unrelated'), false)
})

async function trayCommands({ capabilities, visibility = {}, features, quickActions = [] } = {}) {
  const handlers = new Map()
  let resolveShown
  const shown = new Promise((resolve) => { resolveShown = resolve })
  const image = { isEmpty: () => true, setTemplateImage: () => undefined }
  const tray = loadModule(new URL('../src/main/tray.ts', import.meta.url), {
    electron: {
      app: { quit: () => undefined },
      Tray: class {
        on(name, handler) { handlers.set(name, handler) }
        setToolTip() {}
        destroy() {}
      },
      nativeImage: { createFromPath: () => image, createEmpty: () => image },
      nativeTheme: { shouldUseDarkColors: false, on() {}, removeListener() {} },
      screen: { getCursorScreenPoint: () => ({ x: 0, y: 0 }) }
    },
    '../shared/navigation-visibility': navigation,
    './installer-selection': { readInstallerSelection: () => features == null ? null : { features } },
    './tray-icons': { trayNavSvg: () => '', trayIconSvgForSymbol: () => '' },
    './tray-i18n': {
      localizePipelineName: (name) => name,
      setTrayLanguage: () => undefined,
      trayStrings: () => ({ dashboard: 'Dashboard', keyboard: 'Keyboard', automation: 'Automation', macro: 'Macro', windowsOptimization: 'Tools', open: 'Open', close: 'Close' })
    },
    './tray-popup': {
      destroyTrayPopup() {}, hideTrayPopup() {}, isTrayPopupVisible: () => false,
      showTrayPopup: async (nodes) => { resolveShown(nodes) }
    }
  })
  tray.initTray(() => null, {
    invokeHost: async (method) => {
      if (method === 'host.getCapabilities') return capabilities
      if (method === 'settings.get') return { value: { NavigationItemsVisibility: visibility } }
      if (method === 'automation.getState') return { pipelines: quickActions }
      if (method === 'features.isSupported') return { value: false }
      return {}
    }
  })
  handlers.get('right-click')({}, { x: 0, y: 0, width: 16, height: 16 })
  const nodes = await shown
  tray.destroyTray()
  return Array.from(nodes.filter((node) => node.type === 'item'), (node) => node.cmd)
}

test('Electron tray omits unavailable entries and automated actions while retaining Dashboard', async () => {
  const commands = await trayCommands({
    capabilities: { capabilities: { keyboard: false, automation: false, macro: false, optimization: false } },
    quickActions: [{ id: 'disabled-action', name: 'Action' }]
  })
  assert.deepEqual(commands, ['nav:/dashboard', 'open', 'close'])
})

test('Electron tray keeps entries when an older Host omits capabilities', async () => {
  const commands = await trayCommands()
  assert.deepEqual(commands, ['nav:/dashboard', 'nav:/keyboard', 'nav:/automation', 'nav:/macro', 'nav:/optimization', 'open', 'close'])
})

test('Electron tray combines installer omissions, saved visibility and Host capability independently', async () => {
  const features = { ...installer.defaultInstallerFeatures(), macro: false }
  const commands = await trayCommands({
    features,
    visibility: { keyboard: false },
    capabilities: { capabilities: { automation: false, optimization: true } }
  })
  assert.deepEqual(commands, ['nav:/dashboard', 'nav:/optimization', 'open', 'close'])
})

test('Electron tray quick actions respect the Automation installer and navigation selection', async () => {
  const quickActions = [{ id: 'manual-action', name: 'Action' }]
  const omitted = await trayCommands({
    features: { ...installer.defaultInstallerFeatures(), automation: false },
    quickActions
  })
  const hidden = await trayCommands({ visibility: { automation: false }, quickActions })
  const visible = await trayCommands({ quickActions })
  assert.equal(omitted.includes('run:manual-action'), false)
  assert.equal(hidden.includes('run:manual-action'), false)
  assert.equal(visible.includes('run:manual-action'), true)
})
