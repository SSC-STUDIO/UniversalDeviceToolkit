import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const api = readFileSync(new URL('../src/renderer/src/features/dashboard/api/dashboardHardware.ts', import.meta.url), 'utf8')
const handler = readFileSync(new URL('../../Host/Rpc/Handlers/DashboardHardwareHandlers.cs', import.meta.url), 'utf8')

test('dashboard hardware RPC operation names stay aligned', () => {
  for (const operation of [
    'getState',
    'setMonitoring',
    'killGpuProcesses',
    'restartGpu',
    'setOverclockEnabled',
    'setOverclock',
    'turnOffMonitors'
  ]) {
    assert.match(api, new RegExp(`dashboardHardware\\.${operation}`))
    assert.match(handler, new RegExp(`dashboardHardware\\.${operation}`))
  }
})

test('GPU monitoring uses a subscriber count and stops at zero', () => {
  assert.match(handler, /_gpuMonitorCount\+\+/)
  assert.match(handler, /_gpuMonitorCount = Math\.Max\(0, _gpuMonitorCount - 1\)/)
  assert.match(handler, /gpuController\.StopAsync/)
})

test('special dashboard items remain represented by dedicated cards', () => {
  const items = readFileSync(new URL('../src/renderer/src/features/dashboard/components/dashboardItems.ts', import.meta.url), 'utf8')
  const cards = readFileSync(new URL('../src/renderer/src/features/dashboard/components/DashboardSpecialCard.tsx', import.meta.url), 'utf8')

  for (const item of ['DiscreteGpu', 'OverclockDiscreteGpu', 'TurnOffMonitors']) {
    assert.match(items, new RegExp(item))
    assert.match(cards, new RegExp(item))
  }
})

test('dashboard saveConfig rejects a missing saved flag', () => {
  const dashboard = readFileSync(new URL('../src/renderer/src/features/dashboard/api/dashboard.ts', import.meta.url), 'utf8')
  assert.match(dashboard, /result\.saved !== true/)
})

test('hardware mutation callers reject ok !== true', () => {
  const support = readFileSync(new URL('../src/renderer/src/features/dashboard/components/dashboardHardwareSupport.ts', import.meta.url), 'utf8')
  const special = readFileSync(new URL('../src/renderer/src/features/dashboard/components/DashboardSpecialCard.tsx', import.meta.url), 'utf8')
  const overclock = readFileSync(new URL('../src/renderer/src/features/dashboard/components/OverclockProfilesModal.tsx', import.meta.url), 'utf8')
  assert.match(support, /result\.ok !== true/)
  assert.match(special, /requireHardwareOk/)
  assert.match(overclock, /requireHardwareOk/)
})
