import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { setImmediate } from 'node:timers'
import { URL, fileURLToPath } from 'node:url'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'

const appearanceSectionUrl = new URL(
  '../src/renderer/src/components/settings/AppearanceSection.tsx',
  import.meta.url
)
const settingsPageUrl = new URL('../src/renderer/src/pages/SettingsPage.tsx', import.meta.url)
const settingsCssUrl = new URL(
  '../src/renderer/src/components/settings/settings.css',
  import.meta.url
)
const settingsLoadErrorUrl = new URL(
  '../src/renderer/src/components/settings/SettingsLoadError.tsx',
  import.meta.url
)
const settingsCardUrl = new URL(
  '../src/renderer/src/components/settings/SettingsCard.tsx',
  import.meta.url
)
const settingsStoreUrl = new URL(
  '../src/renderer/src/stores/settingsStore.ts',
  import.meta.url
)
const themeStoreUrl = new URL(
  '../src/renderer/src/stores/themeStore.ts',
  import.meta.url
)
const uiScaleUrl = new URL('../src/renderer/src/theme/uiScale.ts', import.meta.url)

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
    useCallback(fn) {
      cursor += 1
      return fn
    },
    useRef(initial) {
      const index = cursor++
      if (cells[index] === undefined) {
        cells[index] = { current: initial }
      }
      return cells[index]
    },
    useMemo(fn) {
      cursor += 1
      return fn()
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
  setError,
  saveError
} = {}) {
  const calls = {
    errors: [],
    languageChanges: [],
    loads: [],
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
      '../api/settings': { settingsApi },
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
    { zustand, '../theme/uiScale': uiScaleModule },
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
      '../../api/settings': { settingsApi },
      '../../api/system': { systemApi },
      '../../i18n': {
        LANGUAGES: [
          { code: 'en', name: 'English' },
          { code: 'zh-Hans', name: 'Chinese' }
        ],
        changeLanguage: async (language) => {
          calls.languageChanges.push(language)
        }
      },
      '../../stores/settingsStore': settingsStoreModule,
      '../../stores/themeStore': themeStoreModule,
      '../../theme/useTheme': { storeAccentPreference: () => undefined },
      '../../utils/fonts': {
        FONT_PRESETS: [
          { value: 'system', labelKey: 'settings.appearance.fontPresets.system', defaultLabel: 'System Default' }
        ],
        applyAppFont: () => undefined,
        getStoredAppFont: () => 'system'
      },
      '../ColorPicker': { __esModule: true, default: ColorPicker },
      './SettingsCard': { SettingsCard },
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
  const calls = { loads: 0, features: 0, finished: 0 }
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
      '../api/bridge': {
        isHostUnavailableError: (message) => /host is not running/i.test(String(message)),
        sanitizeBridgeError: (error) => (error instanceof Error ? error.message : String(error))
      },
      '../api/features': { featuresApi },
      '../components/icons/fluent': {
        Apps24Regular: Icon,
        ArrowSync24Regular: Icon,
        Desktop24Regular: Icon,
        Eye24Regular: Icon,
        Key24Regular: Icon,
        PaintBrush24Regular: Icon,
        PlugConnected24Regular: Icon,
        Power24Regular: Icon
      },
      '../components/settings/AppearanceSection': { __esModule: true, default: Section },
      '../components/settings/ApplicationSection': { __esModule: true, default: Section },
      '../components/settings/DisplaySection': { DisplaySection: Section },
      '../components/settings/IntegrationsSection': { IntegrationsSection: Section },
      '../components/settings/OsdSection': { OsdSection: Section },
      '../components/settings/PowerSection': { PowerSection: Section },
      '../components/settings/SettingsLoadError': { SettingsLoadError },
      '../components/settings/SettingsSkeleton': { SettingsSectionSkeleton },
      '../components/settings/SmartKeysSection': { SmartKeysSection: Section },
      '../components/settings/UpdateSection': { UpdateSection: Section },
      '../components/settings/settings.css': {},
      '../stores/loadingStore': { useLoadingStore: loadingStore },
      '../stores/settingsStore': { useSettingsStore: settingsStore },
      antd: { Tooltip },
      react: renderer.react,
      'react-i18next': {
        useTranslation: () => ({
          t: translate
        })
      },
      'react/jsx-runtime': jsxRuntime
    }
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
