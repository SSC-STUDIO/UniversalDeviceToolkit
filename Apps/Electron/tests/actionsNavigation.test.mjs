import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'

function loadModule(sourceUrl, imports = {}, globals = {}) {
  const module = { exports: {} }
  const source = ts.transpileModule(readFileSync(sourceUrl, 'utf8'), {
    compilerOptions: {
      esModuleInterop: true,
      jsx: ts.JsxEmit.ReactJSX,
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText
  vm.runInNewContext(source, {
    module, exports: module.exports, ...globals,
    require(specifier) {
      if (Object.hasOwn(imports, specifier)) return imports[specifier]
      throw new Error(`Unexpected import ${specifier}`)
    }
  })
  return module.exports
}

const installer = loadModule(new URL('../src/shared/installer-selection.ts', import.meta.url))
const navigation = loadModule(new URL('../src/shared/navigation-visibility.ts', import.meta.url), {
  './installer-selection': installer
})

function createPageFixture({ capabilities = {}, visibility = {}, features, view = 'automation' } = {}) {
  const state = { scopes: { application: { NavigationItemsVisibility: visibility } } }
  const jsx = (type, props) => ({ type, props })
  let lazyCount = 0
  const page = loadModule(new URL('../src/renderer/src/features/actions/ActionsPage.tsx', import.meta.url), {
    'react/jsx-runtime': { jsx, jsxs: jsx },
    react: {
      lazy: () => ++lazyCount === 1 ? 'AutomationPage' : 'MacroPage',
      Suspense: 'Suspense',
      useMemo: (factory) => factory()
    },
    'react-router-dom': { useSearchParams: () => [new URLSearchParams({ view }), () => undefined] },
    'react-i18next': { useTranslation: () => ({ t: (key) => key }) },
    '../../shared/ui/icons/fluent': {},
    '../../../../shared/navigation-visibility': navigation,
    '../../shared/settings/settingsStore': { useSettingsStore: (select) => select(state) },
    '../../shared/state/hostCapabilitiesStore': {
      useHostCapabilitiesStore: (select) => select({ capabilities: { capabilities } })
    },
    '../../shared/ui/dialogs/CapabilityUnavailable': { __esModule: true, default: 'CapabilityUnavailable' },
    '../../shared/styles/pages.css': {}
  }, { window: { bridge: { installerSelection: { features } } } })
  return {
    render: () => page.default(),
    setVisibility: (next) => { state.scopes.application = { NavigationItemsVisibility: next } }
  }
}

function readPage(view) {
  if (view.type === 'CapabilityUnavailable') return { tabs: [], page: null }
  const [header, body] = view.props.children
  const tabs = header.props.children.props.children.filter(Boolean)
  return {
    tabs: Array.from(tabs, (tab) => tab.props.children),
    page: body.props.children.type
  }
}

test('Actions hides an opted-out Automation tab and opens Macro for a stale Automation link', () => {
  const fixture = createPageFixture({ visibility: { automation: false }, view: 'automation' })
  assert.deepEqual(readPage(fixture.render()), { tabs: ['nav.macro'], page: 'MacroPage' })
})

test('Actions hides an opted-out Macro tab and opens Automation for a stale Macro link', () => {
  const fixture = createPageFixture({ visibility: { macro: false }, view: 'macro' })
  assert.deepEqual(readPage(fixture.render()), { tabs: ['nav.automation'], page: 'AutomationPage' })
})

test('Actions combines installer, settings and Host restrictions on different children', () => {
  for (const options of [
    { visibility: { automation: false }, capabilities: { macro: false } },
    { features: { ...installer.defaultInstallerFeatures(), macro: false }, visibility: { automation: false } },
    { visibility: { automation: false, macro: false } },
    { capabilities: { automation: false, macro: false } }
  ]) {
    const fixture = createPageFixture(options)
    assert.equal(fixture.render().type, 'CapabilityUnavailable')
  }
})

test('Actions follows live application settings changes and retains older Hosts with missing flags', () => {
  const fixture = createPageFixture()
  assert.deepEqual(readPage(fixture.render()), { tabs: ['nav.automation', 'nav.macro'], page: 'AutomationPage' })
  fixture.setVisibility({ automation: false })
  assert.deepEqual(readPage(fixture.render()), { tabs: ['nav.macro'], page: 'MacroPage' })
  fixture.setVisibility({ automation: false, macro: false })
  assert.equal(fixture.render().type, 'CapabilityUnavailable')
  fixture.setVisibility({})
  assert.deepEqual(readPage(fixture.render()), { tabs: ['nav.automation', 'nav.macro'], page: 'AutomationPage' })
})
