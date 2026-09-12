import assert from 'node:assert/strict'
import test from 'node:test'
import { MacroRecorderController } from '../src/renderer/src/hooks/macroRecorderCore.ts'

class FakeEventTarget {
  listeners = new Map()

  addEventListener(type, listener) {
    const listeners = this.listeners.get(type) ?? new Set()
    listeners.add(listener)
    this.listeners.set(type, listeners)
  }

  removeEventListener(type, listener) {
    const listeners = this.listeners.get(type)
    listeners?.delete(listener)
    if (listeners?.size === 0) this.listeners.delete(type)
  }

  emit(type, event) {
    for (const listener of [...(this.listeners.get(type) ?? [])]) {
      listener(event)
    }
  }

  listenerCount() {
    return [...this.listeners.values()].reduce((total, listeners) => total + listeners.size, 0)
  }
}

function createHarness() {
  const target = new FakeEventTarget()
  const timers = new Map()
  const states = []
  const completions = []
  let nextTimer = 1
  let now = 0

  const controller = new MacroRecorderController(
    {
      target,
      now: () => now,
      setTimer: (callback, delayMs) => {
        const id = nextTimer
        nextTimer += 1
        timers.set(id, { callback, delayMs })
        return id
      },
      clearTimer: (id) => {
        timers.delete(id)
      }
    },
    {
      onStateChange: (state) => states.push(state),
      onComplete: (events, interrupted) => completions.push({ events, interrupted })
    }
  )

  return {
    controller,
    target,
    timers,
    states,
    completions,
    setNow(value) {
      now = value
    },
    runNextTimer() {
      const entry = timers.entries().next().value
      assert.ok(entry, 'expected a pending timer')
      const [id, timer] = entry
      timers.delete(id)
      timer.callback()
    }
  }
}

test('recorder transitions through start and stop while appending timed keyboard events', () => {
  const harness = createHarness()
  assert.equal(harness.controller.state, 'idle')

  harness.controller.start('Keyboard')
  assert.equal(harness.controller.state, 'recording')
  assert.equal(harness.target.listenerCount(), 6)

  harness.setNow(100)
  harness.target.emit('keydown', { keyCode: 0x41, repeat: false })
  harness.setNow(130)
  harness.target.emit('keydown', { keyCode: 0x41, repeat: true })
  harness.setNow(155)
  harness.target.emit('keyup', { keyCode: 0x41 })
  harness.controller.stop()

  assert.equal(harness.controller.state, 'idle')
  assert.equal(harness.target.listenerCount(), 0)
  assert.deepEqual(harness.states, ['recording', 'idle'])
  assert.deepEqual(harness.completions, [
    {
      interrupted: false,
      events: [
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
          delayMs: 55
        }
      ]
    }
  ])

  harness.controller.start('Keyboard')
  harness.controller.stop()
  assert.deepEqual(harness.completions.at(-1), { events: [], interrupted: false })
})

test('movement mode prepares before recording and cancellation reports interruption', () => {
  const harness = createHarness()

  harness.controller.start('KeyboardMouseMovement')
  assert.equal(harness.controller.state, 'preparing')
  assert.equal(harness.timers.size, 1)
  assert.equal(harness.timers.values().next().value.delayMs, 3000)
  assert.equal(harness.target.listenerCount(), 0)

  harness.runNextTimer()
  assert.equal(harness.controller.state, 'recording')
  assert.equal(harness.target.listenerCount(), 6)

  harness.setNow(200)
  harness.target.emit('mousemove', { clientX: 20, clientY: 30 })
  harness.setNow(225)
  harness.target.emit('mousedown', { button: 0, clientX: 20, clientY: 30 })
  harness.controller.cancel()

  assert.equal(harness.controller.state, 'idle')
  assert.equal(harness.target.listenerCount(), 0)
  assert.deepEqual(harness.states, ['preparing', 'recording', 'idle'])
  assert.deepEqual(harness.completions, [
    {
      interrupted: true,
      events: [
        {
          source: 'Mouse',
          direction: 'Move',
          key: 0,
          x: 20,
          y: 30,
          delayMs: 0
        },
        {
          source: 'Mouse',
          direction: 'Down',
          key: 1,
          x: 20,
          y: 30,
          delayMs: 25
        }
      ]
    }
  ])
})

test('escape interrupts without recording itself and removes every listener', () => {
  const harness = createHarness()
  let prevented = false

  harness.controller.start('KeyboardMouse')
  harness.setNow(50)
  harness.target.emit('keydown', { keyCode: 0x42, repeat: false })
  harness.setNow(75)
  harness.target.emit('keydown', {
    keyCode: 0x1b,
    repeat: false,
    preventDefault() {
      prevented = true
    }
  })

  assert.equal(prevented, true)
  assert.equal(harness.controller.state, 'idle')
  assert.equal(harness.target.listenerCount(), 0)
  assert.equal(harness.completions.length, 1)
  assert.equal(harness.completions[0].interrupted, true)
  assert.deepEqual(harness.completions[0].events.map((event) => event.key), [0x42])
})

test('movement recording appends every mousemove in order without dropping earlier points', () => {
  const harness = createHarness()
  harness.controller.start('KeyboardMouseMovement')
  harness.runNextTimer()

  const count = 200
  for (let index = 0; index < count; index += 1) {
    harness.setNow(index * 2)
    harness.target.emit('mousemove', { clientX: index, clientY: index + 1 })
  }
  harness.controller.stop()

  const events = harness.completions[0].events
  assert.equal(harness.completions[0].interrupted, false)
  assert.equal(events.length, count)
  assert.deepEqual(
    events.map((event) => ({ x: event.x, y: event.y, delayMs: event.delayMs })),
    Array.from({ length: count }, (_, index) => ({
      x: index,
      y: index + 1,
      delayMs: index === 0 ? 0 : 2
    }))
  )
})

test('dispose clears preparing timers and active listeners without completing a recording', () => {
  const harness = createHarness()

  harness.controller.start('KeyboardMouseMovement')
  harness.controller.dispose()
  assert.equal(harness.controller.state, 'idle')
  assert.equal(harness.timers.size, 0)
  assert.equal(harness.target.listenerCount(), 0)
  assert.deepEqual(harness.completions, [])

  harness.controller.start('Keyboard')
  assert.equal(harness.target.listenerCount(), 6)
  harness.controller.dispose()
  assert.equal(harness.target.listenerCount(), 0)
  assert.deepEqual(harness.completions, [])
})
