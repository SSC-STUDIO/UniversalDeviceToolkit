import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'

const compile = source => ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
}).outputText
const source = readFileSync(new URL('../src/main/index.ts', import.meta.url), 'utf8')
const syntax = ts.createSourceFile('index.ts', source, ts.ScriptTarget.Latest, true)
const lifecycle = syntax.statements.filter(node => ts.isFunctionDeclaration(node)
  && ['enterBackground', 'restoreMainWindow'].includes(node.name?.text)).map(node => node.getText(syntax)).join(';')

function windowHarness() {
  const calls = []
  const deferred = []
  const window = {
    isDestroyed: () => false, isVisible: () => true, isMinimized: () => false,
    hide: () => calls.push('hide'), destroy: () => calls.push('destroy'),
    show: () => calls.push('show'), focus: () => calls.push('focus'),
    webContents: { isLoadingMainFrame: () => false }
  }
  const context = vm.createContext({
    mainWindow: window, isQuitting: false, trayOnlySession: false,
    backgroundDestroyGeneration: 0, pendingTrayRoute: null,
    persistMainWindowBounds: () => calls.push('persist'),
    destroyStatusWindow: () => {}, destroyTrayPopup: () => {},
    isOsdVisible: () => true, suspendOsdWindow: () => calls.push('suspend-osd'),
    trimChromiumCaches: () => calls.push('clear-cache'), logMemoryUsage: () => {},
    setSurfaceVisible: () => {}, createWindow: () => calls.push('create'),
    flushPendingTrayNavigation: () => calls.push('navigate'),
    setImmediate: callback => deferred.push(callback), setTimeout: callback => deferred.push(callback)
  })
  vm.runInContext(compile(lifecycle), context)
  return { calls, context, window, flush: () => { for (const callback of deferred.splice(0)) callback() } }
}

test('tray hide retains the renderer and restores the same window without clearing caches', () => {
  const h = windowHarness()
  h.context.enterBackground()
  h.flush()
  assert.ok(h.calls.includes('hide'))
  assert.equal(h.context.mainWindow, h.window)
  assert.equal(h.context.trayOnlySession, true)
  assert.ok(!h.calls.includes('destroy'))
  assert.ok(!h.calls.includes('clear-cache'))
  assert.ok(!h.calls.includes('suspend-osd'), 'visible OSD must remain active')
  h.context.restoreMainWindow('/settings')
  h.flush()
  assert.deepEqual(h.calls.slice(-3), ['show', 'focus', 'navigate'])
  assert.ok(!h.calls.includes('create'))
})

test('quitting does not retain or recreate the main window', () => {
  const h = windowHarness()
  h.context.isQuitting = true
  h.context.enterBackground()
  h.context.restoreMainWindow()
  h.flush()
  assert.deepEqual(h.calls, [])
})

test('background polling pauses only after every surface hides; reuse cancels idle destruction', () => {
  const timers = new Map()
  let nextTimer = 0
  const activity = { exports: {} }
  vm.runInNewContext(compile(readFileSync(new URL('../src/main/ui-activity.ts', import.meta.url), 'utf8')), {
    exports: activity.exports,
    setTimeout: (callback, delay) => { const id = ++nextTimer; timers.set(id, { callback, delay }); return id },
    clearTimeout: id => timers.delete(id)
  })
  const states = []
  const api = activity.exports
  api.setUiActivityHandler(active => states.push(active))
  api.setSurfaceVisible('main', true)
  api.setSurfaceVisible('osd', true)
  api.setSurfaceVisible('main', false)
  assert.equal(api.isUiActive(), true)
  api.setSurfaceVisible('osd', false)
  assert.deepEqual(states, [true, false])
  api.setSurfaceVisible('main', true)
  assert.deepEqual(states, [true, false, true])
  api.scheduleIdleDestroy('popup', () => assert.fail('reused window must not be destroyed'))
  assert.ok([...timers.values()][0].delay >= 60_000, 'ordinary brief hides should keep the cached window')
  api.cancelIdleDestroy('popup')
  assert.equal(timers.size, 0)
})
