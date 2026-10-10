import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { setImmediate } from 'node:timers/promises'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'

const require = createRequire(import.meta.url)
const storeSource = readFileSync(new URL('../src/renderer/src/features/dashboard/stores/sensorsStore.ts', import.meta.url), 'utf8')
const sectionSource = readFileSync(new URL('../src/renderer/src/features/dashboard/components/SensorSection.tsx', import.meta.url), 'utf8')
const syntax = ts.createSourceFile('SensorSection.tsx', sectionSource, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX)
let pollingEffect
function findPollingEffect(node) {
  if (ts.isCallExpression(node) && node.expression.getText(syntax) === 'useEffect' && node.arguments[0]?.getText(syntax).includes('await store.start(')) pollingEffect = node.arguments[0].getText(syntax)
  ts.forEachChild(node, findPollingEffect)
}
findPollingEffect(syntax)
assert.ok(pollingEffect, 'SensorSection polling effect must exist')
const compile = source => ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText

function harness(options = {}) {
  let releaseSubscribe
  const firstSubscribe = new Promise(resolve => { releaseSubscribe = resolve })
  const listeners = new Set()
  const phases = []
  const loadErrors = []
  const warnings = []
  const reads = { settings: 0, status: 0, snapshot: 0 }
  const retryRef = { current: null }
  let subscriptions = 0
  const sensorsApi = {
    subscribe: async () => { if (++subscriptions === 1) await firstSubscribe; return { subscribed: true } },
    unsubscribe: async () => {},
    onUpdated: listener => { listeners.add(listener); return () => listeners.delete(listener) },
    onFpsUpdated: () => () => {},
    getStatus: async () => { reads.status++; return {} },
    getSnapshot: async () => {
      reads.snapshot++
      return options.getSnapshot?.() ?? { ts: '2026-09-12T00:00:00Z', cpu: { usage: 1 } }
    }
  }
  const module = { exports: {} }
  vm.runInNewContext(compile(storeSource), { exports: module.exports, module, require: name => name === 'zustand' ? require('zustand') : { sensorsApi } })
  const store = module.exports.useSensorsStore
  const visibility = new Set()
  const mount = () => vm.runInNewContext(compile(`const effect = ${pollingEffect}; effect`), {
    useSensorsStore: store,
    Error,
    logger: { warn: (...args) => warnings.push(args) },
    useSettingsStore: { getState: () => ({ load: async () => { reads.settings++; await options.loadSettings?.() }, scopes: {} }) },
    savedIntervalRef: { current: 1 }, retryRef, readSavedRefreshInterval: () => 1,
    setRequestedPhase: phase => phases.push(phase), setLoadError: error => loadErrors.push(error),
    subscribeUiVisibility: listener => { visibility.add(listener); listener(options.visible !== false); return () => visibility.delete(listener) }
  })()
  return {
    store, mount, releaseSubscribe, phases, loadErrors, warnings, reads, retryRef,
    setVisible: active => { for (const listener of visibility) listener(active) },
    emit: usage => { for (const listener of listeners) listener({ ts: '2026-09-12T00:00:01Z', cpu: { usage } }) },
    listenerCount: () => listeners.size
  }
}
async function flush() { await setImmediate(); await setImmediate() }

test('Strict Mode remount keeps the new subscription after the old start resolves', async () => {
  const h = harness()
  const unmountFirst = h.mount()
  await flush()
  unmountFirst()
  const unmountSecond = h.mount()
  h.releaseSubscribe()
  await flush()
  assert.equal(h.store.getState().subscribed, true)
  h.emit(42)
  assert.equal(h.store.getState().snapshot.cpu.usage, 42)
  assert.equal(h.listenerCount(), 1)
  unmountSecond()
  await flush()
  assert.equal(h.store.getState().subscribed, false)
  assert.equal(h.listenerCount(), 0)
})

test('hide and restore during subscription continues receiving live sensor frames', async () => {
  const h = harness()
  const unmount = h.mount()
  await flush()
  h.setVisible(false)
  h.setVisible(true)
  h.releaseSubscribe()
  await flush()
  assert.equal(h.store.getState().subscribed, true)
  h.emit(13)
  h.emit(27)
  assert.equal(h.store.getState().snapshot.cpu.usage, 27)
  assert.equal(h.store.getState().trend.cpuUsage.at(-1), 27)
  h.setVisible(false)
  await flush()
  assert.equal(h.store.getState().subscribed, false)
  h.emit(99)
  assert.equal(h.store.getState().snapshot.cpu.usage, 27)
  unmount()
  await flush()
})

test('settings load rejection is handled while the first sensor snapshot still loads', async () => {
  const h = harness({ loadSettings: async () => { throw new Error('Settings unavailable') } })
  const unmount = h.mount()
  await flush()
  assert.equal(h.reads.status, 1)
  assert.equal(h.reads.snapshot, 1)
  assert.equal(h.store.getState().snapshot.cpu.usage, 1)
  assert.equal(h.phases.at(-1), 'ready')
  assert.equal(h.warnings.length, 1)
  h.releaseSubscribe()
  await flush()
  unmount()
  await flush()
  assert.equal(h.listenerCount(), 0)
})

test('failed first snapshot shows its error and existing retry reloads settings and sensor data', async () => {
  let recover = false
  const h = harness({
    visible: false,
    loadSettings: async () => { if (!recover) throw new Error('Settings unavailable') },
    getSnapshot: async () => {
      if (!recover) throw new Error('Sensor service unavailable')
      return { ts: '2026-09-12T00:00:00Z', cpu: { usage: 77 } }
    }
  })
  const unmount = h.mount()
  await flush()
  assert.equal(h.phases.at(-1), 'error')
  assert.equal(h.loadErrors.at(-1), 'Sensor service unavailable')
  assert.equal(h.reads.snapshot, 1)
  assert.equal(typeof h.retryRef.current, 'function')
  recover = true
  h.retryRef.current()
  await flush()
  assert.equal(h.reads.settings, 2)
  assert.equal(h.reads.snapshot, 2)
  assert.equal(h.phases.at(-1), 'ready')
  assert.equal(h.loadErrors.at(-1), null)
  assert.equal(h.store.getState().snapshot.cpu.usage, 77)
  unmount()
  await flush()
})

test('unmount before settings rejection prevents late snapshot reads and view state updates', async () => {
  let rejectSettings
  const settings = new Promise((_, reject) => { rejectSettings = reject })
  const h = harness({ visible: false, loadSettings: () => settings })
  const unmount = h.mount()
  unmount()
  rejectSettings(new Error('Settings unavailable'))
  await flush()
  assert.equal(h.reads.settings, 1)
  assert.equal(h.reads.status, 0)
  assert.equal(h.reads.snapshot, 0)
  assert.deepEqual(h.phases, [])
  assert.deepEqual(h.loadErrors, [])
  assert.equal(h.retryRef.current, null)
  assert.equal(h.warnings.length, 0)
  assert.equal(h.listenerCount(), 0)
})
