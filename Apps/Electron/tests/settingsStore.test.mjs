import assert from 'node:assert/strict'
import { Buffer } from 'node:buffer'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import ts from 'typescript'

const settingsStoreSource = readFileSync(
  new URL('../src/renderer/src/stores/settingsStore.ts', import.meta.url),
  'utf8'
)

let moduleSequence = 0

function createStoreStubSource() {
  return `
function create(initializer) {
  let state
  const getState = () => state
  const setState = (update) => {
    const partial = typeof update === 'function' ? update(state) : update
    state = { ...state, ...partial }
  }
  const store = (selector = (value) => value) => selector(state)
  store.getState = getState
  store.setState = setState
  state = initializer(setState, getState)
  return store
}
`
}

async function loadSettingsStore(settingsApi) {
  moduleSequence += 1
  const apiKey = `__udtSettingsStoreTestApi${moduleSequence}`
  globalThis[apiKey] = settingsApi

  const harnessSource = settingsStoreSource
    .replace(/import \{ create \} from 'zustand'\r?\n/, createStoreStubSource())
    .replace(
      /import \{ settingsApi, type SettingsScope \} from '\.\.\/api\/settings'\r?\n/,
      `type SettingsScope = string\nconst settingsApi = globalThis[${JSON.stringify(apiKey)}]\n`
    )

  assert.doesNotMatch(harnessSource, /^import /m)
  const output = ts.transpileModule(harnessSource, {
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText

  try {
    const encoded = Buffer.from(output, 'utf8').toString('base64')
    return await import(`data:text/javascript;base64,${encoded}#${moduleSequence}`)
  } finally {
    delete globalThis[apiKey]
  }
}

function createSettingsApi(getAll) {
  return {
    getAll,
    save: async () => ({ saved: [] }),
    onChanged: () => () => undefined
  }
}

function createDeferred() {
  let resolvePromise
  let rejectPromise
  const promise = new Promise((resolve, reject) => {
    resolvePromise = resolve
    rejectPromise = reject
  })
  return {
    promise,
    resolve(value) {
      if (resolvePromise === undefined) throw new Error('Deferred promise was not initialized')
      resolvePromise(value)
    },
    reject(error) {
      if (rejectPromise === undefined) throw new Error('Deferred promise was not initialized')
      rejectPromise(error)
    }
  }
}

test('partial settings loads merge only the requested scopes', async () => {
  const cachedOsd = { Enabled: true }
  const loadedApplication = { AnimationsEnabled: false }
  const settingsApi = createSettingsApi(async () => ({
    scopes: {
      application: loadedApplication,
      osd: { Enabled: false }
    }
  }))
  const { useSettingsStore } = await loadSettingsStore(settingsApi)
  useSettingsStore.setState({
    scopes: {
      application: { AnimationsEnabled: true },
      osd: cachedOsd
    }
  })

  await useSettingsStore.getState().load(['application'])

  assert.deepEqual(useSettingsStore.getState().scopes, {
    application: loadedApplication,
    osd: cachedOsd
  })
})

test('older settings responses cannot overwrite a newer response', async () => {
  const olderResponse = createDeferred()
  const newerResponse = createDeferred()
  let requestCount = 0
  const settingsApi = createSettingsApi(() => {
    requestCount += 1
    return requestCount === 1 ? olderResponse.promise : newerResponse.promise
  })
  const { useSettingsStore } = await loadSettingsStore(settingsApi)

  const olderLoad = useSettingsStore.getState().load(['application'])
  const newerLoad = useSettingsStore.getState().load(['application'])

  newerResponse.resolve({ scopes: { application: { revision: 2 } } })
  await newerLoad
  olderResponse.resolve({ scopes: { application: { revision: 1 } } })
  await olderLoad

  assert.deepEqual(useSettingsStore.getState().scopes.application, { revision: 2 })
  assert.equal(useSettingsStore.getState().loading, false)
})

test('setScope protects an optimistic edit from an older partial response', async () => {
  const olderResponse = createDeferred()
  let requestCount = 0
  const settingsApi = createSettingsApi(() => {
    requestCount += 1
    return requestCount === 1
      ? olderResponse.promise
      : Promise.resolve({ scopes: { application: { revision: 'refreshed' } } })
  })
  const { useSettingsStore } = await loadSettingsStore(settingsApi)

  const olderLoad = useSettingsStore.getState().load(['application'])
  useSettingsStore.getState().setScope('application', { revision: 'optimistic' })
  olderResponse.resolve({ scopes: { application: { revision: 'stale' } } })
  await olderLoad

  assert.deepEqual(useSettingsStore.getState().scopes.application, { revision: 'optimistic' })

  await useSettingsStore.getState().load(['application'])
  assert.deepEqual(useSettingsStore.getState().scopes.application, { revision: 'refreshed' })
})

test('setScope protects an optimistic edit from an older full response', async () => {
  const fullResponse = createDeferred()
  const settingsApi = createSettingsApi(() => fullResponse.promise)
  const { useSettingsStore } = await loadSettingsStore(settingsApi)

  const fullLoad = useSettingsStore.getState().load()
  useSettingsStore.getState().setScope('osd', { revision: 'optimistic' })
  fullResponse.resolve({
    scopes: {
      application: { revision: 'host' },
      osd: { revision: 'stale' }
    }
  })
  await fullLoad

  assert.deepEqual(useSettingsStore.getState().scopes, {
    application: { revision: 'host' },
    osd: { revision: 'optimistic' }
  })
})

test('concurrent disjoint partial loads both merge regardless of response order', async () => {
  const applicationResponse = createDeferred()
  const osdResponse = createDeferred()
  const settingsApi = createSettingsApi((scopes) =>
    scopes.includes('application') ? applicationResponse.promise : osdResponse.promise
  )
  const { useSettingsStore } = await loadSettingsStore(settingsApi)
  useSettingsStore.setState({ scopes: { dashboard: { layout: 'existing' } } })

  const applicationLoad = useSettingsStore.getState().load(['application'])
  const osdLoad = useSettingsStore.getState().load(['osd'])

  osdResponse.resolve({ scopes: { osd: { Enabled: true } } })
  await osdLoad
  assert.equal(useSettingsStore.getState().loading, true)

  applicationResponse.resolve({ scopes: { application: { AnimationsEnabled: false } } })
  await applicationLoad

  assert.deepEqual(useSettingsStore.getState().scopes, {
    dashboard: { layout: 'existing' },
    osd: { Enabled: true },
    application: { AnimationsEnabled: false }
  })
  assert.equal(useSettingsStore.getState().loading, false)
})

test('loading remains true until concurrent rejected requests settle', async () => {
  const applicationResponse = createDeferred()
  const osdResponse = createDeferred()
  const settingsApi = createSettingsApi((scopes) =>
    scopes.includes('application') ? applicationResponse.promise : osdResponse.promise
  )
  const { useSettingsStore } = await loadSettingsStore(settingsApi)

  const applicationLoad = useSettingsStore.getState().load(['application'])
  const osdLoad = useSettingsStore.getState().load(['osd'])

  osdResponse.reject(new Error('osd failed'))
  await assert.rejects(osdLoad, /osd failed/)
  assert.equal(useSettingsStore.getState().loading, true)

  applicationResponse.resolve({ scopes: { application: { revision: 1 } } })
  await applicationLoad
  assert.equal(useSettingsStore.getState().loading, false)
})

test('save without scopes persists only scopes already in the store', async () => {
  const saved = []
  const settingsApi = {
    ...createSettingsApi(async () => ({ scopes: { application: { AnimationsEnabled: true } } })),
    save: async (scopes) => {
      saved.push(scopes)
      return { saved: scopes ?? [] }
    }
  }
  const { useSettingsStore } = await loadSettingsStore(settingsApi)
  useSettingsStore.setState({
    scopes: {
      application: { AnimationsEnabled: true }
    }
  })

  await useSettingsStore.getState().save()

  assert.deepEqual(saved, [['application']])
})

test('settings sync loads valid uncached scopes and ignores invalid scopes', async () => {
  const requestedScopes = []
  let emitChanged = () => {
    throw new Error('Settings listener was not initialized')
  }
  const settingsApi = {
    ...createSettingsApi(async (scopes) => {
      requestedScopes.push(scopes)
      return { scopes: { osd: { Enabled: true } } }
    }),
    onChanged: (callback) => {
      emitChanged = callback
      return () => undefined
    }
  }
  const { initSettingsSync, useSettingsStore } = await loadSettingsStore(settingsApi)
  const stopSync = initSettingsSync()

  emitChanged({ scope: 'not-a-scope', reason: 'test' })
  emitChanged({ scope: 'osd', reason: 'test' })
  await Promise.resolve()

  assert.deepEqual(requestedScopes, [['osd']])
  assert.deepEqual(useSettingsStore.getState().scopes.osd, { Enabled: true })
  stopSync()
})
