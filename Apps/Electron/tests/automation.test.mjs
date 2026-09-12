import assert from 'node:assert/strict'
import { registerHooks } from 'node:module'
import test from 'node:test'
import { createStore } from 'zustand/vanilla'
import {
  appendAutomationStep,
  commitAutomationDraft,
  createAutomationPipeline,
  formatAutomationPipelineSubtitle,
  formatAutomationPipelineTitle,
  formatAutomationStepTitle,
  moveAutomationPipeline,
  moveAutomationStep,
  removeAutomationStep,
  splitAutomationPipelines
} from '../src/renderer/src/components/automation/pipelineHelpers.ts'
import { resolveAutomationDialogKey } from '../src/renderer/src/components/automation/automationDialog.ts'
import { formatStepSummary } from '../src/renderer/src/components/automation/steps.ts'
import { createAutomationStoreState } from '../src/renderer/src/stores/automationStoreCore.ts'

registerHooks({
  resolve(specifier, context, nextResolve) {
    try {
      return nextResolve(specifier, context)
    } catch (error) {
      if (specifier.startsWith('./') || specifier.startsWith('../')) {
        return nextResolve(`${specifier}.ts`, context)
      }
      throw error
    }
  }
})

const PIPELINE_A = '00000000-0000-0000-0000-000000000001'
const PIPELINE_B = '00000000-0000-0000-0000-000000000002'
const PIPELINE_C = '00000000-0000-0000-0000-000000000003'
const PIPELINE_D = '00000000-0000-0000-0000-000000000004'

const translations = new Map([
  ['automation.deactivateGpu', 'Deactivate GPU'],
  ['automation.quickAction', 'Quick action'],
  ['automation.state.quiet', 'Quiet mode'],
  ['automation.state.hz', '{{frequency}} Hz'],
  ['automation.state.resolution', '{{width}} x {{height}}'],
  ['automation.steps', 'Steps'],
  ['trigger.acConnected', 'AC adapter connected'],
  ['wpf.automationPipelineControlstep', '{0} step'],
  ['wpf.automationPipelineControlstepmany', '{0} steps'],
  ['wpf.automationPipelineControlunnamed', 'Unnamed quick action']
])

function translate(key, options = {}) {
  let value = translations.get(key) ?? options.defaultValue ?? key
  for (const [name, replacement] of Object.entries(options)) {
    value = value
      .replaceAll(`{{${name}}}`, String(replacement))
      .replaceAll(`{${name}}`, String(replacement))
  }
  return value
}

function pipeline(id, trigger, steps = [], name) {
  return { id, name, trigger, steps, isExclusive: trigger != null }
}

test('pipelines split into automatic and manual sections without losing order', () => {
  const pipelines = [
    pipeline(PIPELINE_A, { $type: 'aCAdapterConnected' }),
    pipeline(PIPELINE_B, null),
    pipeline(PIPELINE_C, { $type: 'displayOff' }),
    pipeline(PIPELINE_D, undefined)
  ]

  const sections = splitAutomationPipelines(pipelines)

  assert.deepEqual(sections.automatic.map(({ id }) => id), [PIPELINE_A, PIPELINE_C])
  assert.deepEqual(sections.manual.map(({ id }) => id), [PIPELINE_B, PIPELINE_D])
  assert.strictEqual(sections.automatic[0], pipelines[0])
  assert.strictEqual(sections.manual[0], pipelines[1])
})

test('pipeline creation trims names and uses stable unique GUIDs', () => {
  const quickAction = createAutomationPipeline([], '  GPU reset  ', null, () => PIPELINE_A)
  assert.deepEqual(quickAction, {
    id: PIPELINE_A,
    name: 'GPU reset',
    trigger: null,
    steps: [],
    isExclusive: false
  })

  const automatic = createAutomationPipeline(
    [quickAction],
    'On AC',
    { $type: 'aCAdapterConnected' },
    () => PIPELINE_B
  )
  assert.equal(automatic?.id, PIPELINE_B)
  assert.equal(automatic?.isExclusive, true)

  assert.equal(
    createAutomationPipeline([quickAction], 'Duplicate', null, () => PIPELINE_A.toUpperCase()),
    null
  )
  assert.equal(createAutomationPipeline([], '   ', null, () => PIPELINE_B), null)
  assert.equal(createAutomationPipeline([], 'Invalid id', null, () => 'temporary-id'), null)
})

test('pipeline titles, subtitles, and unknown step titles have stable fallbacks', () => {
  const triggerNameKey = (trigger) =>
    trigger.$type === 'aCAdapterConnected' ? 'trigger.acConnected' : null
  const localizeName = (name) =>
    name === '__udt.quickAction.deactivateGpu' ? translate('automation.deactivateGpu') : name

  assert.equal(
    formatAutomationPipelineTitle(
      pipeline(PIPELINE_A, null, [], '  __udt.quickAction.deactivateGpu  '),
      translate,
      triggerNameKey,
      localizeName
    ),
    'Deactivate GPU'
  )
  assert.equal(
    formatAutomationPipelineTitle(
      pipeline(PIPELINE_A, { $type: 'aCAdapterConnected' }),
      translate,
      triggerNameKey
    ),
    'AC adapter connected'
  )
  assert.equal(
    formatAutomationPipelineTitle(
      pipeline(PIPELINE_A, { $type: 'VendorSpecificAutomationPipelineTrigger' }),
      translate,
      triggerNameKey
    ),
    'VendorSpecific'
  )
  assert.equal(
    formatAutomationPipelineTitle(pipeline(PIPELINE_A, null), translate, triggerNameKey),
    'Unnamed quick action'
  )

  assert.equal(
    formatAutomationPipelineSubtitle(
      pipeline(PIPELINE_A, null, [{ $type: 'notification' }]),
      translate
    ),
    '1 step'
  )
  assert.equal(
    formatAutomationPipelineSubtitle(
      pipeline(PIPELINE_A, null, [{ $type: 'notification' }, { $type: 'osd' }]),
      translate
    ),
    '2 steps'
  )
  assert.equal(
    formatAutomationStepTitle({ $type: 'VendorSpecificAutomationStep' }, translate),
    'VendorSpecific'
  )
})

test('step summaries format known values and safely fall back for unknown steps', () => {
  const pipelines = [pipeline(PIPELINE_B, null, [], 'GPU reset')]

  assert.equal(
    formatStepSummary({ $type: 'powerMode', state: 'Quiet' }, translate),
    'Quiet mode'
  )
  assert.equal(
    formatStepSummary({ $type: 'refreshRate', state: { frequency: 165 } }, translate),
    '165 Hz'
  )
  assert.equal(
    formatStepSummary(
      { $type: 'resolution', state: { width: 2560, height: 1600 } },
      translate
    ),
    '2560 x 1600'
  )
  assert.equal(
    formatStepSummary({ $type: 'playSound', path: 'C:\\sounds\\ready.wav' }, translate),
    'ready.wav'
  )
  assert.equal(
    formatStepSummary({ $type: 'quickAction', pipelineId: PIPELINE_B }, translate, pipelines),
    'GPU reset'
  )
  assert.equal(formatStepSummary({ $type: 'unknownStep', state: 'On' }, translate), '')
})

test('step add and remove operations are immutable and reject invalid targets', () => {
  const first = pipeline(PIPELINE_A, null, [{ $type: 'first' }])
  const second = pipeline(PIPELINE_B, null, [{ $type: 'other' }])
  const pipelines = [first, second]

  const appended = appendAutomationStep(pipelines, PIPELINE_A, { $type: 'second' })
  assert.deepEqual(appended[0].steps.map(({ $type }) => $type), ['first', 'second'])
  assert.deepEqual(first.steps.map(({ $type }) => $type), ['first'])
  assert.strictEqual(appended[1], second)

  const removed = removeAutomationStep(appended, PIPELINE_A, 0)
  assert.deepEqual(removed[0].steps.map(({ $type }) => $type), ['second'])
  assert.strictEqual(removeAutomationStep(removed, PIPELINE_A, -1), removed)
  assert.strictEqual(removeAutomationStep(removed, 'missing', 0), removed)
  assert.strictEqual(appendAutomationStep(removed, PIPELINE_A, { $type: '' }), removed)

  const duplicateIds = [first, { ...second, id: PIPELINE_A }]
  assert.strictEqual(
    appendAutomationStep(duplicateIds, PIPELINE_A, { $type: 'ignored' }),
    duplicateIds
  )
})

test('step reordering honors first and last boundaries', () => {
  const pipelines = [
    pipeline(PIPELINE_A, null, [
      { $type: 'first' },
      { $type: 'second' },
      { $type: 'third' }
    ])
  ]

  const movedDown = moveAutomationStep(pipelines, PIPELINE_A, 0, 1)
  assert.deepEqual(movedDown[0].steps.map(({ $type }) => $type), [
    'second',
    'first',
    'third'
  ])
  const movedLastToFirst = moveAutomationStep(movedDown, PIPELINE_A, 2, 0)
  assert.deepEqual(movedLastToFirst[0].steps.map(({ $type }) => $type), [
    'third',
    'second',
    'first'
  ])

  assert.strictEqual(moveAutomationStep(pipelines, PIPELINE_A, 0, -1), pipelines)
  assert.strictEqual(moveAutomationStep(pipelines, PIPELINE_A, 2, 3), pipelines)
  assert.strictEqual(moveAutomationStep(pipelines, PIPELINE_A, 1, 1), pipelines)
})

test('pipeline reordering stays inside its automatic or manual section', () => {
  const pipelines = [
    pipeline(PIPELINE_A, { $type: 'displayOn' }),
    pipeline(PIPELINE_B, null),
    pipeline(PIPELINE_C, { $type: 'displayOff' }),
    pipeline(PIPELINE_D, null)
  ]

  const automaticMoved = moveAutomationPipeline(pipelines, PIPELINE_A, 1)
  const automaticSections = splitAutomationPipelines(automaticMoved)
  assert.deepEqual(automaticSections.automatic.map(({ id }) => id), [PIPELINE_C, PIPELINE_A])
  assert.deepEqual(automaticSections.manual.map(({ id }) => id), [PIPELINE_B, PIPELINE_D])

  const manualMoved = moveAutomationPipeline(pipelines, PIPELINE_D, -1)
  const manualSections = splitAutomationPipelines(manualMoved)
  assert.deepEqual(manualSections.automatic.map(({ id }) => id), [PIPELINE_A, PIPELINE_C])
  assert.deepEqual(manualSections.manual.map(({ id }) => id), [PIPELINE_D, PIPELINE_B])

  assert.strictEqual(moveAutomationPipeline(pipelines, PIPELINE_A, -1), pipelines)
  assert.strictEqual(moveAutomationPipeline(pipelines, PIPELINE_D, 1), pipelines)
  assert.strictEqual(moveAutomationPipeline(pipelines, 'missing', 1), pipelines)
})

function createMockAutomationApi(overrides = {}) {
  return {
    getState: async () => ({ isEnabled: false, pipelines: [] }),
    getSupportedSteps: async () => ({ steps: [] }),
    setEnabled: async () => ({ ok: true }),
    savePipelines: async () => ({ saved: true }),
    runNow: async () => ({ ok: true }),
    ...overrides
  }
}

function createAutomationStore(api, refreshTrayMenu = () => undefined) {
  return createStore(createAutomationStoreState({ api, refreshTrayMenu }))
}

test('automation store loads host state and preserves it when a reload fails', async () => {
  const loadedState = {
    isEnabled: true,
    pipelines: [pipeline(PIPELINE_A, { $type: 'displayOn' })]
  }
  let rejectReload = false
  const store = createAutomationStore(
    createMockAutomationApi({
      getState: async () => {
        if (rejectReload) throw new Error('automation reload failed')
        return loadedState
      },
      getSupportedSteps: async () => ({ steps: ['powerMode', 'notification'] })
    })
  )

  assert.equal(await store.getState().load(), true)
  assert.strictEqual(store.getState().state, loadedState)
  assert.deepEqual(store.getState().steps, ['powerMode', 'notification'])
  assert.equal(store.getState().loaded, true)

  rejectReload = true
  assert.equal(await store.getState().load(), false)
  assert.strictEqual(store.getState().state, loadedState)
  assert.equal(store.getState().error, 'automation reload failed')
  assert.equal(store.getState().loading, false)
})

test('successful saves reload canonical state and refresh the tray once', async () => {
  const submitted = [pipeline(PIPELINE_A, null, [{ $type: 'notification', text: 'Ready' }])]
  const canonical = {
    isEnabled: true,
    pipelines: [{ ...submitted[0], name: 'Canonical quick action' }]
  }
  const calls = []
  let refreshCount = 0
  const store = createAutomationStore(
    createMockAutomationApi({
      savePipelines: async (pipelines, isEnabled) => {
        calls.push({ operation: 'save', pipelines, isEnabled })
        return { saved: true }
      },
      getState: async () => {
        calls.push({ operation: 'load' })
        return canonical
      }
    }),
    () => {
      refreshCount += 1
    }
  )

  assert.equal(await store.getState().save(submitted, true), true)
  assert.deepEqual(calls, [
    { operation: 'save', pipelines: submitted, isEnabled: true },
    { operation: 'load' }
  ])
  assert.strictEqual(store.getState().state, canonical)
  assert.equal(store.getState().error, null)
  assert.equal(refreshCount, 1)
})

test('save and toggle failures stay visible without mutating store state', async (t) => {
  const initialState = {
    isEnabled: false,
    pipelines: [pipeline(PIPELINE_A, null, [], 'Existing')]
  }

  await t.test('rejected save response', async () => {
    let loadCount = 0
    const store = createAutomationStore(
      createMockAutomationApi({
        savePipelines: async () => ({ saved: false }),
        getState: async () => {
          loadCount += 1
          return { isEnabled: true, pipelines: [] }
        }
      })
    )
    store.setState({ state: initialState })

    assert.equal(await store.getState().save([], false), false)
    assert.strictEqual(store.getState().state, initialState)
    assert.match(store.getState().error, /save automation/i)
    assert.equal(loadCount, 0)
  })

  await t.test('thrown save failure', async () => {
    const store = createAutomationStore(
      createMockAutomationApi({
        savePipelines: async () => {
          throw new Error('disk is read-only')
        }
      })
    )
    store.setState({ state: initialState })

    assert.equal(await store.getState().save([], false), false)
    assert.strictEqual(store.getState().state, initialState)
    assert.equal(store.getState().error, 'disk is read-only')
  })

  await t.test('post-save reload failure', async () => {
    let refreshCount = 0
    const store = createAutomationStore(
      createMockAutomationApi({
        getState: async () => {
          throw new Error('saved state could not be reloaded')
        }
      }),
      () => {
        refreshCount += 1
      }
    )
    store.setState({ state: initialState })

    assert.equal(await store.getState().save([], false), false)
    assert.strictEqual(store.getState().state, initialState)
    assert.equal(store.getState().error, 'saved state could not be reloaded')
    assert.equal(refreshCount, 0)
  })

  await t.test('rejected save response uses host error when provided', async () => {
    const store = createAutomationStore(
      createMockAutomationApi({
        savePipelines: async () => ({ saved: false, error: 'quota exceeded' })
      })
    )
    store.setState({ state: initialState })

    assert.equal(await store.getState().save([], false), false)
    assert.strictEqual(store.getState().state, initialState)
    assert.equal(store.getState().error, 'quota exceeded')
  })

  await t.test('rejected enabled toggle', async () => {
    const store = createAutomationStore(
      createMockAutomationApi({
        setEnabled: async () => ({ ok: false })
      })
    )
    store.setState({ state: initialState })

    assert.equal(await store.getState().setEnabled(true), false)
    assert.strictEqual(store.getState().state, initialState)
    assert.match(store.getState().error, /update automation/i)
  })
})

test('run-now failures are exposed and later success clears stale errors', async () => {
  let fail = true
  const store = createAutomationStore(
    createMockAutomationApi({
      runNow: async () => {
        if (fail) throw new Error('pipeline execution failed')
        return { ok: true }
      }
    })
  )

  assert.equal(await store.getState().runNow(PIPELINE_A), false)
  assert.equal(store.getState().error, 'pipeline execution failed')

  fail = false
  assert.equal(await store.getState().runNow(PIPELINE_A), true)
  assert.equal(store.getState().error, null)
})

test('run-now rejected flag keeps store state and surfaces an explicit error', async () => {
  const store = createAutomationStore(
    createMockAutomationApi({
      runNow: async () => ({ ok: false, message: 'pipeline is exclusive' })
    })
  )

  assert.equal(await store.getState().runNow(PIPELINE_A), false)
  assert.equal(store.getState().error, 'pipeline is exclusive')
  store.getState().clearError()
  assert.equal(store.getState().error, null)
})

test('failed save or revert commits keep the dirty draft', () => {
  const draft = [pipeline(PIPELINE_A, null, [{ $type: 'notification' }], 'Draft')]
  const canonical = [pipeline(PIPELINE_B, null, [], 'Saved')]

  assert.deepEqual(commitAutomationDraft(false, draft, canonical), {
    value: draft,
    dirty: true
  })
  assert.strictEqual(commitAutomationDraft(false, draft, canonical).value, draft)
  assert.deepEqual(commitAutomationDraft(true, draft, canonical), {
    value: canonical,
    dirty: false
  })
})

test('automation dialog keys close on Escape and wrap Tab at the edges', () => {
  assert.equal(resolveAutomationDialogKey('Escape', false, 3, 1), 'close')
  assert.equal(resolveAutomationDialogKey('Tab', false, 3, 2), 'wrap-end')
  assert.equal(resolveAutomationDialogKey('Tab', true, 3, 0), 'wrap-start')
  assert.equal(resolveAutomationDialogKey('Tab', false, 3, 0), null)
  assert.equal(resolveAutomationDialogKey('Enter', false, 3, 0), null)
})

const bridgeCalls = []
let bridgeResponder = async () => {
  throw new Error('Unexpected bridge call')
}

globalThis.window = {
  bridge: {
    async invoke(method, params) {
      const call = { method, params: structuredClone(params) }
      bridgeCalls.push(call)
      return bridgeResponder(method, call.params)
    }
  }
}

const { automationApi } = await import('../src/renderer/src/api/automation.ts')

function resetBridge(responder) {
  bridgeCalls.length = 0
  bridgeResponder = responder
}

test('automation API preserves optional save state and stable pipeline IDs', async () => {
  resetBridge(async (method) => {
    if (method === 'automation.savePipelines') return { saved: true }
    if (method === 'automation.runNow') return { ok: true }
    throw new Error(`Unexpected method: ${method}`)
  })
  const pipelines = [pipeline(PIPELINE_A, null, [], 'Quick action')]

  assert.deepEqual(await automationApi.savePipelines(pipelines), { saved: true })
  assert.deepEqual(await automationApi.savePipelines(pipelines, false), { saved: true })
  assert.deepEqual(await automationApi.runNow(PIPELINE_A), { ok: true })
  assert.deepEqual(bridgeCalls, [
    {
      method: 'automation.savePipelines',
      params: { pipelines }
    },
    {
      method: 'automation.savePipelines',
      params: { pipelines, isEnabled: false }
    },
    {
      method: 'automation.runNow',
      params: { pipelineId: PIPELINE_A }
    }
  ])
})

test('automation API does not hide bridge failures', async () => {
  resetBridge(async () => {
    throw new Error(
      "Error invoking remote method 'bridge:invoke': Error: automation save exploded"
    )
  })

  await assert.rejects(
    automationApi.savePipelines([], true),
    /automation save exploded/
  )
})
