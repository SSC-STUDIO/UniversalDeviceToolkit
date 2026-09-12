import assert from 'node:assert/strict'
import test from 'node:test'
import { createMacroApi } from '../src/renderer/src/api/macroClient.ts'

function createInvoker() {
  const calls = []
  const responses = {
    'macro.getState': { isEnabled: true, slots: [] },
    'macro.setEnabled': { ok: true },
    'macro.play': { ok: true },
    'macro.startRecording': { ok: true },
    'macro.stopRecording': { events: [] },
    'macro.saveSequence': { ok: true },
    'macro.clearSequence': { ok: true }
  }

  return {
    calls,
    invoke: async (method, params) => {
      calls.push({ method, params })
      return responses[method]
    }
  }
}

test('macro API sends each operation and payload to the matching RPC method', async () => {
  const invoker = createInvoker()
  const api = createMacroApi(invoker.invoke)
  const events = [
    {
      source: 'Keyboard',
      direction: 'Down',
      key: 0x41,
      x: 0,
      y: 0,
      delayMs: 0
    },
    {
      source: 'Keyboard',
      direction: 'Up',
      key: 0x41,
      x: 0,
      y: 0,
      delayMs: 25
    }
  ]
  const sequence = {
    key: 0x63,
    repeatCount: 4,
    ignoreDelays: true,
    interruptOnOtherKey: true,
    events
  }

  assert.deepEqual(await api.getState(), { isEnabled: true, slots: [] })
  await api.setEnabled(false)
  await api.startRecording('KeyboardMouse', 0x63)
  assert.deepEqual(await api.stopRecording(), { events: [] })
  await api.saveSequence(sequence)
  await api.clearSequence(0x63)

  assert.deepEqual(invoker.calls, [
    { method: 'macro.getState', params: {} },
    { method: 'macro.setEnabled', params: { enabled: false } },
    {
      method: 'macro.startRecording',
      params: { mode: 'KeyboardMouse', key: 0x63 }
    },
    { method: 'macro.stopRecording', params: {} },
    { method: 'macro.saveSequence', params: sequence },
    { method: 'macro.clearSequence', params: { key: 0x63 } }
  ])
})

test('macro API forwards every numpad virtual key without remapping it', async () => {
  const invoker = createInvoker()
  const api = createMacroApi(invoker.invoke)

  for (let key = 0x60; key <= 0x69; key += 1) {
    assert.deepEqual(await api.play(key), { ok: true })
  }

  assert.deepEqual(
    invoker.calls.map((call) => call.params.key),
    [0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69]
  )
})

test('macro API preserves bridge failures for the store to surface', async () => {
  const failure = new Error('macro host unavailable')
  const api = createMacroApi(async () => {
    throw failure
  })

  await assert.rejects(api.getState(), (error) => error === failure)
})

test('macro API forwards rejected persist payloads without rewriting them to success', async () => {
  const api = createMacroApi(async (method) => {
    if (method === 'macro.saveSequence' || method === 'macro.clearSequence') {
      return { ok: false }
    }
    return { ok: true }
  })

  const sequence = {
    key: 0x60,
    repeatCount: 1,
    ignoreDelays: false,
    interruptOnOtherKey: false,
    events: [
      {
        source: 'Keyboard',
        direction: 'Down',
        key: 0x41,
        x: 0,
        y: 0,
        delayMs: 0
      }
    ]
  }

  assert.deepEqual(await api.saveSequence(sequence), { ok: false })
  assert.deepEqual(await api.clearSequence(0x60), { ok: false })
  assert.deepEqual(await api.play(0x60), { ok: true })
})
