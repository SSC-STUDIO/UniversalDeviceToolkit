import assert from 'node:assert/strict'
import test from 'node:test'
import {
  DEFAULT_DASHBOARD_GROUPS,
  resolveDashboardFeature
} from '../src/renderer/src/components/dashboard-parity/dashboardItems.ts'
import { resolveSensorViewPhase } from '../src/renderer/src/components/dashboard/sensorViewPhase.ts'
import {
  createUiVisibilityGate,
  mergeUiVisibility
} from '../src/renderer/src/utils/uiVisibility.ts'

function info(key, supported = true) {
  return { key, supported, stateType: 'TestState' }
}

test('default groups retain the WPF section order and all dashboard items', () => {
  assert.deepEqual(DEFAULT_DASHBOARD_GROUPS.map((group) => group.type), [
    'Power',
    'Graphics',
    'Display',
    'Other'
  ])
  assert.equal(DEFAULT_DASHBOARD_GROUPS.flatMap((group) => group.items).length, 23)
})

test('WPF dashboard item names resolve to the host feature keys', () => {
  const infos = {
    powerMode: info('powerMode'),
    battery: info('battery'),
    batteryNightCharge: info('batteryNightCharge'),
    alwaysOnUsb: info('alwaysOnUsb'),
    instantBoot: info('instantBoot'),
    flipToStart: info('flipToStart'),
    resolution: info('resolution'),
    refreshRate: info('refreshRate'),
    dpiScale: info('dpiScale'),
    hdr: info('hdr'),
    overDrive: info('overDrive'),
    microphone: info('microphone'),
    panelLogo: info('panelLogo'),
    portsBacklight: info('portsBacklight'),
    touchpadLock: info('touchpadLock'),
    fnLock: info('fnLock'),
    winKey: info('winKey'),
    itsMode: info('itsMode')
  }

  assert.equal(resolveDashboardFeature('PowerMode', infos), 'powerMode')
  assert.equal(resolveDashboardFeature('BatteryMode', infos), 'battery')
  assert.equal(resolveDashboardFeature('BatteryNightChargeMode', infos), 'batteryNightCharge')
  assert.equal(resolveDashboardFeature('AlwaysOnUsb', infos), 'alwaysOnUsb')
  assert.equal(resolveDashboardFeature('InstantBoot', infos), 'instantBoot')
  assert.equal(resolveDashboardFeature('FlipToStart', infos), 'flipToStart')
  assert.equal(resolveDashboardFeature('Resolution', infos), 'resolution')
  assert.equal(resolveDashboardFeature('RefreshRate', infos), 'refreshRate')
  assert.equal(resolveDashboardFeature('DpiScale', infos), 'dpiScale')
  assert.equal(resolveDashboardFeature('Hdr', infos), 'hdr')
  assert.equal(resolveDashboardFeature('OverDrive', infos), 'overDrive')
  assert.equal(resolveDashboardFeature('Microphone', infos), 'microphone')
  assert.equal(resolveDashboardFeature('PanelLogoBacklight', infos), 'panelLogo')
  assert.equal(resolveDashboardFeature('PortsBacklight', infos), 'portsBacklight')
  assert.equal(resolveDashboardFeature('TouchpadLock', infos), 'touchpadLock')
  assert.equal(resolveDashboardFeature('FnLock', infos), 'fnLock')
  assert.equal(resolveDashboardFeature('WinKeyLock', infos), 'winKey')
  assert.equal(resolveDashboardFeature('ItsMode', infos), 'itsMode')
})

test('hybrid and keyboard items select the supported WPF fallback implementation', () => {
  assert.equal(resolveDashboardFeature('HybridMode', {
    hybridMode: info('hybridMode', false),
    igpuMode: info('igpuMode')
  }), 'igpuMode')

  assert.equal(resolveDashboardFeature('WhiteKeyboardBacklight', {
    whiteKeyboard: info('whiteKeyboard', false),
    oneLevelWhiteKeyboard: info('oneLevelWhiteKeyboard')
  }), 'oneLevelWhiteKeyboard')
})

test('unsupported features still resolve when present; special items stay null', () => {
  assert.equal(resolveDashboardFeature('PowerMode', { powerMode: info('powerMode', false) }), 'powerMode')
  assert.equal(resolveDashboardFeature('DiscreteGpu', {}), null)
  assert.equal(resolveDashboardFeature('OverclockDiscreteGpu', {}), null)
  assert.equal(resolveDashboardFeature('TurnOffMonitors', {}), null)
})

test('UI visibility is active only when document and app are both visible', () => {
  assert.equal(mergeUiVisibility(true, true), true)
  assert.equal(mergeUiVisibility(true, false), false)
  assert.equal(mergeUiVisibility(false, true), false)
  assert.equal(mergeUiVisibility(false, false), false)
})

test('visibility gate emits only when the merged documentVisible && appVisible value changes', () => {
  const seen = []
  const gate = createUiVisibilityGate((active) => {
    seen.push(active)
  })

  gate.setDocumentVisible(true)
  gate.setAppVisible(true)
  assert.deepEqual(seen, [true])

  gate.setDocumentVisible(true)
  gate.setAppVisible(true)
  assert.deepEqual(seen, [true])

  gate.setAppVisible(false)
  assert.deepEqual(seen, [true, false])

  gate.setDocumentVisible(false)
  assert.deepEqual(seen, [true, false])

  gate.setAppVisible(true)
  assert.deepEqual(seen, [true, false])

  gate.setDocumentVisible(true)
  assert.deepEqual(seen, [true, false, true])
  assert.equal(gate.getActive(), true)
})

test('sensor view phase leaves a failed snapshot on error instead of perpetual loading', () => {
  assert.equal(resolveSensorViewPhase(null, 'idle'), 'idle')
  assert.equal(resolveSensorViewPhase(null, 'loading'), 'loading')
  assert.equal(resolveSensorViewPhase(null, 'error'), 'error')
  assert.equal(resolveSensorViewPhase(null, 'ready'), 'error')
  assert.equal(resolveSensorViewPhase({ ts: '1' }, 'loading'), 'ready')
  assert.equal(resolveSensorViewPhase({ ts: '1' }, 'error'), 'ready')
})
