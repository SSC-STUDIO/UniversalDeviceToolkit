import assert from 'node:assert/strict'
import test from 'node:test'
import {
  appendCapturedEvents,
  createMacroEditorDraft,
  hasMacroEvents,
  isNumpadVirtualKey,
  macroVirtualKeyName,
  numpadDigitToVirtualKey,
  numpadVirtualKeyToDigit
} from '../src/renderer/src/components/macro/macroHelpers.ts'

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

test('numpad virtual keys 0x60 through 0x69 map exactly to digits 0 through 9', () => {
  for (let digit = 0; digit <= 9; digit += 1) {
    const key = 0x60 + digit
    assert.equal(isNumpadVirtualKey(key), true)
    assert.equal(numpadVirtualKeyToDigit(key), digit)
    assert.equal(numpadDigitToVirtualKey(digit), key)
    assert.equal(macroVirtualKeyName(key), `NumPad ${digit}`)
  }
})

test('numpad mapping safely rejects invalid and out-of-range values', () => {
  for (const key of [0x5f, 0x6a, -1, 1.5, Number.NaN, Number.POSITIVE_INFINITY]) {
    assert.equal(isNumpadVirtualKey(key), false)
    assert.equal(numpadVirtualKeyToDigit(key), null)
  }
  for (const digit of [-1, 10, 2.5, Number.NaN, Number.POSITIVE_INFINITY]) {
    assert.equal(numpadDigitToVirtualKey(digit), null)
  }

  assert.equal(macroVirtualKeyName(0x5f), 'Key 95')
  assert.equal(macroVirtualKeyName(0x6a), 'Key 106')
  assert.throws(() => createMacroEditorDraft(0x6a, []), {
    name: 'RangeError',
    message: 'Unsupported macro key: 106'
  })
})

test('selecting a slot creates an isolated draft with all persisted settings and events', () => {
  const slots = [
    {
      key: 0x60,
      source: 'Keyboard',
      repeatCount: 2,
      ignoreDelays: false,
      interruptOnOtherKey: false,
      events: [keyboardEvent(0x41)]
    },
    {
      key: 0x61,
      source: 'Keyboard',
      repeatCount: 7,
      ignoreDelays: true,
      interruptOnOtherKey: true,
      events: [keyboardEvent(0x42), keyboardEvent(0x42, 'Up', 30)]
    }
  ]

  const firstDraft = createMacroEditorDraft(0x60, slots)
  firstDraft.repeatCount = 9
  firstDraft.ignoreDelays = true
  firstDraft.events.push(keyboardEvent(0x43))

  const secondDraft = createMacroEditorDraft(0x61, slots)
  assert.deepEqual(secondDraft, {
    key: 0x61,
    repeatCount: 7,
    ignoreDelays: true,
    interruptOnOtherKey: true,
    events: [keyboardEvent(0x42), keyboardEvent(0x42, 'Up', 30)]
  })
  assert.notStrictEqual(secondDraft.events, slots[1].events)
  assert.notStrictEqual(secondDraft.events[0], slots[1].events[0])

  const emptyDraft = createMacroEditorDraft(0x62, slots)
  assert.deepEqual(emptyDraft, {
    key: 0x62,
    repeatCount: 1,
    ignoreDelays: false,
    interruptOnOtherKey: false,
    events: []
  })
})

test('captured events append only after a completed recording and empty sequences stay empty', () => {
  const saved = [keyboardEvent(0x41), keyboardEvent(0x41, 'Up', 20)]
  const captured = [keyboardEvent(0x42, 'Down', 35), keyboardEvent(0x42, 'Up', 10)]

  assert.deepEqual(appendCapturedEvents(saved, captured, false), [...saved, ...captured])
  assert.deepEqual(appendCapturedEvents(saved, captured, true), saved)
  assert.deepEqual(appendCapturedEvents(saved, [], false), saved)
  assert.equal(hasMacroEvents([]), false)
  assert.equal(hasMacroEvents(saved), true)
})
