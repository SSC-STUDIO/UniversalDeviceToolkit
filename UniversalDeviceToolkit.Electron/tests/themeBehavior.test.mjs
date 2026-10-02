import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { registerHooks } from 'node:module'
import test from 'node:test'
import { URL } from 'node:url'
import { runInNewContext } from 'node:vm'

const HARNESS_KEY = '__udtThemeTestHarness'
const USE_THEME_PARENT = '/src/renderer/src/theme/useTheme.ts'
const MAIN_SOURCE = readFileSync(
  new URL('../src/renderer/src/main.tsx', import.meta.url),
  'utf8'
)

function extractCssBlock(css, prelude) {
  const start = css.indexOf(prelude)
  if (start < 0) return ''
  const open = css.indexOf('{', start)
  if (open < 0) return ''
  let depth = 0
  for (let index = open; index < css.length; index += 1) {
    const character = css[index]
    if (character === '{') depth += 1
    else if (character === '}') {
      depth -= 1
      if (depth === 0) return css.slice(start, index + 1)
    }
  }
  return ''
}

function extractMediaBlocks(css, query) {
  const needle = `@media (${query})`
  const blocks = []
  let searchFrom = 0
  while (searchFrom < css.length) {
    const start = css.indexOf(needle, searchFrom)
    if (start < 0) break
    const block = extractCssBlock(css.slice(start), needle)
    if (!block) break
    blocks.push(block)
    searchFrom = start + block.length
  }
  return blocks
}

function dataModule(source) {
  return `data:text/javascript,${encodeURIComponent(source)}`
}

const harnessAccessor = `const harness = () => globalThis[${JSON.stringify(HARNESS_KEY)}]\n`
const useThemeMocks = {
  react: dataModule(`${harnessAccessor}
export const useEffect = (effect, dependencies) => harness().useEffect(effect, dependencies)
`),
  '../api/settings': dataModule(`${harnessAccessor}
export const settingsApi = {
  get: (...args) => harness().settingsGet(...args),
  onChanged: (listener) => harness().onSettingsChanged(listener)
}
`),
  '../api/system': dataModule(`${harnessAccessor}
export const systemApi = {
  getAccentColor: () => harness().getAccentColor()
}
`),
  '../stores/themeStore': dataModule(`${harnessAccessor}
export const applyUiScale = (scale) => harness().applyUiScale(scale)
export const useThemeStore = Object.assign((selector) => selector(harness().store), {
  getState: () => harness().store
})
`),
  './uiScale': new URL('../src/renderer/src/theme/uiScale.ts', import.meta.url).href,
  './accentPalette': dataModule(`${harnessAccessor}
export const applyAccentSurfacePalette = (palette) => harness().applyAccentSurfacePalette(palette)
export const clearAccentSurfacePalette = () => harness().clearAccentSurfacePalette()
export const createAccentPalette = (...args) => harness().createAccentPalette(...args)
`)
}

registerHooks({
  resolve(specifier, context, nextResolve) {
    if (context.parentURL?.includes(USE_THEME_PARENT) && useThemeMocks[specifier]) {
      return { shortCircuit: true, url: useThemeMocks[specifier] }
    }
    if (
      specifier.startsWith('.') &&
      !specifier.endsWith('.ts') &&
      context.parentURL?.includes('.ts')
    ) {
      return nextResolve(`${specifier}.ts`, context)
    }
    return nextResolve(specifier, context)
  }
})

let importSequence = 0

function freshImport(relativePath) {
  const url = new URL(relativePath, import.meta.url)
  url.searchParams.set('theme-test', String(++importSequence))
  return import(url.href)
}

function installBrowserGlobals(storedTheme, systemDark, readThrows = false) {
  const values = new Map()
  if (storedTheme !== null) values.set('udt.theme', storedTheme)

  globalThis.localStorage = {
    getItem(key) {
      if (readThrows) throw new Error('storage unavailable')
      return values.get(key) ?? null
    },
    setItem(key, value) {
      values.set(key, value)
    },
    removeItem(key) {
      values.delete(key)
    }
  }

  let matchMediaCalls = 0
  globalThis.window = {
    bridge: { platform: 'web' },
    matchMedia(query) {
      assert.equal(query, '(prefers-color-scheme: dark)')
      matchMediaCalls += 1
      return { matches: systemDark }
    }
  }

  const attributes = new Map()
  const style = {
    removeProperty(property) {
      delete this[property]
    }
  }
  const root = {
    dataset: {},
    style,
    setAttribute(name, value) {
      attributes.set(name, value)
    }
  }
  globalThis.document = { documentElement: root }

  return {
    attributes,
    matchMediaCalls: () => matchMediaCalls,
    root,
    values
  }
}

function createMediaQueryList(initialMatches) {
  const listeners = new Set()
  return {
    addCalls: 0,
    matches: initialMatches,
    listeners,
    media: '(prefers-color-scheme: dark)',
    removeCalls: 0,
    addEventListener(type, listener) {
      assert.equal(type, 'change')
      this.addCalls += 1
      listeners.add(listener)
    },
    removeEventListener(type, listener) {
      assert.equal(type, 'change')
      this.removeCalls += 1
      listeners.delete(listener)
    },
    dispatch(matches) {
      this.matches = matches
      for (const listener of [...listeners]) {
        listener({ matches, media: this.media })
      }
    }
  }
}

function createHookRuntime(themePreference, systemDark) {
  const effects = []
  const media = createMediaQueryList(systemDark)
  const modeUpdates = []
  let matchMediaCalls = 0
  let settingsUnsubscribeCalls = 0

  const store = {
    accentTintsSurfaces: true,
    colorPrimary: undefined,
    setAccent(colorPrimary) {
      store.colorPrimary = colorPrimary
    },
    setAccentTintsSurfaces(accentTintsSurfaces) {
      store.accentTintsSurfaces = accentTintsSurfaces
    },
    setThemeMode(themeMode) {
      modeUpdates.push(themeMode)
      store.themeMode = themeMode
    },
    setThemePreference(preference) {
      store.themePreference = preference
    },
    setUiScale(uiScale) {
      store.uiScale = uiScale
    },
    applyComputedUiScale(uiScale) {
      store.uiScale = uiScale
    },
    themeMode: 'dark',
    themePreference,
    uiScale: 1,
    uiScalePreference: 1
  }

  const harness = {
    applyAccentSurfacePalette() {},
    applyUiScale() {},
    clearAccentSurfacePalette() {},
    createAccentPalette() {
      return {}
    },
    getAccentColor() {
      return new Promise(() => {})
    },
    onSettingsChanged() {
      return () => {
        settingsUnsubscribeCalls += 1
      }
    },
    settingsGet() {
      return new Promise(() => {})
    },
    store,
    useEffect(effect, dependencies) {
      effects.push({ cleanup: effect(), dependencies: [...dependencies] })
    }
  }

  globalThis.localStorage = {
    getItem() {
      return null
    },
    removeItem() {},
    setItem() {}
  }
  globalThis.window = {
    matchMedia(query) {
      assert.equal(query, media.media)
      matchMediaCalls += 1
      return media
    }
  }
  globalThis[HARNESS_KEY] = harness

  return {
    cleanup() {
      globalThis[HARNESS_KEY] = harness
      for (const entry of effects.splice(0).reverse()) {
        entry.cleanup?.()
      }
    },
    coreDependencies() {
      return effects.find((entry) => typeof entry.cleanup === 'function')?.dependencies
    },
    matchMediaCalls: () => matchMediaCalls,
    media,
    modeUpdates,
    render(useTheme) {
      globalThis[HARNESS_KEY] = harness
      useTheme()
    },
    settingsUnsubscribeCalls: () => settingsUnsubscribeCalls,
    store
  }
}

test('Electron renderer theme behavior', async (t) => {
  const { useTheme } = await freshImport('../src/renderer/src/theme/useTheme.ts')

  t.after(() => {
    delete globalThis[HARNESS_KEY]
    delete globalThis.document
    delete globalThis.localStorage
    delete globalThis.window
  })

  await t.test('manual Light and Dark preferences map to their forced modes', () => {
    for (const [preference, systemDark, expectedMode] of [
      ['light', true, 'light'],
      ['dark', false, 'dark']
    ]) {
      const runtime = createHookRuntime(preference, systemDark)
      runtime.render(useTheme)

      assert.equal(runtime.modeUpdates.at(-1), expectedMode)
      assert.equal(runtime.matchMediaCalls(), 0)
      assert.equal(runtime.media.listeners.size, 0)
      runtime.cleanup()
    }
  })

  await t.test('System resolves immediately and follows media-query changes', () => {
    const runtime = createHookRuntime('system', false)
    runtime.render(useTheme)

    assert.equal(runtime.modeUpdates.at(-1), 'light')
    assert.equal(runtime.media.listeners.size, 1)
    assert.equal(runtime.coreDependencies()?.[0], 'system')

    runtime.media.dispatch(true)
    assert.equal(runtime.modeUpdates.at(-1), 'dark')
    runtime.media.dispatch(false)
    assert.equal(runtime.modeUpdates.at(-1), 'light')

    runtime.cleanup()
    const updatesAfterCleanup = runtime.modeUpdates.length
    runtime.media.dispatch(true)

    assert.equal(runtime.media.listeners.size, 0)
    assert.equal(runtime.media.removeCalls, 1)
    assert.equal(runtime.modeUpdates.length, updatesAfterCleanup)
    assert.equal(runtime.settingsUnsubscribeCalls(), 1)
  })

  await t.test('leaving System detaches its listener before applying a manual mode', () => {
    const runtime = createHookRuntime('system', true)
    runtime.render(useTheme)
    assert.equal(runtime.media.listeners.size, 1)

    runtime.cleanup()
    runtime.store.themePreference = 'light'
    runtime.render(useTheme)

    assert.equal(runtime.coreDependencies()?.[0], 'light')
    assert.equal(runtime.modeUpdates.at(-1), 'light')
    assert.equal(runtime.media.addCalls, 1)
    assert.equal(runtime.media.removeCalls, 1)
    assert.equal(runtime.media.listeners.size, 0)

    runtime.cleanup()
    assert.equal(runtime.media.removeCalls, 1)
  })

  await t.test('native theme source resets to system even while the old forced mode remains', () => {
    const effect = MAIN_SOURCE.match(
      /window\.bridge\?\.setThemeSource\?\.\(\s*([^)\r\n]+?)\s*\)\s*\r?\n\s*\},\s*\[([^\]]+)\]\)/
    )
    assert.ok(effect, 'Root theme effect must call setThemeSource and declare its dependencies')

    const dependencies = effect[2].split(',').map((dependency) => dependency.trim())
    assert.ok(dependencies.includes('themeMode'))
    assert.ok(dependencies.includes('themePreference'))

    const resolveSource = (themePreference, themeMode) =>
      runInNewContext(`(${effect[1]})`, { themeMode, themePreference })

    assert.equal(resolveSource('light', 'light'), 'light')
    assert.equal(resolveSource('dark', 'dark'), 'dark')
    assert.equal(resolveSource('system', 'light'), 'system')
    assert.equal(resolveSource('system', 'dark'), 'system')
  })

  await t.test('theme store restores and persists valid preferences', async () => {
    for (const preference of ['light', 'dark', 'system']) {
      installBrowserGlobals(preference, false)
      const { useThemeStore } = await freshImport(
        '../src/renderer/src/stores/themeStore.ts'
      )
      assert.equal(useThemeStore.getState().themePreference, preference)
    }

    const browser = installBrowserGlobals('light', false)
    const { useThemeStore } = await freshImport(
      '../src/renderer/src/stores/themeStore.ts'
    )
    useThemeStore.getState().setThemePreference('dark')
    assert.equal(browser.values.get('udt.theme'), 'dark')
    useThemeStore.getState().setThemePreference('system')
    assert.equal(browser.values.get('udt.theme'), 'system')
  })

  await t.test('theme store restores and persists the Focus style preset', async () => {
    const browser = installBrowserGlobals('light', false)
    browser.values.set('udt.theme-style', 'focus')
    const { useThemeStore } = await freshImport(
      '../src/renderer/src/stores/themeStore.ts'
    )
    assert.equal(useThemeStore.getState().stylePreference, 'focus')

    useThemeStore.getState().setStylePreference('default')
    assert.equal(browser.values.get('udt.theme-style'), 'default')
  })

  await t.test('theme store rejects invalid or unavailable persisted preferences', async () => {
    for (const preference of ['sepia', '', null]) {
      installBrowserGlobals(preference, true)
      const { useThemeStore } = await freshImport(
        '../src/renderer/src/stores/themeStore.ts'
      )
      assert.equal(useThemeStore.getState().themePreference, 'system')
    }

    installBrowserGlobals('dark', false, true)
    const { useThemeStore } = await freshImport(
      '../src/renderer/src/stores/themeStore.ts'
    )
    assert.equal(useThemeStore.getState().themePreference, 'system')
  })

  await t.test('theme store restores Auto and locked UI scale preferences', async () => {
    const { computeAutoUiScale, layoutWidthChanged, readLayoutWidth } = await freshImport(
      '../src/renderer/src/theme/uiScale.ts'
    )
    assert.equal(computeAutoUiScale(1024), 1.1)
    assert.equal(computeAutoUiScale(1058), 1.11)
    assert.equal(computeAutoUiScale(1300), 1.18)
    assert.equal(computeAutoUiScale(1366), 1.2)
    assert.equal(computeAutoUiScale(1430), 1.22)
    assert.equal(computeAutoUiScale(1600), 1.27)
    assert.equal(computeAutoUiScale(1625), 1.27)
    assert.equal(computeAutoUiScale(1920), 1.36)
    assert.equal(computeAutoUiScale(900), 1.1)
    assert.equal(computeAutoUiScale(2000), 1.36)
    assert.equal(computeAutoUiScale(0), 1.1)

    assert.equal(layoutWidthChanged(1600, 1600), false)
    assert.equal(layoutWidthChanged(1600, 1604), false)
    assert.equal(layoutWidthChanged(1600, 1610), true)

    installBrowserGlobals(null, false)
    globalThis.window.outerWidth = 1024
    globalThis.window.innerWidth = 1920
    assert.equal(readLayoutWidth(), 1024)
    assert.equal(computeAutoUiScale(readLayoutWidth()), 1.1)

    installBrowserGlobals(null, false)
    globalThis.window.outerWidth = 0
    globalThis.window.innerWidth = 1920
    assert.equal(readLayoutWidth(), 1024)
    assert.equal(computeAutoUiScale(readLayoutWidth()), 1.1)

    installBrowserGlobals(null, false)
    globalThis.localStorage.setItem('udt-ui-scale', 'auto')
    globalThis.window.outerWidth = 1024
    globalThis.window.innerWidth = 1920
    const autoStore = await freshImport('../src/renderer/src/stores/themeStore.ts')
    assert.equal(autoStore.useThemeStore.getState().uiScalePreference, 'auto')
    assert.equal(autoStore.useThemeStore.getState().uiScale, 1.1)

    installBrowserGlobals(null, false)
    globalThis.localStorage.setItem('udt-ui-scale', '1.25')
    const lockedStore = await freshImport('../src/renderer/src/stores/themeStore.ts')
    assert.equal(lockedStore.useThemeStore.getState().uiScalePreference, 1.25)
    assert.equal(lockedStore.useThemeStore.getState().uiScale, 1.25)
  })

  await t.test('Auto scale ignores zoom-only resize feedback', async () => {
    const { useTheme } = await freshImport('../src/renderer/src/theme/useTheme.ts')
    const runtime = createHookRuntime('dark', false)
    runtime.store.uiScalePreference = 'auto'
    runtime.store.uiScale = 1.1
    const applied = []
    runtime.store.applyComputedUiScale = (uiScale) => {
      applied.push(uiScale)
      runtime.store.uiScale = uiScale
    }

    globalThis.window.outerWidth = 1024
    globalThis.window.innerWidth = 1024
    const listeners = new Map()
    const previousMatchMedia = globalThis.window.matchMedia
    globalThis.window.addEventListener = (type, listener) => {
      listeners.set(type, listener)
    }
    globalThis.window.removeEventListener = (type) => {
      listeners.delete(type)
    }
    globalThis.window.matchMedia = previousMatchMedia

    runtime.render(useTheme)
    assert.deepEqual(applied, [1.1])

    globalThis.window.innerWidth = 750
    listeners.get('resize')?.()
    await new Promise((resolve) => setTimeout(resolve, 150))
    assert.deepEqual(applied, [1.1])

    globalThis.window.outerWidth = 1600
    globalThis.window.innerWidth = 1185
    listeners.get('resize')?.()
    await new Promise((resolve) => setTimeout(resolve, 150))
    assert.deepEqual(applied, [1.1, 1.27])

    runtime.cleanup()
  })

  await t.test('accent surface palette does not retint control strokes', () => {
    const source = readFileSync(
      new URL('../src/renderer/src/theme/accentPalette.ts', import.meta.url),
      'utf8'
    )
    assert.match(source, /slot: 'controlFillDefault'/)
    assert.doesNotMatch(source, /variable: '--udt-control-stroke-default'/)
    assert.doesNotMatch(source, /variable: '--udt-control-stroke-secondary'/)
  })

  await t.test('global theme tokens define light strokes and reduced-motion coverage', () => {
    const globalCss = readFileSync(
      new URL('../src/renderer/src/styles/global.css', import.meta.url),
      'utf8'
    )
    const skeletonCss = readFileSync(
      new URL('../src/renderer/src/styles/skeleton.css', import.meta.url),
      'utf8'
    )

    const lightBlock = extractCssBlock(globalCss, ":root[data-theme='light']")
    assert.ok(lightBlock, 'light theme token block must exist')
    assert.match(lightBlock, /--udt-control-stroke-secondary:\s*rgba\(0,\s*0,\s*0/)
    assert.match(lightBlock, /--udt-control-stroke-strong:\s*rgba\(0,\s*0,\s*0/)
    assert.match(lightBlock, /--udt-subtle-fill-tertiary:\s*rgba\(0,\s*0,\s*0/)
    assert.match(globalCss, /--udt-control-stroke-strong:\s*rgba\(255,\s*255,\s*255/)

    const reducedCss = extractMediaBlocks(globalCss, 'prefers-reduced-motion: reduce').join('\n')
    assert.match(reducedCss, /\.udt-macro-page \.udt-macro-list > \*/)
    assert.match(reducedCss, /\.udt-modal__list > \*/)
    assert.match(reducedCss, /\.udt-card:hover/)
    assert.match(reducedCss, /\.udt-btn:not\(:disabled\):active/)
    assert.match(reducedCss, /\.udt-nav/)
    assert.match(skeletonCss, /@media\s*\(prefers-reduced-motion:\s*reduce\)/)
    assert.match(skeletonCss, /\.udt-skeleton::after/)
  })

  await t.test('bootstrap honors valid preferences and treats invalid values as System', async () => {
    const { bootstrapThemeDocument } = await freshImport(
      '../src/renderer/src/theme/bootstrapTheme.ts'
    )
    const scenarios = [
      { stored: 'light', systemDark: true, expected: 'light', mediaCalls: 0 },
      { stored: 'dark', systemDark: false, expected: 'dark', mediaCalls: 0 },
      { stored: 'system', systemDark: true, expected: 'dark', mediaCalls: 1 },
      { stored: 'system', systemDark: false, expected: 'light', mediaCalls: 1 },
      { stored: 'invalid', systemDark: true, expected: 'dark', mediaCalls: 1 }
    ]

    for (const scenario of scenarios) {
      const browser = installBrowserGlobals(scenario.stored, scenario.systemDark)
      bootstrapThemeDocument()

      assert.equal(browser.attributes.get('data-theme'), scenario.expected)
      assert.equal(browser.root.style.colorScheme, scenario.expected)
      assert.equal(browser.root.dataset.backdrop, 'none')
      assert.equal(browser.matchMediaCalls(), scenario.mediaCalls)
    }
  })
})
