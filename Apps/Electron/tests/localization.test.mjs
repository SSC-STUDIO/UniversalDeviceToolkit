import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'
import { setImmediate } from 'node:timers'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'

function compileModule(fileUrl, mocks) {
  const fileName = fileURLToPath(fileUrl)
  const result = ts.transpileModule(readFileSync(fileUrl, 'utf8'), {
    fileName,
    compilerOptions: {
      esModuleInterop: true,
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  })
  const module = { exports: {} }
  const context = {
    console: { warn: () => undefined },
    exports: module.exports,
    module,
    require(specifier) {
      if (Object.prototype.hasOwnProperty.call(mocks, specifier)) return mocks[specifier]
      throw new Error(`Unexpected import "${specifier}" from ${fileName}`)
    }
  }
  new vm.Script(result.outputText, { filename: fileName }).runInNewContext(context)
  return module.exports
}

function flush() {
  return new Promise((resolve) => setImmediate(resolve))
}

const localizationUrl = new URL('../src/renderer/src/api/localization.ts', import.meta.url)

test('renderer culture codes map to canonical Host cultures', () => {
  const localization = compileModule(localizationUrl, {
    './bridge': {
      getHostStatus: async () => ({ ready: false }),
      invoke: async () => ({ culture: 'en' }),
      on: () => () => undefined
    }
  })

  assert.equal(localization.hostCultureForLanguage('zh-CN'), 'zh-Hans')
  assert.equal(localization.hostCultureForLanguage('de'), 'de')
})

test('Host culture synchronization retries when host.ready arrives', async () => {
  let ready = false
  let readyHandler = null
  const calls = []
  const localization = compileModule(localizationUrl, {
    './bridge': {
      getHostStatus: async () => ({ ready }),
      invoke: async (method, params) => {
        calls.push({ method, params })
        return { culture: params.culture }
      },
      on: (event, callback) => {
        assert.equal(event, 'host.ready')
        readyHandler = callback
        return () => undefined
      }
    }
  })

  localization.registerHostCultureRetry(() => 'de')
  assert.equal(await localization.syncCultureToHost('de'), false)
  assert.equal(calls.length, 0)

  ready = true
  readyHandler()
  await flush()
  await flush()

  assert.equal(calls.length, 1)
  assert.equal(calls[0].method, 'localization.setCulture')
  assert.equal(calls[0].params.culture, 'de')
})

test('latest culture selection wins when Host requests overlap', async () => {
  let resolveFirst
  const calls = []
  const localization = compileModule(localizationUrl, {
    './bridge': {
      getHostStatus: async () => ({ ready: true }),
      invoke: async (method, params) => {
        calls.push({ method, params })
        if (calls.length === 1) {
          return new Promise((resolve) => {
            resolveFirst = resolve
          })
        }
        return { culture: params.culture }
      },
      on: () => () => undefined
    }
  })

  const first = localization.syncCultureToHost('de')
  await flush()
  const second = localization.syncCultureToHost('fr')
  await flush()
  resolveFirst({ culture: 'de' })

  assert.equal(await first, false)
  await flush()
  assert.equal(await second, true)
  assert.deepEqual(calls.map((call) => call.params.culture), ['de', 'fr'])
})

test('date formatting uses the active UI language', () => {
  const date = new Date(2024, 0, 2)
  const dateFormat = compileModule(
    new URL('../src/renderer/src/utils/dateFormat.ts', import.meta.url),
    {
      '../i18n': { language: 'de', resolvedLanguage: 'de' }
    }
  )

  assert.equal(dateFormat.formatDateForUi(date), date.toLocaleDateString('de'))
})
