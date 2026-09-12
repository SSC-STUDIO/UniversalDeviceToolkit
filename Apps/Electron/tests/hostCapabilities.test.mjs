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
    compilerOptions: { esModuleInterop: true, module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
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

const capabilitiesUrl = new URL('../src/renderer/src/api/hostCapabilities.ts', import.meta.url)

test('host capabilities payload is validated and preserved', async () => {
  const api = compileModule(capabilitiesUrl, {
    './bridge': {
      invokeObject: async () => ({
        platform: 'linux',
        portable: true,
        vendorHardware: false,
        capabilities: { optimization: false, sensors: true },
        backends: { configuration: true },
        implementedMethods: ['ping'],
        unsupportedMethods: ['network.getStatus']
      })
    }
  })

  const value = await api.getHostCapabilities()
  assert.equal(value.platform, 'linux')
  assert.equal(value.capabilities.optimization, false)
})

test('host capability sync refreshes when the host becomes ready', async () => {
  let readyHandler
  let loads = 0
  const storeModule = compileModule(
    new URL('../src/renderer/src/stores/hostCapabilitiesStore.ts', import.meta.url),
    {
      zustand: {
        create: () => (initializer) => {
          const state = { capabilities: null, loading: false, error: null }
          const set = (patch) => Object.assign(state, patch)
          const load = async () => {
              loads += 1
              set({ capabilities: { platform: 'macos', portable: true, capabilities: {} } })
          }
          initializer(() => {
            return { capabilities: null, loading: false, error: null, load }
          })
          const store = { getState: () => ({ ...state, load }) }
          return store
        }
      },
      '../api/hostCapabilities': {
        getHostCapabilities: async () => {
          loads += 1
          return { platform: 'macos', portable: true, capabilities: {} }
        }
      },
      '../api/bridge': {
        on: (event, handler) => {
          assert.equal(event, 'host.ready')
          readyHandler = handler
          return () => undefined
        }
      }
    }
  )

  const unsubscribe = storeModule.initHostCapabilitiesSync()
  await flush()
  readyHandler()
  await flush()
  await flush()
  unsubscribe()

  assert.equal(loads, 2)
  assert.equal(storeModule.useHostCapabilitiesStore.getState().capabilities.platform, 'macos')
})
