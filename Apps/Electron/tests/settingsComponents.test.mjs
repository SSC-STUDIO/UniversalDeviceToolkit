import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { setImmediate } from 'node:timers'
import { URL, fileURLToPath } from 'node:url'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'

const appearanceSectionUrl = new URL(
  '../src/renderer/src/features/settings/components/AppearanceSection.tsx',
  import.meta.url
)
const settingsPageUrl = new URL('../src/renderer/src/features/settings/SettingsPage.tsx', import.meta.url)
const settingsCssUrl = new URL(
  '../src/renderer/src/features/settings/components/settings.css',
  import.meta.url
)
const cachedRouteUrl = new URL('../src/renderer/src/app/CachedRoute.tsx', import.meta.url)
const settingsLoadErrorUrl = new URL(
  '../src/renderer/src/features/settings/components/SettingsLoadError.tsx',
  import.meta.url
)
const settingsCardUrl = new URL(
  '../src/renderer/src/features/settings/components/SettingsCard.tsx',
  import.meta.url
)
const settingsStoreUrl = new URL(
  '../src/renderer/src/shared/settings/settingsStore.ts',
  import.meta.url
)
const themeStoreUrl = new URL(
  '../src/renderer/src/shared/theme/themeStore.ts',
  import.meta.url
)
const uiScaleUrl = new URL('../src/renderer/src/shared/theme/uiScale.ts', import.meta.url)
const osdApiUrl = new URL('../src/renderer/src/features/settings/api/osd.ts', import.meta.url)
const osdSectionUrl = new URL('../src/renderer/src/features/settings/components/OsdSection.tsx', import.meta.url)
const osdStoreUrl = new URL('../src/renderer/src/features/settings/stores/osdSettingsStore.ts', import.meta.url)

const Fragment = Symbol('Fragment')

function createElement(type, props, key) {
  return {
    type,
    key: key ?? null,
    props: props ?? {}
  }
}

const jsxRuntime = {
  Fragment,
  jsx: createElement,
  jsxs: createElement
}

function compileModule(fileUrl) {
  const fileName = fileURLToPath(fileUrl)
  const result = ts.transpileModule(readFileSync(fileUrl, 'utf8'), {
    fileName,
    reportDiagnostics: true,
    compilerOptions: {
      esModuleInterop: true,
      jsx: ts.JsxEmit.ReactJSX,
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  })
  const errors = (result.diagnostics ?? []).filter(
    (diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error
  )
  if (errors.length > 0) {
    throw new Error(
      errors
        .map((diagnostic) => ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n'))
        .join('\n')
    )
  }
  return result.outputText
}

function loadModule(fileUrl, mocks, globals = {}) {
  const fileName = fileURLToPath(fileUrl)
  const module = { exports: {} }
  const context = {
    ...globals,
    exports: module.exports,
    module,
    require(specifier) {
      if (Object.prototype.hasOwnProperty.call(mocks, specifier)) {
        return mocks[specifier]
      }
      throw new Error(`Unexpected import "${specifier}" from ${fileName}`)
    }
  }

  new vm.Script(compileModule(fileUrl), { filename: fileName }).runInNewContext(context)
  return module.exports
}

function createZustandMock() {
  function buildStore(initializer) {
    let state
    const listeners = new Set()
    const getState = () => state
    const setState = (update, replace = false) => {
      const next = typeof update === 'function' ? update(state) : update
      const previous = state
      state = replace ? next : { ...state, ...next }
      for (const listener of listeners) {
        listener(state, previous)
      }
    }
    const subscribe = (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    }
    const api = { getState, setState, subscribe }
    state = initializer(setState, getState, api)

    const useStore = (selector = (current) => current) => selector(state)
    useStore.getState = getState
    useStore.setState = setState
    useStore.subscribe = subscribe
    return useStore
  }

  return {
    create: (initializer) => (initializer == null ? buildStore : buildStore(initializer))
  }
}

function createMemoryStorage(initial = {}) {
  const values = new Map(Object.entries(initial).map(([key, value]) => [key, String(value)]))
  return {
    clear: () => values.clear(),
    getItem: (key) => values.get(key) ?? null,
    key: (index) => [...values.keys()][index] ?? null,
    get length() {
      return values.size
    },
    removeItem: (key) => values.delete(key),
    setItem: (key, value) => values.set(key, String(value))
  }
}

function cloneJson(value) {
  return JSON.parse(JSON.stringify(value))
}

function collectElements(node, result = [], seen = new Set()) {
  if (node == null || typeof node !== 'object' || seen.has(node)) {
    return result
  }
  seen.add(node)
  if (Array.isArray(node)) {
    for (const child of node) {
      collectElements(child, result, seen)
    }
    return result
  }
  if ('type' in node && 'props' in node) {
    result.push(node)
    for (const value of Object.values(node.props)) {
      collectElements(value, result, seen)
    }
  } else {
    for (const value of Object.values(node)) {
      collectElements(value, result, seen)
    }
  }
  return result
}

function findSingleElement(root, predicate, description) {
  const matches = collectElements(root).filter(predicate)
  assert.equal(matches.length, 1, `Expected one ${description}, found ${matches.length}`)
  return matches[0]
}

async function settleAsyncWork() {
  await Promise.resolve()
  await new Promise((resolve) => setImmediate(resolve))
}

function createHookedRenderer() {
  const cells = []
  const effectRecords = []
  let cursor = 0
  let renderImpl
  let latestRoot
  let renderQueued = false

  function rerender() {
    cursor = 0
    latestRoot = renderImpl()
    return latestRoot
  }

  function queueRender() {
    if (renderQueued) return
    renderQueued = true
    queueMicrotask(() => {
      renderQueued = false
      rerender()
    })
  }

  function flushEffects() {
    for (const record of effectRecords) {
      if (record == null || record.ran) continue
      record.ran = true
      const cleanup = record.effect()
      if (typeof cleanup === 'function') record.cleanup = cleanup
    }
  }

  const react = {
    useState(initial) {
      const index = cursor++
      if (cells[index] === undefined) {
        cells[index] = typeof initial === 'function' ? initial() : initial
      }
      return [
        cells[index],
        (update) => {
          const next = typeof update === 'function' ? update(cells[index]) : update
          if (Object.is(next, cells[index])) return
          cells[index] = next
          queueRender()
        }
      ]
    },
    useEffect(effect, deps) {
      const index = cursor++
      const previous = effectRecords[index]
      const depsChanged =
        previous == null ||
        previous.deps == null ||
        deps == null ||
        previous.deps.length !== deps.length ||
        previous.deps.some((dep, depIndex) => !Object.is(dep, deps[depIndex]))
      if (!depsChanged) return
      previous?.cleanup?.()
      effectRecords[index] = { effect, deps, ran: false }
    },
    useCallback(fn, deps) {
      return react.useMemo(() => fn, deps)
    },
    useRef(initial) {
      const index = cursor++
      if (cells[index] === undefined) {
        cells[index] = { current: initial }
      }
      return cells[index]
    },
    useMemo(fn, deps) {
      const index = cursor++
      const previous = cells[index]
      if (previous == null || deps == null || previous.deps == null ||
          previous.deps.length !== deps.length ||
          previous.deps.some((dep, depIndex) => !Object.is(dep, deps[depIndex]))) {
        cells[index] = { value: fn(), deps }
      }
      return cells[index].value
    }
  }

  return {
    react,
    render(renderFn) {
      renderImpl = renderFn
      rerender()
      flushEffects()
      return latestRoot
    },
    async settle() {
      for (let attempt = 0; attempt < 12; attempt += 1) {
        flushEffects()
        await settleAsyncWork()
      }
      return latestRoot
    },
    get root() {
      return latestRoot
    },
    invalidate: queueRender,
    cleanup() {
      for (const record of effectRecords) {
        record?.cleanup?.()
      }
    }
  }
}

function createAppearanceFixture({
  application = { UnrelatedSetting: 'preserved' },
  omitApplicationScope = false,
  storage = {},
  loadError,
  setError,
  saveError
} = {}) {
  const calls = {
    errors: [],
    languageChanges: [],
    loads: [],
    warnings: [],
    saves: [],
    sets: []
  }
  const localStorage = createMemoryStorage(storage)
  const style = {
    zoom: '',
    removeProperty(property) {
      if (property !== 'zoom') return ''
      const previous = this.zoom
      this.zoom = ''
      return previous
    }
  }
  const globals = {
    document: {
      documentElement: {
        style,
        // themeStore applies <html data-style="..."> on module load
        setAttribute() {},
        getAttribute: () => null,
        removeAttribute() {}
      }
    },
    localStorage,
    window: { bridge: { platform: 'web' } }
  }
  const initialApplication = cloneJson(application)
  const settingsApi = {
    getAll: async (scopes) => {
      calls.loads.push(scopes == null ? undefined : Array.from(scopes))
      if (loadError != null) throw loadError
      return { scopes: { application: cloneJson(initialApplication) } }
    },
    onChanged: () => () => undefined,
    save: async (scopes) => {
      const savedScopes = scopes == null ? undefined : Array.from(scopes)
      calls.saves.push(savedScopes)
      if (saveError != null) throw saveError
      return { saved: savedScopes ?? [] }
    },
    set: async (scope, value) => {
      calls.sets.push({ scope, value: cloneJson(value) })
      if (setError != null) throw setError
      return undefined
    }
  }
  const zustand = createZustandMock()
  const uiScaleModule = loadModule(uiScaleUrl, {}, globals)
  const settingsStoreModule = loadModule(
    settingsStoreUrl,
    {
      './settings': { settingsApi },
      '../format/logger': { logger: { warn: (...args) => calls.warnings.push(args) } },
      zustand
    },
    globals
  )
  settingsStoreModule.useSettingsStore.setState({
    loading: false,
    scopes: omitApplicationScope ? {} : { application: cloneJson(initialApplication) }
  })
  const themeStoreModule = loadModule(
    themeStoreUrl,
    { zustand, './uiScale': uiScaleModule },
    globals
  )

  const effectCleanups = []
  const Select = function Select() {}
  const Checkbox = function Checkbox() {}
  const ColorPicker = function ColorPicker() {}
  const SettingsCard = function SettingsCard() {}
  const react = {
    useEffect(effect) {
      const cleanup = effect()
      if (typeof cleanup === 'function') effectCleanups.push(cleanup)
    },
    useState(initial) {
      return [typeof initial === 'function' ? initial() : initial, () => undefined]
    },
    useRef(initial) {
      return { current: initial }
    },
    useMemo(fn) {
      return fn()
    },
    useCallback(fn) {
      return fn
    }
  }
  const systemApi = {
    getAccentColor: async () => ({ r: 0, g: 120, b: 212 }),
    setAccentColor: async () => undefined
  }
  const appearanceModule = loadModule(
    appearanceSectionUrl,
    {
      '../../../shared/settings/settings': { settingsApi },
      '../../../shared/bridge/system': { systemApi },
      '../../../shared/i18n': {
        LANGUAGES: [
          { code: 'en', name: 'English' },
          { code: 'zh-Hans', name: 'Chinese' }
        ],
        changeLanguage: async (language) => {
          calls.languageChanges.push(language)
        }
      },
      '../../../shared/settings/settingsStore': settingsStoreModule,
      '../../../shared/theme/themeStore': themeStoreModule,
      '../../../shared/theme/useTheme': { storeAccentPreference: () => undefined },
      '../../../shared/format/fonts': {
        FONT_PRESETS: [
          { value: 'system', labelKey: 'settings.appearance.fontPresets.system', defaultLabel: 'System Default' }
        ],
        applyAppFont: () => undefined,
        getStoredAppFont: () => 'system'
      },
      '../../../shared/ui/ColorPicker': { __esModule: true, default: ColorPicker },
      './SettingsCard': { SettingsCard },
      './temperaturePreference': loadModule(new URL('../src/renderer/src/features/settings/components/temperaturePreference.ts', import.meta.url), {}, globals),
      '@fluentui/react-icons': {},
      antd: {
        Checkbox,
        Select,
        message: {
          error: (message) => calls.errors.push(message)
        }
      },
      react,
      'react-i18next': {
        useTranslation: () => ({
          i18n: { language: 'en' },
          t: (key) => key
        })
      },
      'react/jsx-runtime': jsxRuntime
    },
    globals
  )
  const root = appearanceModule.default()

  return {
    calls,
    cleanup() {
      for (const cleanup of effectCleanups.reverse()) cleanup()
    },
    localStorage,
    root,
    settingsStore: settingsStoreModule.useSettingsStore,
    style,
    themeStore: themeStoreModule.useThemeStore,
    types: { Checkbox, Select, SettingsCard }
  }
}

test('language selection invokes the language change path', async (t) => {
  const fixture = createAppearanceFixture()
  t.after(fixture.cleanup)
  await settleAsyncWork()

  const languageSelect = findSingleElement(
    fixture.root,
    (element) =>
      element.type === fixture.types.Select &&
      element.props.className.includes('--language'),
    'language select'
  )
  languageSelect.props.onChange('zh-Hans')

  assert.deepEqual(fixture.calls.languageChanges, ['zh-Hans'])
})

test('temperature selections persist locally and to application settings', async (t) => {
  for (const unit of ['C', 'F']) {
    await t.test(unit, async (t) => {
      const fixture = createAppearanceFixture({
        storage: { 'udt-temperature-unit': unit === 'C' ? 'F' : 'C' }
      })
      t.after(fixture.cleanup)
      await settleAsyncWork()

      const temperatureSelect = findSingleElement(
        fixture.root,
        (element) =>
          element.type === fixture.types.Select &&
          element.props.className === 'udt-settings-select',
        'temperature select'
      )
      temperatureSelect.props.onChange(unit)
      await settleAsyncWork()

      assert.equal(fixture.localStorage.getItem('udt-temperature-unit'), unit)
      assert.equal(
        fixture.settingsStore.getState().scopes.application.TemperatureUnit,
        unit
      )
      assert.deepEqual(fixture.calls.sets, [
        {
          scope: 'application',
          value: { UnrelatedSetting: 'preserved', TemperatureUnit: unit }
        }
      ])
      assert.deepEqual(fixture.calls.saves, [['application']])
    })
  }
})

test('UI scale selection updates and persists the theme store', async (t) => {
  const fixture = createAppearanceFixture()
  t.after(fixture.cleanup)
  await settleAsyncWork()

  const scaleSelect = findSingleElement(
    fixture.root,
    (element) =>
      element.type === fixture.types.Select &&
      element.props.className.includes('--scale'),
    'UI scale select'
  )
  scaleSelect.props.onChange(1.25)

  assert.equal(fixture.themeStore.getState().uiScale, 1.25)
  assert.equal(fixture.themeStore.getState().uiScalePreference, 1.25)
  assert.equal(fixture.localStorage.getItem('udt-ui-scale'), '1.25')
  assert.equal(fixture.style.zoom, '1.25')
})

test('UI scale Auto preference persists and leaves the applied scale unlocked', async (t) => {
  const fixture = createAppearanceFixture({ storage: { 'udt-ui-scale': '1.25' } })
  t.after(fixture.cleanup)
  await settleAsyncWork()

  const scaleSelect = findSingleElement(
    fixture.root,
    (element) =>
      element.type === fixture.types.Select &&
      element.props.className.includes('--scale'),
    'UI scale select'
  )
  assert.equal(scaleSelect.props.value, 1.25)

  scaleSelect.props.onChange('auto')

  assert.equal(fixture.themeStore.getState().uiScalePreference, 'auto')
  assert.equal(fixture.localStorage.getItem('udt-ui-scale'), 'auto')
  assert.equal(scaleSelect.props.options[0].value, 'auto')
  assert.equal(scaleSelect.props.options.at(-1).value, 1.5)
})

test('UI scale manual selection reaches 150 percent', async (t) => {
  const fixture = createAppearanceFixture()
  t.after(fixture.cleanup)
  await settleAsyncWork()

  const scaleSelect = findSingleElement(
    fixture.root,
    (element) =>
      element.type === fixture.types.Select &&
      element.props.className.includes('--scale'),
    'UI scale select'
  )
  scaleSelect.props.onChange(1.5)

  assert.equal(fixture.themeStore.getState().uiScale, 1.5)
  assert.equal(fixture.themeStore.getState().uiScalePreference, 1.5)
  assert.equal(fixture.localStorage.getItem('udt-ui-scale'), '1.5')
  assert.equal(fixture.style.zoom, '1.5')
})

test('theme selections persist System, Light, and Dark representations', async (t) => {
  for (const [applicationTheme, storeTheme] of [
    ['System', 'system'],
    ['Light', 'light'],
    ['Dark', 'dark']
  ]) {
    await t.test(applicationTheme, async (t) => {
      const fixture = createAppearanceFixture()
      t.after(fixture.cleanup)
      await settleAsyncWork()

      const themeOption = findSingleElement(
        fixture.root,
        (element) => element.props.option?.value === applicationTheme,
        `${applicationTheme} theme option`
      )
      themeOption.props.onClick()
      await settleAsyncWork()

      assert.equal(fixture.themeStore.getState().themePreference, storeTheme)
      assert.equal(fixture.localStorage.getItem('udt.theme'), storeTheme)
      assert.equal(
        fixture.settingsStore.getState().scopes.application.Theme,
        applicationTheme
      )
      assert.deepEqual(fixture.calls.sets, [
        {
          scope: 'application',
          value: { UnrelatedSetting: 'preserved', Theme: applicationTheme }
        }
      ])
      assert.deepEqual(fixture.calls.saves, [['application']])
    })
  }
})

test('failed application persistence surfaces the existing save error path', async (t) => {
  const fixture = createAppearanceFixture({
    saveError: new Error('save failed')
  })
  t.after(fixture.cleanup)
  await settleAsyncWork()

  const temperatureSelect = findSingleElement(
    fixture.root,
    (element) =>
      element.type === fixture.types.Select &&
      element.props.className === 'udt-settings-select',
    'temperature select'
  )
  temperatureSelect.props.onChange('F')
  await settleAsyncWork()

  assert.equal(fixture.calls.sets.length, 1)
  assert.deepEqual(fixture.calls.saves, [['application']])
  assert.deepEqual(fixture.calls.errors, ['settings.saveFailed'])
})

function loadSettingsCard() {
  return loadModule(settingsCardUrl, {
    '@fluentui/react-icons': {
      ChevronRight16Regular: function ChevronRight16Regular() {}
    },
    'react/jsx-runtime': jsxRuntime
  }).SettingsCard
}

test('clickable SettingsCard activates with Enter and Space', () => {
  const SettingsCard = loadSettingsCard()
  let activations = 0
  let prevented = 0
  const card = SettingsCard({
    onClick: () => {
      activations += 1
    },
    title: 'Clickable setting'
  })
  const keyDown = (key) =>
    card.props.onKeyDown({
      key,
      preventDefault: () => {
        prevented += 1
      }
    })

  assert.equal(card.props.role, 'button')
  assert.equal(card.props.tabIndex, 0)

  keyDown('ArrowRight')
  assert.equal(activations, 0)
  assert.equal(prevented, 0)

  keyDown('Enter')
  keyDown(' ')
  assert.equal(activations, 2)
  assert.equal(prevented, 2)
})

test('non-clickable SettingsCard rows have no interactive role', () => {
  const SettingsCard = loadSettingsCard()
  let prevented = false
  const card = SettingsCard({
    action: jsxRuntime.jsx('span', { children: 'Current value' }),
    title: 'Read-only setting'
  })

  assert.equal(card.props.role, undefined)
  assert.equal(card.props.tabIndex, undefined)
  assert.equal(card.props.onClick, undefined)

  card.props.onKeyDown({
    key: 'Enter',
    preventDefault: () => {
      prevented = true
    }
  })
  assert.equal(prevented, false)
})

test('appearance editors stay disabled until the application scope is loaded', async (t) => {
  const fixture = createAppearanceFixture({ omitApplicationScope: true })
  t.after(fixture.cleanup)
  await settleAsyncWork()

  const languageSelect = findSingleElement(
    fixture.root,
    (element) =>
      element.type === fixture.types.Select &&
      element.props.className.includes('--language'),
    'language select'
  )
  const temperatureSelect = findSingleElement(
    fixture.root,
    (element) =>
      element.type === fixture.types.Select &&
      element.props.className === 'udt-settings-select',
    'temperature select'
  )

  assert.equal(languageSelect.props.disabled, true)
  assert.equal(temperatureSelect.props.disabled, true)

  temperatureSelect.props.onChange('F')
  await settleAsyncWork()
  assert.equal(fixture.calls.sets.length, 0)
  assert.equal(fixture.calls.saves.length, 0)
})

test('appearance background refresh handles read failure and keeps cached settings', async (t) => {
  const fixture = createAppearanceFixture({ loadError: new Error('Host unavailable') })
  t.after(fixture.cleanup)
  await settleAsyncWork()
  assert.equal(fixture.calls.warnings.length, 1)
  assert.equal(fixture.settingsStore.getState().loading, false)
  assert.equal(fixture.settingsStore.getState().scopes.application.UnrelatedSetting, 'preserved')
})

function loadSettingsLoadError() {
  return loadModule(settingsLoadErrorUrl, {
    'react-i18next': {
      useTranslation: () => ({
        t: (key, options) => options?.defaultValue ?? key
      })
    },
    'react/jsx-runtime': jsxRuntime
  }).SettingsLoadError
}

test('SettingsLoadError exposes retry without enabling editors', () => {
  const SettingsLoadError = loadSettingsLoadError()
  let retries = 0
  const root = SettingsLoadError({
    message: 'host is not running',
    onRetry: () => {
      retries += 1
    }
  })

  assert.equal(root.props.role, 'alert')
  const retry = findSingleElement(
    root,
    (element) => element.type === 'button' && element.props.onClick != null,
    'retry button'
  )
  retry.props.onClick()
  assert.equal(retries, 1)
  const message = findSingleElement(
    root,
    (element) => element.type === 'p' && element.props.children === 'host is not running',
    'error message'
  )
  assert.equal(message.props.children, 'host is not running')
})

function createSettingsPageFixture({ loadImpl, featuresImpl } = {}) {
  const calls = { loads: 0, features: 0, finished: 0, warnings: [] }
  const loadingStore = {
    start: () => 'settings-load',
    finish: () => {
      calls.finished += 1
    },
    getState() {
      return this
    }
  }
  const settingsStore = {
    scopes: {},
    async load() {
      calls.loads += 1
      if (loadImpl != null) return loadImpl()
      this.scopes = { application: { Theme: 'Dark' } }
    },
    getState() {
      return this
    }
  }
  const featuresApi = {
    async list() {
      calls.features += 1
      if (featuresImpl != null) return featuresImpl()
      return []
    }
  }
  const Section = function Section() {}
  const SettingsSectionSkeleton = function SettingsSectionSkeleton() {}
  const SettingsLoadError = function SettingsLoadError() {}
  const Tooltip = function Tooltip() {}
  const Icon = function Icon() {}
  const translate = (key, options) => options?.defaultValue ?? key
  const renderer = createHookedRenderer()
  const pageModule = loadModule(
    settingsPageUrl,
    {
      '../../shared/bridge/bridge': {
        isHostUnavailableError: (message) => /host is not running/i.test(String(message)),
        sanitizeBridgeError: (error) => (error instanceof Error ? error.message : String(error))
      },
      '../dashboard/api/features': { featuresApi },
      '../../shared/ui/icons/fluent': {
        Apps24Regular: Icon,
        ArrowSync24Regular: Icon,
        Desktop24Regular: Icon,
        Eye24Regular: Icon,
        Key24Regular: Icon,
        PaintBrush24Regular: Icon,
        PlugConnected24Regular: Icon,
        Power24Regular: Icon
      },
      '../../shared/ui/CachedView': { __esModule: true, default: function CachedView() {} },
      './components/AppearanceSection': { __esModule: true, default: Section },
      './components/ApplicationSection': { __esModule: true, default: Section },
      './components/DisplaySection': { DisplaySection: Section },
      './components/IntegrationsSection': { IntegrationsSection: Section },
      './components/OsdSection': { OsdSection: Section },
      './components/PowerSection': { PowerSection: Section },
      './components/SettingsLoadError': { SettingsLoadError },
      './components/SettingsSkeleton': { SettingsSectionSkeleton },
      './components/SmartKeysSection': { SmartKeysSection: Section },
      './components/UpdateSection': { UpdateSection: Section },
      './components/settings.css': {},
      '../../shared/state/loadingStore': { useLoadingStore: loadingStore },
      '../../shared/settings/settingsStore': { useSettingsStore: settingsStore },
      antd: { Tooltip },
      react: renderer.react,
      'react-i18next': {
        useTranslation: () => ({
          t: translate
        })
      },
      'react/jsx-runtime': jsxRuntime
    },
    { console: { warn: (...args) => calls.warnings.push(args) } }
  )

  renderer.render(() => pageModule.default())
  return {
    calls,
    renderer,
    types: { Section, SettingsLoadError, SettingsSectionSkeleton }
  }
}

test('settings page keeps the skeleton until scopes load and then enables editors', async (t) => {
  let resolveLoad
  const fixture = createSettingsPageFixture({
    loadImpl: () =>
      new Promise((resolve) => {
        resolveLoad = resolve
      })
  })
  t.after(() => fixture.renderer.cleanup())

  assert.equal(
    collectElements(fixture.renderer.root).some(
      (element) => element.type === fixture.types.SettingsSectionSkeleton
    ),
    true
  )
  assert.equal(
    collectElements(fixture.renderer.root).some((element) => element.type === fixture.types.Section),
    false
  )

  resolveLoad()
  const root = await fixture.renderer.settle()
  assert.equal(
    collectElements(root).some(
      (element) => element.type === fixture.types.SettingsSectionSkeleton
    ),
    false
  )
  assert.equal(
    collectElements(root).some((element) => element.type === fixture.types.Section),
    true
  )
  assert.equal(fixture.calls.loads, 1)
})

test('settings page shows error and retry instead of default editors when load fails', async (t) => {
  let shouldFail = true
  const fixture = createSettingsPageFixture({
    loadImpl: async () => {
      if (shouldFail) throw new Error('host is not running')
    }
  })
  t.after(() => fixture.renderer.cleanup())

  let root = await fixture.renderer.settle()
  assert.equal(
    collectElements(root).some((element) => element.type === fixture.types.Section),
    false
  )
  const error = findSingleElement(
    root,
    (element) => element.type === fixture.types.SettingsLoadError,
    'settings load error'
  )
  assert.match(String(error.props.message), /host is not running|backend host/)
  assert.equal(typeof error.props.onRetry, 'function')

  shouldFail = false
  error.props.onRetry()
  root = await fixture.renderer.settle()
  assert.equal(fixture.calls.loads, 2)
  assert.equal(
    collectElements(root).some((element) => element.type === fixture.types.SettingsLoadError),
    false
  )
  assert.equal(
    collectElements(root).some((element) => element.type === fixture.types.Section),
    true
  )
})

test('settings page handles an early capability rejection while settings are pending', async (t) => {
  let finishLoading
  const fixture = createSettingsPageFixture({
    loadImpl: () => new Promise((resolve) => { finishLoading = resolve }),
    featuresImpl: async () => { throw new Error('capability query failed') }
  })
  t.after(() => fixture.renderer.cleanup())

  await fixture.renderer.settle()
  assert.equal(fixture.calls.warnings.length, 1)
  assert.match(fixture.calls.warnings[0].join(' '), /capability query failed/)
  finishLoading()
  const root = await fixture.renderer.settle()
  assert.equal(
    collectElements(root).some((element) => element.type === fixture.types.Section),
    true
  )
  assert.equal(
    collectElements(root).some((element) => element.type === fixture.types.SettingsLoadError),
    false
  )
})

test('settings failure still handles an independently rejected capability query', async (t) => {
  const fixture = createSettingsPageFixture({
    loadImpl: async () => { throw new Error('settings unavailable') },
    featuresImpl: async () => { throw new Error('capability query failed') }
  })
  t.after(() => fixture.renderer.cleanup())

  const root = await fixture.renderer.settle()
  const error = findSingleElement(root,
    (element) => element.type === fixture.types.SettingsLoadError, 'settings load error')
  assert.equal(error.props.message, 'settings unavailable')
  assert.equal(fixture.calls.warnings.length, 1)
})

test('cached settings route does not lock the console scroller', () => {
  const css = readFileSync(settingsCssUrl, 'utf8')
  const route = readFileSync(cachedRouteUrl, 'utf8')
  assert.match(route, /data-udt-active=\{active \? 'true' : 'false'\}/)
  assert.match(css, /data-udt-page='\/settings'/)
  assert.match(css, /data-udt-active='true'/)
  assert.doesNotMatch(css, /:has\(\.udt-settings-page\)/)
})

test('settings nav stays inside the shell at the stacked breakpoint', () => {
  const css = readFileSync(settingsCssUrl, 'utf8')
  const stacked = css.match(
    /@container udt-settings-shell \(max-width: 720px\) \{([\s\S]*?)\n\}/
  )
  assert.ok(stacked != null, 'expected stacked settings breakpoint')
  const navBlock = stacked[1].match(/\.udt-settings-page__nav \{([^}]+)\}/)
  assert.ok(navBlock != null, 'expected stacked nav rules')
  assert.match(navBlock[1], /min-width:\s*0/)
  assert.match(navBlock[1], /overflow-x:\s*auto/)
  assert.doesNotMatch(navBlock[1], /overflow:\s*visible/)
  assert.doesNotMatch(navBlock[1], /!important/)
})


test('a device group hidden by capability discovery falls back to the visible appearance editor', async (t) => {
  let finishLoading
  const fixture = createSettingsPageFixture({
    loadImpl: () => new Promise((resolve) => { finishLoading = resolve }),
    featuresImpl: async () => []
  })
  t.after(() => fixture.renderer.cleanup())
  const deviceButton = findSingleElement(fixture.renderer.root,
    (element) => element.type === 'button' && collectElements(element).some(
      (child) => child.props.children === 'settings.nav.device'), 'device group')
  deviceButton.props.onClick()
  await fixture.renderer.settle()
  finishLoading()
  const root = await fixture.renderer.settle()
  const selected = findSingleElement(root,
    (element) => element.type === 'button' && element.props['aria-current'] === 'true', 'selected group')
  assert.ok(collectElements(selected).some((child) => child.props.children === 'settings.nav.appearance'))
  const content = findSingleElement(root,
    (element) => element.type === 'section' && element.props['aria-label'] === 'settings.nav.appearance', 'appearance content')
  assert.ok(collectElements(content).some((element) => element.type === fixture.types.Section))
})

const sanitizeError = (reason) => reason instanceof Error ? reason.message : String(reason)
const defaultOsdSettings = loadModule(osdApiUrl, {
  '../../../shared/settings/settings': { settingsApi: {} }
}).DEFAULT_OSD_SETTINGS

function createOsdStoreFixture({ getImpl, saveImpl } = {}) {
  const calls = { loads: 0, saves: [] }
  const api = {
    async get() {
      calls.loads += 1
      return getImpl == null ? { ...defaultOsdSettings, showOsd: true } : getImpl()
    },
    async save(settings) {
      calls.saves.push(cloneJson(settings))
      if (saveImpl != null) await saveImpl(settings)
    }
  }
  const module = loadModule(osdStoreUrl, {
    zustand: createZustandMock(),
    '../../../shared/bridge/bridge': { sanitizeBridgeError: sanitizeError },
    '../api/osd': { DEFAULT_OSD_SETTINGS: defaultOsdSettings, osdApi: api }
  })
  return { calls, store: module.useOsdSettingsStore }
}

function createOsdSectionFixture(options = {}) {
  const fixture = createOsdStoreFixture(options)
  const renderer = createHookedRenderer()
  const timers = new Map()
  const errors = []
  let nextTimer = 0
  const Button = function Button() {}
  const Checkbox = function Checkbox() {}
  const ColorPicker = function ColorPicker() {}
  const InputNumber = function InputNumber() {}
  const Select = function Select() {}
  const Slider = function Slider() {}
  const Switch = function Switch() {}
  const Tabs = function Tabs() {}
  const SettingsCard = function SettingsCard() {}
  const SettingsLoadError = function SettingsLoadError() {}
  const SettingsSectionSkeleton = function SettingsSectionSkeleton() {}
  const translate = (key) => key
  const module = loadModule(osdSectionUrl, {
    react: renderer.react,
    antd: {
      Button, Checkbox, ColorPicker, InputNumber, Select, Slider, Switch, Tabs,
      message: { error: (value) => { errors.push(value) } }
    },
    'react-i18next': { useTranslation: () => ({ t: translate }) },
    '../../../shared/bridge/bridge': { sanitizeBridgeError: sanitizeError },
    '../../dashboard/api/sensors': {
      sensorsApi: { getStatus: async () => ({ isHybrid: false }) }
    },
    '../stores/osdSettingsStore': { useOsdSettingsStore: fixture.store },
    './SettingsCard': { SettingsCard },
    './SettingsLoadError': { SettingsLoadError },
    './SettingsSkeleton': { SettingsSectionSkeleton },
    'react/jsx-runtime': jsxRuntime
  }, {
    console,
    window: {
      setTimeout(callback) {
        const timer = ++nextTimer
        timers.set(timer, callback)
        return timer
      },
      clearTimeout(timer) { timers.delete(timer) }
    }
  })
  const unsubscribe = fixture.store.subscribe(renderer.invalidate)
  renderer.render(() => module.OsdSection())
  return {
    ...fixture, renderer, timers, errors,
    types: { ColorPicker, Slider, Switch, SettingsLoadError, SettingsSectionSkeleton },
    cleanup() {
      unsubscribe()
      renderer.cleanup()
    },
    flushTimers() {
      const pending = [...timers.values()]
      timers.clear()
      for (const callback of pending) callback()
    }
  }
}

test('OSD cannot overwrite settings before a successful load', async () => {
  const fixture = createOsdStoreFixture({ getImpl: async () => { throw 'OSD unavailable' } })
  assert.equal(await fixture.store.getState().update({ showOsd: true }), false)
  await fixture.store.getState().load()
  assert.equal(fixture.store.getState().loaded, false)
  assert.equal(fixture.store.getState().error, 'OSD unavailable')
  assert.equal(await fixture.store.getState().update({ fontSize: 20 }), false)
  assert.equal(fixture.calls.saves.length, 0)
})

test('OSD saves are serialized and retain both rapid edits', async () => {
  let finishFirst
  const fixture = createOsdStoreFixture({
    saveImpl: async () => {
      if (fixture.calls.saves.length === 1) {
        await new Promise((resolve) => { finishFirst = resolve })
      }
    }
  })
  await fixture.store.getState().load()
  const first = fixture.store.getState().update({ fontSize: 18 })
  const second = fixture.store.getState().update({ backgroundOpacity: 0.4 })
  await settleAsyncWork()
  assert.equal(fixture.calls.saves.length, 1)
  finishFirst()
  assert.equal(await first, true)
  assert.equal(await second, true)
  assert.equal(fixture.calls.saves.length, 2)
  assert.equal(fixture.calls.saves[1].fontSize, 18)
  assert.equal(fixture.calls.saves[1].backgroundOpacity, 0.4)
})

test('OSD save failure remains visible and does not block the next edit', async () => {
  let fail = true
  const fixture = createOsdStoreFixture({
    saveImpl: async () => { if (fail) throw 'save interrupted' }
  })
  await fixture.store.getState().load()
  assert.equal(await fixture.store.getState().update({ fontSize: 18 }), false)
  assert.equal(fixture.store.getState().error, 'save interrupted')
  fail = false
  assert.equal(await fixture.store.getState().update({ isLocked: true }), true)
  assert.equal(fixture.store.getState().error, null)
  assert.equal(fixture.calls.saves[1].fontSize, 18)
})

test('OSD reload waits for queued saves and blocks edits while loading', async () => {
  let finishSave
  let persisted = { ...defaultOsdSettings, showOsd: true }
  const fixture = createOsdStoreFixture({
    getImpl: async () => persisted,
    saveImpl: async (settings) => {
      await new Promise((resolve) => { finishSave = resolve })
      persisted = settings
    }
  })
  await fixture.store.getState().load()
  const saving = fixture.store.getState().update({ fontSize: 18 })
  const loading = fixture.store.getState().load()
  await settleAsyncWork()
  assert.equal(fixture.calls.loads, 1)
  assert.equal(await fixture.store.getState().update({ isLocked: true }), false)
  finishSave()
  await saving
  await loading
  assert.equal(fixture.calls.loads, 2)
  assert.equal(fixture.store.getState().settings.fontSize, 18)
  assert.equal(fixture.store.getState().settings.isLocked, false)
})

test('OSD load failure shows retry and does not expose default editors', async (t) => {
  let fail = true
  const fixture = createOsdSectionFixture({
    getImpl: async () => {
      if (fail) throw new Error('OSD unavailable')
      return { ...defaultOsdSettings, showOsd: true, fontSize: 17 }
    }
  })
  t.after(fixture.cleanup)
  let root = await fixture.renderer.settle()
  const error = findSingleElement(root,
    (element) => element.type === fixture.types.SettingsLoadError, 'OSD load error')
  assert.equal(error.props.message, 'OSD unavailable')
  assert.equal(collectElements(root).some((element) => element.type === fixture.types.Switch), false)
  fail = false
  error.props.onRetry()
  root = await fixture.renderer.settle()
  assert.equal(collectElements(root).some((element) => element.type === fixture.types.SettingsLoadError), false)
  assert.equal(fixture.store.getState().settings.fontSize, 17)
  assert.equal(fixture.calls.saves.length, 0)
})

test('OSD save errors use the existing visible error message', async (t) => {
  const fixture = createOsdSectionFixture({
    saveImpl: async () => { throw new Error('write denied') }
  })
  t.after(fixture.cleanup)
  const root = await fixture.renderer.settle()
  const toggle = findSingleElement(root,
    (element) => element.type === fixture.types.Switch && element.props.checked === true,
    'OSD enabled switch')
  toggle.props.onChange(false)
  await fixture.renderer.settle()
  assert.deepEqual(fixture.errors, ['settings.saveFailed: write denied'])
})

test('leaving the OSD editor flushes merged slider and color edits', async () => {
  const fixture = createOsdSectionFixture()
  const root = await fixture.renderer.settle()
  const opacity = findSingleElement(root,
    (element) => element.type === fixture.types.Slider && element.props.max === 1,
    'OSD opacity slider')
  const background = findSingleElement(root,
    (element) => element.type === fixture.types.ColorPicker && element.props.value === '#1E1E1E',
    'OSD background color')
  opacity.props.onChange(0.3)
  background.props.onChange({ toHexString: () => '#112233' })
  assert.equal(fixture.calls.saves.length, 0)
  assert.equal(fixture.timers.size, 1)
  fixture.cleanup()
  await settleAsyncWork()
  assert.equal(fixture.timers.size, 0)
  assert.equal(fixture.calls.saves.length, 1)
  assert.equal(fixture.calls.saves[0].backgroundOpacity, 0.3)
  assert.equal(fixture.calls.saves[0].backgroundColor, '#112233')
})

test('OSD debounce saves the latest slider value once', async (t) => {
  const fixture = createOsdSectionFixture()
  t.after(fixture.cleanup)
  const root = await fixture.renderer.settle()
  const opacity = findSingleElement(root,
    (element) => element.type === fixture.types.Slider && element.props.max === 1,
    'OSD opacity slider')
  opacity.props.onChange(0.2)
  opacity.props.onChange(0.5)
  assert.equal(fixture.timers.size, 1)
  fixture.flushTimers()
  await fixture.renderer.settle()
  assert.equal(fixture.calls.saves.length, 1)
  assert.equal(fixture.calls.saves[0].backgroundOpacity, 0.5)
})
