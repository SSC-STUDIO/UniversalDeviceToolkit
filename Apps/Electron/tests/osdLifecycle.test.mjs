import assert from 'node:assert/strict'
import { EventEmitter } from 'node:events'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { setImmediate } from 'node:timers/promises'
import test from 'node:test'
import vm from 'node:vm'
import ts from 'typescript'

const require = createRequire(import.meta.url)
const compile = source => ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
}).outputText
const source = compile(readFileSync(new URL('../src/main/osd-window.ts', import.meta.url), 'utf8'))
const presentationModule = { exports: {} }
vm.runInNewContext(compile(readFileSync(new URL('../src/shared/osd-presentation.ts', import.meta.url), 'utf8')), {
  module: presentationModule, exports: presentationModule.exports
})

function deferred() {
  let resolve
  const promise = new Promise(done => { resolve = done })
  return { promise, resolve }
}

async function flush() {
  await setImmediate()
  await setImmediate()
}

async function harness(initial = {}) {
  const events = new EventEmitter()
  const screen = new EventEmitter()
  const powerMonitor = new EventEmitter()
  const windows = []
  const calls = []
  const errors = []
  const pendingLoads = []
  const pendingSubscriptions = []
  const activeSensors = new Map()
  let activeFps = 0
  let hostEpoch = 0
  let persisted = { ShowOsd: false, Items: ['CpuTemperature'], OsdRefreshInterval: 1, ...initial }
  const display = { workArea: { x: 0, y: 0, width: 1920, height: 1080 } }
  screen.getAllDisplays = () => [display]
  screen.getPrimaryDisplay = () => display
  screen.getDisplayMatching = () => display
  class Window extends EventEmitter {
    constructor(options) {
      super()
      this.destroyed = false
      this.visible = options.show
      this.size = [options.width, options.height]
      this.position = [0, 0]
      this.showCount = 0
      this.renderCount = 0
      this.webContents = { executeJavaScript: async expression => {
        if (expression.startsWith('globalThis.udtRender')) this.renderCount++
        return [320, 96]
      } }
      windows.push(this)
    }
    isDestroyed() { return this.destroyed }
    isVisible() { return this.visible }
    getSize() { return this.size }
    setSize(width, height) { this.size = [width, height] }
    getPosition() { return this.position }
    setPosition(x, y) { this.position = [x, y] }
    setIgnoreMouseEvents() {}
    async loadURL() { await pendingLoads.shift()?.promise }
    show() { this.visible = true; this.showCount++ }
    hide() { this.visible = false }
    destroy() { this.destroyed = true; this.visible = false; this.emit('closed') }
  }
  const hostClient = {
    on: (event, listener) => {
      events.on(event, listener)
      return () => events.removeListener(event, listener)
    },
    invoke: async (method, params) => {
      calls.push({ method, params })
      const epoch = hostEpoch
      if (method === 'settings.get') return { value: params.scope === 'osd' ? { ...persisted } : {} }
      if (method === 'settings.set') { persisted = { ...params.value }; return }
      if (method === 'sensors.subscribe') {
        await pendingSubscriptions.shift()?.promise
        if (epoch === hostEpoch) activeSensors.set(params.subscriberId, params.intervalSec)
      }
      if (method === 'sensors.unsubscribe') activeSensors.delete(params.subscriberId)
      if (method === 'sensors.subscribeFps') activeFps++
      if (method === 'sensors.unsubscribeFps') activeFps--
      if (method === 'sensors.getSnapshot') return { cpu: { usage: 1 } }
    }
  }
  const module = { exports: {} }
  vm.runInNewContext(source, {
    module, exports: module.exports, process, setTimeout, clearTimeout,
    console: { error: (...args) => errors.push(args) },
    require: name => {
      if (name === '../shared/osd-presentation') return presentationModule.exports
      if (name === 'electron') return {
        BrowserWindow: Window, powerMonitor, screen,
        globalShortcut: { isRegistered: () => false, register: () => true, unregister: () => {} }
      }
      if (name === './host-client') return { hostClient }
      if (name === './ui-scale') return { effectiveZoom: () => 1 }
      if (name === './ui-activity') return {
        cancelIdleDestroy() {}, scheduleIdleDestroy() {}, setSurfaceVisible() {}
      }
      return require(name)
    }
  })
  module.exports.initOsdWindow()
  await flush()
  return {
    api: module.exports, calls, errors, windows, events, powerMonitor,
    activeSensors, activeFps: () => activeFps,
    async show() { events.emit('osd.changed', { state: 'Show' }); await flush() },
    async hide() { events.emit('osd.changed', { state: 'Hidden' }); await flush() },
    async change(value) {
      persisted = { ...persisted, ...value }
      events.emit('settings.changed', { scope: 'osd' })
      await flush()
    },
    async restart() {
      hostEpoch++
      activeSensors.clear()
      activeFps = 0
      events.emit('host.ready')
      await flush()
    },
    deferLoad() { const pending = deferred(); pendingLoads.push(pending); return pending },
    deferSubscription() { const pending = deferred(); pendingSubscriptions.push(pending); return pending }
  }
}

test('visible OSD synchronizes FPS item additions, removals and sensor interval changes', async () => {
  const h = await harness()
  await h.show()
  assert.equal(h.activeSensors.get('osd'), 1)
  assert.equal(h.activeFps(), 0)
  await h.change({ Items: ['CpuTemperature', 'Fps'], OsdRefreshInterval: 2 })
  assert.equal(h.activeSensors.get('osd'), 2)
  assert.equal(h.activeFps(), 1)
  assert.equal(h.events.listenerCount('sensors.fpsUpdated'), 1)
  await h.change({ Items: ['CpuTemperature', 'LowFps'] })
  assert.equal(h.activeFps(), 1)
  await h.change({ Items: ['CpuTemperature'] })
  assert.equal(h.activeFps(), 0)
  assert.equal(h.events.listenerCount('sensors.fpsUpdated'), 0)
  h.api.destroyOsdWindow()
  await flush()
  assert.equal(h.activeSensors.size, 0)
  assert.deepEqual(h.errors, [])
})

test('Host restart restores visible OSD subscriptions once and releases them when hidden', async () => {
  const h = await harness({ Items: ['Fps'] })
  await h.show()
  await h.restart()
  assert.equal(h.activeSensors.get('osd'), 1)
  assert.equal(h.activeFps(), 1)
  assert.equal(h.events.listenerCount('sensors.updated'), 1)
  assert.equal(h.events.listenerCount('sensors.fpsUpdated'), 1)
  const window = h.windows[0]
  const rendered = window.renderCount
  h.events.emit('sensors.updated', { cpu: { usage: 42 } })
  h.events.emit('sensors.fpsUpdated', { fps: 60 })
  await flush()
  assert.equal(window.renderCount, rendered + 2)
  await h.hide()
  assert.equal(h.activeSensors.size, 0)
  assert.equal(h.activeFps(), 0)
  await h.restart()
  assert.equal(h.activeSensors.size, 0)
  assert.equal(h.activeFps(), 0)
  h.api.destroyOsdWindow()
  assert.equal(h.events.listenerCount('host.ready'), 0)
  assert.deepEqual(h.errors, [])
})

test('hide cancels an unfinished OSD page load without reviving or persisting show', async () => {
  const h = await harness({ Items: ['Fps'] })
  const load = h.deferLoad()
  await h.show()
  assert.equal(h.api.isOsdVisible(), false)
  await h.hide()
  load.resolve()
  await flush()
  assert.equal(h.windows[0].showCount, 0)
  assert.equal(h.api.isOsdVisible(), false)
  assert.equal(h.activeSensors.size, 0)
  assert.equal(h.activeFps(), 0)
  assert.ok(h.calls.filter(call => call.method === 'settings.set').every(call => call.params.value.ShowOsd === false))
  await h.show()
  assert.equal(h.windows[0].showCount, 1)
  h.api.destroyOsdWindow()
  await flush()
  assert.deepEqual(h.errors, [])
})

test('toggle and settings hide also cancel pending initial display', async () => {
  for (const hide of [h => h.api.toggleOsd(), h => h.change({ ShowOsd: false })]) {
    const h = await harness()
    const load = h.deferLoad()
    h.api.toggleOsd()
    await flush()
    await hide(h)
    load.resolve()
    await flush()
    assert.equal(h.windows[0].showCount, 0)
    assert.equal(h.api.isOsdVisible(), false)
    h.api.destroyOsdWindow()
  }
})

test('hide during a pending sensor subscribe releases the eventual Host subscription', async () => {
  const h = await harness({ Items: ['Fps'] })
  const subscribe = h.deferSubscription()
  await h.show()
  await h.hide()
  subscribe.resolve()
  await flush()
  assert.equal(h.activeSensors.size, 0)
  assert.equal(h.activeFps(), 0)
  assert.equal(h.events.listenerCount('sensors.updated'), 0)
  await h.show()
  assert.equal(h.activeSensors.get('osd'), 1)
  assert.equal(h.events.listenerCount('sensors.updated'), 1)
  assert.equal(h.activeFps(), 1)
  h.api.destroyOsdWindow()
  await flush()
  assert.deepEqual(h.errors, [])
})
