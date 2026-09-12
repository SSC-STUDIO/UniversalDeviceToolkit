import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const section = readFileSync(
  new URL('../src/renderer/src/components/dashboard/SensorSection.tsx', import.meta.url),
  'utf8'
)
const css = readFileSync(
  new URL('../src/renderer/src/components/dashboard/sensor.css', import.meta.url),
  'utf8'
)
const store = readFileSync(
  new URL('../src/renderer/src/stores/sensorsStore.ts', import.meta.url),
  'utf8'
)
const trendChart = readFileSync(
  new URL('../src/renderer/src/components/dashboard/TrendChart.tsx', import.meta.url),
  'utf8'
)

test('low-power adapter warning uses the notification center, not a dashboard overlay', () => {
  assert.match(section, /from '\.\.\/\.\.\/notifications'/)
  assert.match(section, /notify\(\{ title, severity: 'Warning', isPersistent: true \}\)/)
  assert.match(section, /dashboard\.sensor\.lowPowerAdapter/)
  assert.doesNotMatch(section, /afterChart/)
  assert.doesNotMatch(section, /batteryAfterChart/)
  assert.doesNotMatch(section, /udt-sensor-panel__warning--low-power/)
  assert.doesNotMatch(css, /warning--low-power/)
  assert.doesNotMatch(css, /warnings--after-chart/)
})

test('first sensor sample is seeded so charts can draw with the first gauge reading', () => {
  assert.match(store, /const duplicateFirst = history\.labels\.length === 0/)
  assert.match(store, /duplicateFirst \? \[value, value\] : \[\.\.\.values, value\]/)
  assert.match(trendChart, /length >= 2/)
})
