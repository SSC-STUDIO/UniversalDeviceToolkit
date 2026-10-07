import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import vm from 'node:vm'
import ts from 'typescript'

const sourceUrl = new URL('../src/renderer/src/app/layout/AppLayout.tsx', import.meta.url)
const createElement = (type, props) => ({ type, props: props ?? {} })

const navigationModule = { exports: {} }
vm.runInNewContext(ts.transpileModule(readFileSync(new URL('../src/shared/navigation-visibility.ts', import.meta.url), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
}).outputText, {
  module: navigationModule, exports: navigationModule.exports,
  require: () => ({ isInstallerOptionalFeatureEnabled: () => true })
})

function createFixture({ direction = 'ltr', loadImpl = async () => undefined, capabilities = null, visibility = {} } = {}) {
  const cells = []
  const effects = []
  const listeners = new Map()
  const storage = new Map()
  const warnings = []
  let cursor = 0
  let initialized = false
  let currentDirection = direction
  const state = { scopes: { application: { NavigationItemsVisibility: visibility } }, refresh: loadImpl }
  const react = {
    useState(initial) {
      const index = cursor++
      if (!initialized) cells[index] = typeof initial === 'function' ? initial() : initial
      return [cells[index], (update) => {
        cells[index] = typeof update === 'function' ? update(cells[index]) : update
      }]
    },
    useRef(initial) {
      const index = cursor++
      if (!initialized) cells[index] = { current: initial }
      return cells[index]
    },
    useCallback: (callback) => callback,
    useMemo: (factory) => factory(),
    useEffect(effect) {
      if (!initialized) effects.push(effect)
    }
  }
  const window = {
    innerWidth: 1300,
    bridge: { platform: 'win32' },
    addEventListener(type, callback) {
      if (!listeners.has(type)) listeners.set(type, new Set())
      listeners.get(type).add(callback)
    },
    removeEventListener(type, callback) { listeners.get(type)?.delete(callback) }
  }
  const Stub = function Stub() {}
  const icons = new Map()
  const module = { exports: {} }
  const imports = {
    react,
    'react-i18next': { useTranslation: () => ({ t: (key) => key, i18n: { dir: () => currentDirection } }) },
    'react-router-dom': {
      useLocation: () => ({ pathname: '/dashboard' }),
      useNavigate: () => () => undefined
    },
    '../../../../shared/installer-selection': { isInstallerOptionalFeatureEnabled: () => true },
    '../../../../shared/navigation-visibility': navigationModule.exports,
    '../../features/dashboard/components/statusDialog': { openStatusModal: async () => undefined },
    '../../shared/bridge/bridge': {
      on: () => () => undefined,
      sanitizeBridgeError: (reason) => reason instanceof Error ? reason.message : String(reason)
    },
    '../../shared/settings/settingsStore': { useSettingsStore: (selector) => selector(state) },
    '../../shared/state/hostCapabilitiesStore': {
      useHostCapabilitiesStore: (selector) => selector({ capabilities: capabilities == null ? null : { capabilities } })
    },
    '../../shared/ui/icons/fluent': new Proxy({}, {
      get: (_target, name) => {
        if (!icons.has(name)) icons.set(name, function Icon() {})
        return icons.get(name)
      }
    }),
    'react/jsx-runtime': { jsx: createElement, jsxs: createElement }
  }
  const output = ts.transpileModule(readFileSync(sourceUrl, 'utf8'), {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
      esModuleInterop: true
    }
  }).outputText
  new vm.Script(output).runInNewContext({
    module,
    exports: module.exports,
    require: (name) => imports[name] ?? { __esModule: true, default: Stub },
    window,
    document: { documentElement: { dir: direction } },
    getComputedStyle: () => ({
      direction: currentDirection,
      getPropertyValue: (name) => name.includes('collapsed') ? '70px' : '220px'
    }),
    localStorage: {
      getItem: (key) => storage.get(key) ?? null,
      setItem: (key, value) => storage.set(key, value)
    },
    console: { warn: (...args) => warnings.push(args) }
  })
  const render = () => {
    cursor = 0
    const root = module.exports.default({ children: null })
    initialized = true
    return root
  }
  const root = render()
  const cleanups = effects.map((effect) => effect()).filter((cleanup) => typeof cleanup === 'function')
  return {
    root, render, warnings, icons,
    setDirection: (nextDirection) => { currentDirection = nextDirection },
    cleanup: () => { for (const cleanup of cleanups) cleanup() },
    listenerCount: (type) => listeners.get(type)?.size ?? 0,
    dispatch(type, event) {
      for (const callback of [...(listeners.get(type) ?? [])]) callback(event)
    }
  }
}

function findElement(node, predicate) {
  if (node == null || typeof node !== 'object') return undefined
  if (predicate(node)) return node
  for (const child of Array.isArray(node) ? node : Object.values(node.props ?? {})) {
    const found = findElement(child, predicate)
    if (found != null) return found
  }
  return undefined
}

test('merged Actions navigation follows child visibility and explicit Host capabilities', () => {
  for (const [visibility, capabilities, expectedVisible] of [
    [{}, null, true],
    [{ automation: false, macro: false }, null, false],
    [{}, { automation: false, macro: false }, false],
    [{ automation: false }, { macro: true }, true],
    [{ automation: false }, { macro: false }, false]
  ]) {
    const fixture = createFixture({ visibility, capabilities })
    const entry = findElement(fixture.render(), (node) => node.props?.item?.key === '/actions')
    assert.equal(entry != null, expectedVisible)
    assert.ok(findElement(fixture.render(), (node) => node.props?.item?.key === '/dashboard'))
    fixture.cleanup()
  }
})

function readWidth(fixture) {
  return findElement(fixture.render(), (node) => node.type === 'nav').props.style.width
}

function startDrag(fixture, overrides = {}) {
  const captures = new Set()
  const releases = []
  const target = {
    setPointerCapture: (pointerId) => captures.add(pointerId),
    hasPointerCapture: (pointerId) => captures.has(pointerId),
    releasePointerCapture(pointerId) {
      captures.delete(pointerId)
      releases.push(pointerId)
    }
  }
  const resizer = findElement(fixture.render(), (node) => node.props?.role === 'separator')
  resizer.props.onPointerDown({
    button: 0, isPrimary: true, clientX: 500, pointerId: 7,
    currentTarget: target, preventDefault: () => undefined, ...overrides
  })
  return { captures, releases }
}

test('navigation drag increases width toward the content in LTR and RTL', () => {
  for (const [direction, clientX] of [['ltr', 560], ['rtl', 440]]) {
    const fixture = createFixture({ direction })
    const drag = startDrag(fixture)
    fixture.dispatch('pointermove', { clientX, pointerId: 7 })
    assert.equal(readWidth(fixture), 280, direction)
    fixture.dispatch('pointerup', { pointerId: 7 })
    assert.equal(drag.captures.size, 0)
    assert.equal(fixture.listenerCount('pointermove'), 0)
    fixture.cleanup()
  }
})

function findToggle(fixture) {
  return findElement(fixture.render(), (node) => node.props?.className?.split(' ').includes('udt-nav-toggle'))
}

test('navigation toggle points toward its action in LTR and RTL', () => {
  for (const direction of ['ltr', 'rtl']) {
    const fixture = createFixture({ direction })
    const collapseIcon = direction === 'rtl' ? 'ChevronRight16Regular' : 'ChevronLeft16Regular'
    const expandIcon = direction === 'rtl' ? 'ChevronLeft16Regular' : 'ChevronRight16Regular'
    assert.equal(findToggle(fixture).props.children.type, fixture.icons.get(collapseIcon), direction)
    findToggle(fixture).props.onClick()
    assert.equal(findToggle(fixture).props.children.type, fixture.icons.get(expandIcon), direction)
    assert.equal(readWidth(fixture), 70, direction)
    findToggle(fixture).props.onClick()
    assert.equal(findToggle(fixture).props.children.type, fixture.icons.get(collapseIcon), direction)
    assert.equal(readWidth(fixture), 220, direction)
    fixture.cleanup()
  }
})

test('navigation toggle follows language direction without changing collapse state', () => {
  const fixture = createFixture()
  assert.equal(findToggle(fixture).props.children.type, fixture.icons.get('ChevronLeft16Regular'))
  fixture.setDirection('rtl')
  assert.equal(findToggle(fixture).props.children.type, fixture.icons.get('ChevronRight16Regular'))
  assert.equal(readWidth(fixture), 220)
  findToggle(fixture).props.onClick()
  assert.equal(findToggle(fixture).props.children.type, fixture.icons.get('ChevronLeft16Regular'))
  fixture.setDirection('ltr')
  assert.equal(findToggle(fixture).props.children.type, fixture.icons.get('ChevronRight16Regular'))
  assert.equal(readWidth(fixture), 70)
  fixture.cleanup()
})

test('navigation unmount releases capture and removes active drag listeners', () => {
  const fixture = createFixture()
  const drag = startDrag(fixture)
  assert.equal(fixture.listenerCount('pointermove'), 1)
  fixture.cleanup()
  assert.equal(fixture.listenerCount('pointermove'), 0)
  assert.equal(fixture.listenerCount('pointerup'), 0)
  assert.equal(fixture.listenerCount('pointercancel'), 0)
  assert.deepEqual(drag.releases, [7])
})

test('an unrelated pointer does not resize or end the active navigation drag', () => {
  const fixture = createFixture()
  startDrag(fixture)
  fixture.dispatch('pointermove', { clientX: 620, pointerId: 8 })
  assert.equal(readWidth(fixture), 220)
  fixture.dispatch('pointerup', { pointerId: 8 })
  assert.equal(fixture.listenerCount('pointermove'), 1)
  fixture.dispatch('pointercancel', { pointerId: 7 })
  assert.equal(fixture.listenerCount('pointermove'), 0)
  fixture.cleanup()
})

test('navigation ignores secondary buttons and nonprimary pointers', () => {
  const fixture = createFixture()
  startDrag(fixture, { button: 2 })
  assert.equal(fixture.listenerCount('pointermove'), 0)
  startDrag(fixture, { isPrimary: false })
  assert.equal(fixture.listenerCount('pointermove'), 0)
  fixture.cleanup()
})

test('starting a fresh navigation drag cleans up the previous capture', () => {
  const fixture = createFixture()
  const first = startDrag(fixture)
  const second = startDrag(fixture, { pointerId: 9 })
  assert.deepEqual(first.releases, [7])
  assert.equal(fixture.listenerCount('pointermove'), 1)
  fixture.dispatch('pointerup', { pointerId: 9 })
  assert.deepEqual(second.releases, [9])
  fixture.cleanup()
})
