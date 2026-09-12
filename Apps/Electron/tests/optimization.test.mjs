import assert from 'node:assert/strict'
import { registerHooks } from 'node:module'
import test from 'node:test'

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

const bridgeCalls = []
let bridgeResponder = async () => {
  throw new Error('Unexpected bridge call')
}

globalThis.window = {
  bridge: {
    async invoke(method, params) {
      const call = { method, params: globalThis.structuredClone(params) }
      bridgeCalls.push(call)
      return bridgeResponder(method, call.params)
    },
    on() {
      return () => undefined
    },
    async getHostStatus() {
      return { running: true, ready: true, lastError: null, readyPayload: null }
    }
  }
}

const {
  optimizationApi
} = await import('../src/renderer/src/api/optimization.ts')
const {
  localizeHostError
} = await import('../src/renderer/src/api/bridge.ts')
const {
  useOptimizationStore
} = await import('../src/renderer/src/stores/optimizationStore.ts')
const {
  getTogglePairFeatureState,
  presentCategoryActions
} = await import('../src/renderer/src/utils/optimizationToggle.ts')
const {
  NETWORK_ACCELERATION_MODES,
  collectRecommendedActionKeys,
  getActionSelectionPresentation,
  getNetworkSelectedTargetCount,
  isFailedCleanupEstimate,
  isOptimizationPlayDisabled,
  presentActionNotification,
  resolveActionError,
  runExclusivePoll,
  shouldShowEmptyPlaceholder,
  visibleOptimizationTabs
} = await import('../src/renderer/src/utils/optimizationPresentation.ts')

function action(key, applied, recommended = false) {
  return {
    key,
    title: `${key}.title`,
    description: `${key}.description`,
    recommended,
    applied
  }
}

function category(key, actions) {
  return {
    key,
    title: `${key}.title`,
    description: `${key}.description`,
    pluginId: null,
    hasSettings: false,
    actions
  }
}

function networkConfig(mode = 'SystemProxy') {
  return {
    accelerationEnabled: true,
    mode,
    listenPort: 8877,
    domainGroups: [
      {
        id: 'steam',
        displayName: 'Steam',
        enabled: true,
        isFavorite: true,
        domains: ['store.steampowered.com', '  '],
        subItems: [
          {
            id: 'community',
            displayName: 'Community',
            domain: 'steamcommunity.com',
            enabled: true,
            isBeta: false
          },
          {
            id: 'cdn',
            displayName: 'CDN',
            domain: 'steamcdn-a.akamaihd.net',
            enabled: false,
            isBeta: false
          }
        ],
        iconKey: 'SteamLogo',
        description: null
      }
    ],
    dnsServer: '1.1.1.1',
    dohUrl: 'https://cloudflare-dns.com/dns-query',
    certificateFingerprintSha256: 'ABC123',
    lastRecoverySnapshot: {
      capturedAtUtc: '2026-08-13T16:00:00Z',
      snapshotPath: 'snapshot.json',
      hadSystemProxy: false,
      hadHostsBlock: false,
      hadPacPath: false,
      notes: null
    },
    showInNavigation: true
  }
}

function networkStatus(config = networkConfig(), overrides = {}) {
  return {
    config,
    isBackendReady: true,
    isRunning: false,
    statusText: 'Ready',
    ...overrides
  }
}

function resetBridge(responder) {
  bridgeCalls.length = 0
  bridgeResponder = responder
}

function resetOptimizationStore(state = {}) {
  useOptimizationStore.setState({
    categories: [],
    networkStatus: null,
    trafficSnapshot: null,
    runtimeSnapshot: null,
    loading: false,
    error: null,
    ...state
  })
}

test('toggle pairs present only the action matching the current feature state', () => {
  const enabledActions = [
    action('feature.enable', true, true),
    action('feature.disable', false),
    action('standalone', false)
  ]
  const enabledPresentation = presentCategoryActions(enabledActions)
  assert.deepEqual(
    enabledPresentation.visible.map(({ action: visibleAction }) => visibleAction.key),
    ['feature.disable', 'standalone']
  )
  assert.equal(enabledPresentation.visible[0].editable, true)

  const disabledActions = [
    action('feature.enable', false, true),
    action('feature.disable', true)
  ]
  const disabledPresentation = presentCategoryActions(disabledActions)
  assert.deepEqual(
    disabledPresentation.visible.map(({ action: visibleAction }) => visibleAction.key),
    ['feature.enable']
  )
  assert.deepEqual(disabledPresentation.recommendedKeys, ['feature.enable'])
})

test('unknown, conflicting, cleanup, and busy actions expose the correct editability', () => {
  const unknownPair = [
    action('feature.enable', null, true),
    action('feature.disable', null)
  ]
  const unknownPresentation = presentCategoryActions(unknownPair)
  assert.equal(unknownPresentation.visible[0].action.key, 'feature.enable')
  assert.equal(unknownPresentation.visible[0].editable, false)

  const conflictingPair = [
    action('feature.enable', true),
    action('feature.disable', true)
  ]
  assert.equal(getTogglePairFeatureState({
    baseKey: 'feature',
    enable: conflictingPair[0],
    disable: conflictingPair[1]
  }), null)
  assert.equal(presentCategoryActions(conflictingPair).visible[0].editable, false)

  const unpaired = [
    action('optimization.unknown', null),
    action('cleanup.cache', null)
  ]
  assert.deepEqual(
    presentCategoryActions(unpaired).visible.map(({ action: visibleAction, editable }) => ({
      key: visibleAction.key,
      editable
    })),
    [
      { key: 'optimization.unknown', editable: false },
      { key: 'cleanup.cache', editable: true }
    ]
  )
  assert.ok(presentCategoryActions(unpaired, true).visible.every(({ editable }) => !editable))
})

test('recommended selection skips applied actions and respects category scope', () => {
  const categories = [
    category('optimization.system', [
      action('system.recommended', false, true),
      action('system.applied', true, true),
      action('system.optional', false, false)
    ]),
    category('cleanup.cache', [
      action('cleanup.recommended', null, true)
    ])
  ]

  assert.deepEqual(
    collectRecommendedActionKeys(categories, (key) => !key.startsWith('cleanup.')),
    ['system.recommended']
  )
  assert.deepEqual(
    collectRecommendedActionKeys(categories, (key) => key.startsWith('cleanup.')),
    ['cleanup.recommended']
  )
})

test('action selection preserves applied and indeterminate checkbox state', () => {
  assert.deepEqual(
    getActionSelectionPresentation(action('applied', true), false),
    { checked: true, indeterminate: false }
  )
  assert.deepEqual(
    getActionSelectionPresentation(action('selected', false), true),
    { checked: true, indeterminate: false }
  )
  assert.deepEqual(
    getActionSelectionPresentation(action('unknown', null), false),
    { checked: false, indeterminate: true }
  )
})

test('optimization store applies selected actions and refreshes their applied state', async () => {
  const selectedKeys = ['system.recommended', 'system.optional']
  const refreshedCategories = [
    category('optimization.system', [
      action('system.recommended', true, true),
      action('system.optional', true)
    ])
  ]
  resetBridge(async (method, params) => {
    if (method === 'optimization.apply') {
      assert.deepEqual(params, { actionKeys: selectedKeys })
      return { applied: true }
    }
    if (method === 'optimization.getCategories') {
      return { categories: refreshedCategories }
    }
    throw new Error(`Unexpected method: ${method}`)
  })
  resetOptimizationStore({
    categories: [
      category('optimization.system', [
        action('system.recommended', false, true),
        action('system.optional', false)
      ])
    ]
  })

  assert.equal(await useOptimizationStore.getState().apply(selectedKeys), true)
  assert.deepEqual(bridgeCalls.map(({ method }) => method), [
    'optimization.apply',
    'optimization.getCategories'
  ])
  assert.deepEqual(useOptimizationStore.getState().categories, refreshedCategories)
})

test('play availability enforces selection, backend, and Hosts-mode rules', () => {
  const readyStatus = networkStatus()
  const base = {
    busy: false,
    cleanupSelectedCount: 1,
    driverSelectedCount: 1,
    networkStatus: readyStatus
  }

  assert.equal(isOptimizationPlayDisabled({ ...base, tab: 'optimization' }), false)
  assert.equal(isOptimizationPlayDisabled({ ...base, tab: 'optimization', busy: true }), true)
  assert.equal(
    isOptimizationPlayDisabled({ ...base, tab: 'cleanup', cleanupSelectedCount: 0 }),
    true
  )
  assert.equal(
    isOptimizationPlayDisabled({ ...base, tab: 'driverDownload', driverSelectedCount: 0 }),
    true
  )
  assert.equal(
    isOptimizationPlayDisabled({ ...base, tab: 'networkAcceleration', networkStatus: null }),
    true
  )
  assert.equal(
    isOptimizationPlayDisabled({
      ...base,
      tab: 'networkAcceleration',
      networkStatus: networkStatus(networkConfig(), { isBackendReady: false })
    }),
    true
  )
  assert.equal(
    isOptimizationPlayDisabled({
      ...base,
      tab: 'networkAcceleration',
      networkStatus: networkStatus(networkConfig('Hosts'))
    }),
    true
  )
  assert.equal(
    isOptimizationPlayDisabled({
      ...base,
      tab: 'networkAcceleration',
      networkStatus: networkStatus(networkConfig('DiagnosticsOnly'))
    }),
    false
  )
})

test('network target count ignores disabled groups and blank domains', () => {
  const config = networkConfig()
  assert.equal(getNetworkSelectedTargetCount(config), 2)
  assert.equal(
    getNetworkSelectedTargetCount({
      ...config,
      domainGroups: [
        ...config.domainGroups,
        { ...config.domainGroups[0], id: 'disabled', enabled: false }
      ]
    }),
    2
  )
})

test('network mode values survive save API serialization unchanged', async () => {
  assert.deepEqual(
    [...NETWORK_ACCELERATION_MODES],
    ['Off', 'SystemProxy', 'Hosts', 'DiagnosticsOnly']
  )

  for (const mode of ['SystemProxy', 'Hosts', 'DiagnosticsOnly']) {
    resetBridge(async () => ({ saved: true }))
    const config = networkConfig(mode)

    assert.deepEqual(await optimizationApi.networkSaveConfig(config), { saved: true })
    assert.deepEqual(bridgeCalls, [
      {
        method: 'network.saveConfig',
        params: { config }
      }
    ])
    assert.equal(JSON.parse(JSON.stringify(bridgeCalls[0].params)).config.mode, mode)
  }
})

test('network store saves the current config and reloads the persisted status', async () => {
  const initialConfig = networkConfig('SystemProxy')
  const currentConfig = {
    ...initialConfig,
    mode: 'DiagnosticsOnly',
    listenPort: 9123,
    dnsServer: '8.8.8.8'
  }
  let persistedConfig = initialConfig
  resetBridge(async (method, params) => {
    if (method === 'network.saveConfig') {
      persistedConfig = globalThis.structuredClone(params.config)
      return { saved: true }
    }
    if (method === 'network.getStatus') {
      return networkStatus(persistedConfig)
    }
    throw new Error(`Unexpected method: ${method}`)
  })
  resetOptimizationStore({ networkStatus: networkStatus(initialConfig) })

  assert.equal(
    await useOptimizationStore.getState().saveNetworkConfig(currentConfig),
    true
  )
  assert.deepEqual(bridgeCalls.map(({ method }) => method), [
    'network.saveConfig',
    'network.getStatus'
  ])
  assert.deepEqual(persistedConfig, currentConfig)
  assert.deepEqual(useOptimizationStore.getState().networkStatus?.config, currentConfig)
})

test('network group updates derive from and preserve the current config', async () => {
  const currentConfig = {
    ...networkConfig('DiagnosticsOnly'),
    listenPort: 9555
  }
  let persistedConfig = currentConfig
  resetBridge(async (method, params) => {
    if (method === 'network.saveConfig') {
      persistedConfig = globalThis.structuredClone(params.config)
      return { saved: true }
    }
    if (method === 'network.getStatus') return networkStatus(persistedConfig)
    throw new Error(`Unexpected method: ${method}`)
  })
  resetOptimizationStore({ networkStatus: networkStatus(currentConfig) })

  assert.equal(
    await useOptimizationStore.getState().setNetworkGroupEnabled('STEAM', false),
    true
  )
  assert.equal(persistedConfig.mode, 'DiagnosticsOnly')
  assert.equal(persistedConfig.listenPort, 9555)
  assert.equal(persistedConfig.domainGroups[0].enabled, false)
  assert.ok(persistedConfig.domainGroups[0].subItems.every((subItem) => !subItem.enabled))
})

test('network start and stop call the host, refresh status, and clear stale runtime data', async () => {
  let running = false
  const config = networkConfig()
  resetBridge(async (method) => {
    if (method === 'network.start') {
      running = true
      return { ok: true }
    }
    if (method === 'network.stop') {
      running = false
      return { ok: true }
    }
    if (method === 'network.getStatus') {
      return networkStatus(config, { isRunning: running })
    }
    throw new Error(`Unexpected method: ${method}`)
  })
  resetOptimizationStore({ networkStatus: networkStatus(config) })

  assert.equal(await useOptimizationStore.getState().startNetwork(), true)
  assert.equal(useOptimizationStore.getState().networkStatus?.isRunning, true)

  useOptimizationStore.setState({
    trafficSnapshot: {
      bytesUploaded: 10,
      bytesDownloaded: 20,
      activeConnections: 1,
      totalConnections: 2
    },
    runtimeSnapshot: {
      healthStatus: 'healthy',
      traffic: {
        bytesUploaded: 10,
        bytesDownloaded: 20,
        activeConnections: 1,
        totalConnections: 2
      },
      connections: [],
      destinations: []
    }
  })

  assert.equal(await useOptimizationStore.getState().stopNetwork(), true)
  assert.equal(useOptimizationStore.getState().networkStatus?.isRunning, false)
  assert.equal(useOptimizationStore.getState().trafficSnapshot, null)
  assert.equal(useOptimizationStore.getState().runtimeSnapshot, null)
  assert.deepEqual(bridgeCalls.map(({ method }) => method), [
    'network.start',
    'network.getStatus',
    'network.stop',
    'network.getStatus'
  ])
})

test('network save, start, and stop failures are exposed through store error state', async () => {
  const config = networkConfig()
  const operations = [
    {
      method: 'network.saveConfig',
      run: () => useOptimizationStore.getState().saveNetworkConfig(config)
    },
    {
      method: 'network.start',
      run: () => useOptimizationStore.getState().startNetwork()
    },
    {
      method: 'network.stop',
      run: () => useOptimizationStore.getState().stopNetwork()
    }
  ]

  for (const operation of operations) {
    resetOptimizationStore({ networkStatus: networkStatus(config) })
    resetBridge(async () => {
      throw new Error(
        `Error invoking remote method 'bridge:invoke': Error: [UDT:-1012] ${operation.method} refused`
      )
    })

    assert.equal(await operation.run(), false)
    assert.equal(
      useOptimizationStore.getState().error,
      `[UDT:-1012] ${operation.method} refused`
    )
    assert.deepEqual(bridgeCalls.map(({ method }) => method), [operation.method])
  }
})

test('exclusive poll skips overlapping runs and releases the lock afterwards', async () => {
  const inFlight = { current: false }
  let active = 0
  let maxActive = 0
  let runs = 0
  const poll = async () => {
    active += 1
    maxActive = Math.max(maxActive, active)
    runs += 1
    await new Promise((resolve) => setTimeout(resolve, 20))
    active -= 1
  }

  const first = runExclusivePoll(inFlight, poll)
  const skipped = runExclusivePoll(inFlight, poll)
  assert.equal(await skipped, false)
  assert.equal(await first, true)
  assert.equal(runs, 1)
  assert.equal(maxActive, 1)
  assert.equal(inFlight.current, false)
  assert.equal(await runExclusivePoll(inFlight, poll), true)
  assert.equal(runs, 2)
})

test('cleanup estimate treats 0-plus-error as failure, not a successful size', () => {
  assert.equal(isFailedCleanupEstimate(0, '[UDT:-1012] estimate refused'), true)
  assert.equal(isFailedCleanupEstimate(0, null), false)
  assert.equal(isFailedCleanupEstimate(0, ''), false)
  assert.equal(isFailedCleanupEstimate(128, 'stale error'), false)
  assert.equal(isFailedCleanupEstimate(Number.NaN, null), true)
  assert.equal(isFailedCleanupEstimate(-1, null), true)
})

test('action errors keep the host message and fall back when the store is silent', () => {
  assert.equal(resolveActionError('[UDT:-1012] refused', 'Failed'), '[UDT:-1012] refused')
  assert.equal(resolveActionError('  ', 'Failed'), 'Failed')
  assert.equal(resolveActionError(null, 'Failed'), 'Failed')
  assert.equal(resolveActionError(undefined, 'Failed'), 'Failed')
})

test('empty placeholders stay hidden while loading, unloaded, or after an error', () => {
  assert.equal(
    shouldShowEmptyPlaceholder({ loading: false, itemCount: 0, error: null, loaded: true }),
    true
  )
  assert.equal(
    shouldShowEmptyPlaceholder({ loading: true, itemCount: 0, error: null, loaded: true }),
    false
  )
  assert.equal(
    shouldShowEmptyPlaceholder({ loading: false, itemCount: 0, error: null, loaded: false }),
    false
  )
  assert.equal(
    shouldShowEmptyPlaceholder({
      loading: false,
      itemCount: 0,
      error: 'load failed',
      loaded: true
    }),
    false
  )
  assert.equal(
    shouldShowEmptyPlaceholder({ loading: false, itemCount: 2, error: null, loaded: true }),
    false
  )
})

test('host error localization uses stable codes and readable unknown-code fallbacks', () => {
  const translations = new Map([
    ['optimization.network.hostsModeRefused', 'Hosts mode is unavailable'],
    [
      'optimization.network.startRefused',
      'Acceleration did not start. Check that it is enabled and at least one target is selected.'
    ]
  ])
  const translate = (key, { defaultValue } = { defaultValue: key }) =>
    translations.get(key) ?? defaultValue

  assert.equal(
    localizeHostError(
      new Error(
        "Error invoking remote method 'bridge:invoke': Error: [UDT:-1011] Host refused the mode"
      ),
      translate
    ),
    'Hosts mode is unavailable'
  )
  assert.equal(
    localizeHostError('[UDT:-1012] Failed to start network acceleration.', translate),
    'Acceleration did not start. Check that it is enabled and at least one target is selected.'
  )
  assert.equal(
    localizeHostError('[UDT:-3999] Detailed host fallback', translate),
    'Detailed host fallback'
  )
  assert.equal(
    localizeHostError('Plain host failure', translate),
    'Plain host failure'
  )
})

test('action notifications keep a short title and put the host detail in the message', () => {
  assert.deepEqual(
    presentActionNotification(
      'Acceleration did not start. Check that it is enabled and at least one target is selected.',
      'Failed to start'
    ),
    {
      title: 'Failed to start',
      message:
        'Acceleration did not start. Check that it is enabled and at least one target is selected.'
    }
  )
  assert.deepEqual(presentActionNotification('Failed to start', 'Failed to start'), {
    title: 'Failed to start'
  })
  assert.deepEqual(presentActionNotification('  Host detail  ', '  '), {
    title: 'Host detail'
  })
})

test('optimization tabs hide network acceleration when it was not installed', () => {
  const tabs = ['optimization', 'cleanup', 'driverDownload', 'networkAcceleration', 'gameBoost']
  assert.deepEqual(visibleOptimizationTabs(tabs, true), tabs)
  assert.deepEqual(visibleOptimizationTabs(tabs, false), [
    'optimization',
    'cleanup',
    'driverDownload',
    'gameBoost'
  ])
})
