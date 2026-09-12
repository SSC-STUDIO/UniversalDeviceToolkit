import assert from 'node:assert/strict'
import test from 'node:test'
import { createMacroStore } from '../src/renderer/src/stores/macroStoreCore.ts'

function clone(value) {
  return JSON.parse(JSON.stringify(value))
}

function keyboardEvent(key, direction = 'Down', delayMs = 0) {
  return {
    source: 'Keyboard',
    direction,
    key,
    x: 0,
    y: 0,
    delayMs
  }
}

function createBackend(initialState) {
  let state = clone(initialState)
  const calls = []
  const recordedEvents = [keyboardEvent(0x43), keyboardEvent(0x43, 'Up', 15)]

  const api = {
    async getState() {
      calls.push({ method: 'getState' })
      return clone(state)
    },
    async setEnabled(enabled) {
      calls.push({ method: 'setEnabled', enabled })
      state = { ...state, isEnabled: enabled }
      return { ok: true }
    },
    async play(key) {
      calls.push({ method: 'play', key })
      return {
        ok: state.slots.some((slot) => slot.key === key && slot.events.length > 0)
      }
    },
    async startRecording(mode, key) {
      calls.push({ method: 'startRecording', mode, key })
      return { ok: true }
    },
    async stopRecording() {
      calls.push({ method: 'stopRecording' })
      return { events: clone(recordedEvents) }
    },
    async saveSequence(params) {
      calls.push({ method: 'saveSequence', params: clone(params) })
      const slot = { source: 'Keyboard', ...clone(params) }
      state = {
        ...state,
        slots: [...state.slots.filter((candidate) => candidate.key !== params.key), slot]
      }
      return { ok: true }
    },
    async clearSequence(key) {
      calls.push({ method: 'clearSequence', key })
      state = {
        ...state,
        slots: state.slots.filter((candidate) => candidate.key !== key)
      }
      return { ok: true }
    }
  }

  return { api, calls }
}

const savedSlot = {
  key: 0x60,
  source: 'Keyboard',
  repeatCount: 6,
  ignoreDelays: true,
  interruptOnOtherKey: true,
  events: [keyboardEvent(0x41), keyboardEvent(0x41, 'Up', 40)]
}

test('macro store loads every slot field and disabling preserves explicit preview', async () => {
  const backend = createBackend({ isEnabled: true, slots: [savedSlot] })
  const store = createMacroStore(backend.api)

  await store.getState().load()
  assert.equal(store.getState().loaded, true)
  assert.equal(store.getState().loading, false)
  assert.equal(store.getState().error, null)
  assert.deepEqual(store.getState().state, {
    isEnabled: true,
    slots: [savedSlot]
  })

  assert.equal(await store.getState().setEnabled(false), true)
  assert.equal(store.getState().state.isEnabled, false)
  assert.deepEqual(store.getState().state.slots, [savedSlot])
  assert.deepEqual(backend.calls.at(-1), { method: 'setEnabled', enabled: false })

  assert.equal(await store.getState().play(0x60), true)
  assert.deepEqual(backend.calls.at(-1), { method: 'play', key: 0x60 })
})

test('saving then clearing a slot round-trips settings and events through reloads', async () => {
  const otherSlot = {
    key: 0x69,
    source: 'Keyboard',
    repeatCount: 1,
    ignoreDelays: false,
    interruptOnOtherKey: false,
    events: [keyboardEvent(0x44), keyboardEvent(0x44, 'Up', 10)]
  }
  const backend = createBackend({ isEnabled: true, slots: [savedSlot, otherSlot] })
  const store = createMacroStore(backend.api)
  await store.getState().load()

  const updated = {
    key: 0x60,
    repeatCount: 9,
    ignoreDelays: false,
    interruptOnOtherKey: true,
    events: [
      keyboardEvent(0x42),
      keyboardEvent(0x42, 'Up', 75),
      {
        source: 'Mouse',
        direction: 'Wheel',
        key: 120,
        x: 10,
        y: 20,
        delayMs: 12
      }
    ]
  }

  assert.equal(await store.getState().saveSequence(updated), true)
  assert.equal(store.getState().error, null)
  assert.deepEqual(
    store.getState().state.slots.find((slot) => slot.key === 0x60),
    { source: 'Keyboard', ...updated }
  )
  assert.deepEqual(
    backend.calls.find((call) => call.method === 'saveSequence'),
    { method: 'saveSequence', params: updated }
  )

  assert.equal(await store.getState().clearSequence(0x60), true)
  assert.equal(store.getState().state.slots.some((slot) => slot.key === 0x60), false)
  assert.deepEqual(store.getState().state.slots, [otherSlot])
})

test('saving an empty sequence clears the slot instead of persisting an empty record', async () => {
  const backend = createBackend({ isEnabled: true, slots: [savedSlot] })
  const store = createMacroStore(backend.api)
  await store.getState().load()

  assert.equal(
    await store.getState().saveSequence({
      key: 0x60,
      repeatCount: 10,
      ignoreDelays: true,
      interruptOnOtherKey: true,
      events: []
    }),
    true
  )
  assert.equal(backend.calls.some((call) => call.method === 'saveSequence'), false)
  assert.equal(backend.calls.some((call) => call.method === 'clearSequence'), true)
  assert.deepEqual(store.getState().state.slots, [])
})

test('recording operations preserve event payloads returned by the API', async () => {
  const backend = createBackend({ isEnabled: false, slots: [] })
  const store = createMacroStore(backend.api)

  assert.equal(await store.getState().startRecording('KeyboardMouseMovement', 0x65), true)
  assert.deepEqual(await store.getState().stopRecording(), [
    keyboardEvent(0x43),
    keyboardEvent(0x43, 'Up', 15)
  ])
})

test('rejected and thrown API failures set an actionable store error', async () => {
  const backend = createBackend({ isEnabled: false, slots: [] })
  const store = createMacroStore(backend.api)
  backend.api.play = async () => ({ ok: false })

  assert.equal(await store.getState().play(0x60), false)
  assert.equal(store.getState().error, 'Macro playback was rejected.')

  backend.api.clearSequence = async () => {
    throw new Error('settings file is locked')
  }
  assert.equal(await store.getState().clearSequence(0x60), false)
  assert.equal(store.getState().error, 'settings file is locked')

  backend.api.getState = async () => {
    throw 'host unavailable'
  }
  await store.getState().load()
  assert.equal(store.getState().error, 'host unavailable')
  assert.equal(store.getState().loading, false)
})

test('rejected persist results do not clear the slot or report success', async () => {
  const backend = createBackend({ isEnabled: true, slots: [savedSlot] })
  const store = createMacroStore(backend.api)

  let reloads = 0
  const originalGetState = backend.api.getState
  backend.api.getState = async () => {
    reloads += 1
    return originalGetState()
  }
  await store.getState().load()
  assert.equal(reloads, 1)

  backend.api.saveSequence = async (params) => {
    backend.calls.push({ method: 'saveSequence', params: clone(params) })
    return { ok: false }
  }

  assert.equal(
    await store.getState().saveSequence({
      key: 0x60,
      repeatCount: 2,
      ignoreDelays: false,
      interruptOnOtherKey: false,
      events: [keyboardEvent(0x42), keyboardEvent(0x42, 'Up', 20)]
    }),
    false
  )
  assert.equal(store.getState().error, 'Macro sequence save was rejected.')
  assert.deepEqual(store.getState().state.slots, [savedSlot])
  assert.equal(reloads, 1)

  backend.api.clearSequence = async (key) => {
    backend.calls.push({ method: 'clearSequence', key })
    return { ok: false }
  }

  assert.equal(await store.getState().clearSequence(0x60), false)
  assert.equal(store.getState().error, 'Macro sequence clear was rejected.')
  assert.deepEqual(store.getState().state.slots, [savedSlot])
  assert.equal(reloads, 1)

  assert.equal(
    await store.getState().saveSequence({
      key: 0x60,
      repeatCount: 10,
      ignoreDelays: true,
      interruptOnOtherKey: true,
      events: []
    }),
    false
  )
  assert.equal(store.getState().error, 'Macro sequence clear was rejected.')
  assert.deepEqual(store.getState().state.slots, [savedSlot])
  assert.equal(reloads, 1)
})
