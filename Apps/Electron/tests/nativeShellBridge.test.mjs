import { readFile } from 'node:fs/promises'
import { strict as assert } from 'node:assert'
import test from 'node:test'
import vm from 'node:vm'

async function createBridge() {
  const source = (await readFile(new URL('../../Windows/Bridge.js', import.meta.url), 'utf8'))
    .replace('__UDT_STARTUP_JSON__', JSON.stringify({ language: 'en', deviceMode: 'auto', features: {} }))
  const messageListeners = new Set()
  const pagehideListeners = new Set()
  const sent = []
  const transport = {
    postMessage(message) { sent.push(message) },
    addEventListener(type, callback) {
      if (type === 'message') messageListeners.add(callback)
    }
  }
  const context = {
    console,
    setTimeout,
    clearTimeout,
    window: {
      chrome: { webview: transport },
      addEventListener(type, callback) {
        if (type === 'pagehide') pagehideListeners.add(callback)
      }
    }
  }
  vm.runInNewContext(source, context, { filename: 'Bridge.js' })
  return {
    bridge: context.window.bridge,
    sent,
    emitMessage(data) { for (const listener of messageListeners) listener({ data }) },
    closePage() { for (const listener of pagehideListeners) listener() }
  }
}

test('native bridge correlates replies and preserves Host error codes', async () => {
  const shell = await createBridge()
  assert.equal(shell.bridge.shellVariant, 'webview2')
  assert.equal(Object.isFrozen(shell.bridge), true)
  const request = shell.bridge.invoke('network.status', { refresh: true })
  assert.equal(JSON.stringify(shell.sent[0]), JSON.stringify({
    id: 1,
    method: 'bridge:invoke',
    params: { method: 'network.status', params: { refresh: true } }
  }))
  shell.emitMessage({ id: 1, result: { connected: true } })
  assert.equal(JSON.stringify(await request), JSON.stringify({ connected: true }))

  const failed = shell.bridge.invoke('network.status')
  shell.emitMessage({ id: 2, error: { code: -32099, message: 'Host unavailable' } })
  await assert.rejects(failed, error => error.message === '[UDT:-32099] Host unavailable')
})

test('native bridge isolates event listeners and removes them on unsubscribe', async () => {
  const shell = await createBridge()
  const received = []
  let failures = 0
  const removeFirst = shell.bridge.on('host.ready', value => received.push(value))
  shell.bridge.on('host.ready', () => { throw new Error('listener failure') })
  shell.bridge.on('host.ready', () => { failures += 1 })
  shell.emitMessage({ event: 'host.ready', data: { version: '6.1.1' } })
  assert.deepEqual(received, [{ version: '6.1.1' }])
  assert.equal(failures, 1)
  removeFirst()
  shell.emitMessage({ event: 'host.ready', data: { version: '6.1.2' } })
  assert.deepEqual(received, [{ version: '6.1.1' }])
  assert.equal(failures, 2)
})

test('native bridge rejects pending work when the page is closed', async () => {
  const shell = await createBridge()
  const request = shell.bridge.getHostStatus()
  shell.closePage()
  await assert.rejects(request, error => error.message === 'The application page was closed.')
})
