import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import vm from 'node:vm'
import ts from 'typescript'
import {
  hostSidecarPath,
  isDevHostLayoutReady,
  isRunnableHost,
  listHostCandidates,
  networkProxyPathBesideHost,
  PORTABLE_HOST_TFM
} from '../scripts/host-sidecar.mjs'

test('hostSidecarPath strips .exe on Windows', () => {
  const exe = 'C:\\out\\UniversalDeviceToolkit.Host.exe'
  assert.equal(hostSidecarPath(exe, 'runtimeconfig.json', 'win32'), 'C:\\out\\UniversalDeviceToolkit.Host.runtimeconfig.json')
  assert.equal(hostSidecarPath(exe, 'deps.json', 'win32'), 'C:\\out\\UniversalDeviceToolkit.Host.deps.json')
})

test('hostSidecarPath appends extension on Unix', () => {
  const exe = '/opt/udt/UniversalDeviceToolkit.Host'
  assert.equal(
    hostSidecarPath(exe, 'runtimeconfig.json', 'linux'),
    '/opt/udt/UniversalDeviceToolkit.Host.runtimeconfig.json'
  )
})

test('isRunnableHost requires exe plus runtimeconfig and deps', () => {
  const exe = 'C:\\out\\UniversalDeviceToolkit.Host.exe'
  const present = new Set([exe])
  const exists = (path) => present.has(path)

  assert.equal(isRunnableHost(exe, exists, 'win32'), false)

  present.add('C:\\out\\UniversalDeviceToolkit.Host.runtimeconfig.json')
  assert.equal(isRunnableHost(exe, exists, 'win32'), false)

  present.add('C:\\out\\UniversalDeviceToolkit.Host.deps.json')
  assert.equal(isRunnableHost(exe, exists, 'win32'), true)
})

test('isRunnableHost accepts Unix FDD with sidecars', () => {
  const exe = '/repo/Apps/Host/bin/Debug/net10.0/UniversalDeviceToolkit.Host'
  const present = new Set([
    exe,
    `${exe}.dll`,
    `${exe}.runtimeconfig.json`,
    `${exe}.deps.json`
  ])
  assert.equal(isRunnableHost(exe, (path) => present.has(path), 'linux'), true)
})

test('isRunnableHost rejects Unix FDD apphost that is missing sidecars', () => {
  const exe = '/repo/Apps/Host/bin/Debug/net10.0/UniversalDeviceToolkit.Host'
  const present = new Set([exe, `${exe}.dll`])
  assert.equal(isRunnableHost(exe, (path) => present.has(path), 'linux'), false)
})

test('isRunnableHost accepts Unix single-file publish without a sibling dll', () => {
  const exe = '/opt/udt/publish/linux-x64/UniversalDeviceToolkit.Host'
  const present = new Set([exe])
  assert.equal(isRunnableHost(exe, (path) => present.has(path), 'linux'), true)
})

test('networkProxyPathBesideHost keeps the Host directory', () => {
  assert.equal(
    networkProxyPathBesideHost('C:\\out\\UniversalDeviceToolkit.Host.exe', 'win32'),
    'C:\\out\\UniversalDeviceToolkit.NetworkProxy.exe'
  )
  assert.equal(
    networkProxyPathBesideHost('/opt/udt/UniversalDeviceToolkit.Host', 'linux'),
    '/opt/udt/UniversalDeviceToolkit.NetworkProxy'
  )
})

test('isDevHostLayoutReady requires Host and NetworkProxy sidecars', () => {
  const host = 'C:\\out\\UniversalDeviceToolkit.Host.exe'
  const worker = 'C:\\out\\UniversalDeviceToolkit.NetworkProxy.exe'
  const present = new Set([
    host,
    'C:\\out\\UniversalDeviceToolkit.Host.runtimeconfig.json',
    'C:\\out\\UniversalDeviceToolkit.Host.deps.json'
  ])
  const exists = (path) => present.has(path)

  assert.equal(isDevHostLayoutReady(host, exists, 'win32'), false)

  present.add(worker)
  present.add('C:\\out\\UniversalDeviceToolkit.NetworkProxy.runtimeconfig.json')
  present.add('C:\\out\\UniversalDeviceToolkit.NetworkProxy.deps.json')
  assert.equal(isDevHostLayoutReady(host, exists, 'win32'), true)
})

test('isDevHostLayoutReady on Linux does not require NetworkProxy', () => {
  const host = '/repo/Apps/Host/bin/Debug/net10.0/UniversalDeviceToolkit.Host'
  const present = new Set([
    host,
    `${host}.dll`,
    `${host}.runtimeconfig.json`,
    `${host}.deps.json`
  ])
  assert.equal(isDevHostLayoutReady(host, (path) => present.has(path), 'linux'), true)
})

test('listHostCandidates includes portable net10.0 Debug output on Linux', () => {
  const candidates = listHostCandidates({
    electronRoot: '/workspace/Apps/Electron',
    platform: 'linux',
    arch: 'x64'
  })
  assert.ok(candidates.some((path) => path.replaceAll('\\', '/').includes(`/bin/Debug/${PORTABLE_HOST_TFM}/UniversalDeviceToolkit.Host`)))
  assert.ok(candidates.some((path) => path.replaceAll('\\', '/').includes('/publish/linux-x64/UniversalDeviceToolkit.Host')))
  assert.ok(candidates.some((path) => path.replaceAll('\\', '/').startsWith('/workspace/Apps/Host/bin/')))
})

test('Electron main resolveHostPath looks at portable net10.0 on Linux', () => {
  const source = readFileSync(new URL('../src/main/index.ts', import.meta.url), 'utf8')
  assert.match(source, /portableTfm = 'net10\.0'/)
  assert.match(source, /bin', 'Debug', portableTfm/)
  assert.doesNotMatch(
    source,
    /process\.platform === 'linux'[\s\S]{0,200}net10\.0-windows10\.0\.26100\.0/
  )
})

const nodeRequire = createRequire(import.meta.url)
const hostClientUrl = new URL('../src/main/host-client.ts', import.meta.url)

function compileModule(fileUrl) {
  const fileName = fileURLToPath(fileUrl)
  const result = ts.transpileModule(readFileSync(fileUrl, 'utf8'), {
    fileName,
    reportDiagnostics: true,
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022
    }
  })
  const errors = (result.diagnostics ?? []).filter(
    (diagnostic) => diagnostic.category === ts.DiagnosticCategory.Error
  )
  if (errors.length > 0) {
    throw new Error(errors.map((diagnostic) => ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n')).join('\n'))
  }
  return result.outputText
}

function loadHostClient(mocks = {}) {
  const module = { exports: {} }
  const sourcePath = fileURLToPath(hostClientUrl)
  vm.runInNewContext(compileModule(hostClientUrl), {
    exports: module.exports,
    module,
    require(specifier) {
      if (Object.prototype.hasOwnProperty.call(mocks, specifier)) return mocks[specifier]
      return nodeRequire(specifier)
    },
    console,
    Buffer,
    setTimeout,
    clearTimeout
  }, { filename: sourcePath })
  return module.exports
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

function waitForEvent(client, eventName) {
  return new Promise((resolve) => {
    const unsubscribe = client.on(eventName, (data) => {
      unsubscribe()
      resolve(data)
    })
  })
}

function waitForHostFailure(client) {
  return new Promise((resolve) => {
    const unsubscribe = client.onAny((event, data) => {
      if (event !== 'host.error' && event !== 'host.exited') return
      unsubscribe()
      resolve({ event, data })
    })
  })
}

class FakeHostProcess {
  constructor() {
    this.killed = false
    this.listeners = new Map()
    this.stdin = {
      writable: true,
      write: () => true
    }
    this.stdout = {
      on() {},
      resume() {},
      pause() {}
    }
    this.stderr = {
      on: (event, callback) => {
        this.on(event === 'data' ? 'stderr-data' : event, callback)
        return this
      }
    }
  }

  on(event, callback) {
    if (!this.listeners.has(event)) this.listeners.set(event, [])
    this.listeners.get(event).push(callback)
    return this
  }

  once(event, callback) {
    const wrap = (...args) => {
      this.off(event, wrap)
      callback(...args)
    }
    return this.on(event, wrap)
  }

  off(event, callback) {
    const list = this.listeners.get(event)
    if (!list) return this
    const index = list.indexOf(callback)
    if (index >= 0) list.splice(index, 1)
    return this
  }

  emit(event, ...args) {
    const list = [...(this.listeners.get(event) ?? [])]
    for (const callback of list) callback(...args)
  }

  kill() {
    this.killed = true
    this.emit('exit', 1, null)
  }
}

function createFakeHostRuntime() {
  const children = []
  const lines = []
  let closed = 0

  const childProcess = {
    spawn() {
      const child = new FakeHostProcess()
      children.push(child)
      return child
    }
  }

  const readline = {
    createInterface() {
      return {
        on(event, callback) {
          if (event === 'line') lines.push(callback)
        },
        close() {
          closed += 1
        }
      }
    }
  }

  return {
    children,
    closed: () => closed,
    emitLine(line) {
      for (const callback of lines) callback(line)
    },
    mocks: { child_process: childProcess, readline }
  }
}

const missingHostPath = process.platform === 'win32'
  ? 'C:\\udt-missing-host-spawn-lifecycle.exe'
  : '/tmp/udt-missing-host-spawn-lifecycle'

test('spawn error finalizes the phantom child so start can run again', async () => {
  const { HostClient } = loadHostClient()
  const client = new HostClient({ readyTimeoutMs: 2_000, restartDelayMs: 60_000, stopGraceMs: 50 })
  const failed = waitForHostFailure(client)

  client.start(missingHostPath)
  await failed
  assert.equal(client.isRunning, false)
  assert.match(String(client.lastFailure), /spawn failed|exited/i)
  await assert.rejects(client.waitUntilReady(200), /spawn failed|exited/i)

  client.start(missingHostPath)
  await waitForHostFailure(client)
  assert.equal(client.isRunning, false)
  await client.stop()
})

test('spawn error then exit is idempotent and does not leave a running handle', async () => {
  const runtime = createFakeHostRuntime()
  const { HostClient } = loadHostClient(runtime.mocks)
  const client = new HostClient({ readyTimeoutMs: 2_000, restartDelayMs: 60_000, stopGraceMs: 50 })
  const events = []
  client.onAny((event) => events.push(event))

  client.start('C:\\fake-host.exe')
  assert.equal(runtime.children.length, 1)
  const child = runtime.children[0]

  child.emit('error', new Error('ENOENT'))
  assert.equal(client.isRunning, false)
  assert.equal(runtime.closed(), 1)
  assert.equal(events.filter((event) => event === 'host.error').length, 1)

  child.emit('exit', 1, null)
  child.emit('error', new Error('ENOENT'))
  assert.equal(client.isRunning, false)
  assert.equal(runtime.closed(), 1)
  assert.equal(events.filter((event) => event === 'host.error').length, 1)
  assert.equal(events.filter((event) => event === 'host.exited').length, 0)
  await client.stop()
})

test('boot watchdog kills a Host that never becomes ready', async () => {
  const { HostClient } = loadHostClient()
  const client = new HostClient({ readyTimeoutMs: 250, restartDelayMs: 60_000, stopGraceMs: 50 })
  const failed = waitForEvent(client, 'host.error')

  client.start(process.execPath, ['-e', 'setInterval(() => {}, 1000)'])
  const payload = await failed
  assert.match(String(payload.message), /did not become ready in time/)
  assert.equal(client.isRunning, false)
  await assert.rejects(client.waitUntilReady(200), /did not become ready in time/)
  await client.stop()
})

test('host.ready disarms the boot watchdog', async () => {
  const runtime = createFakeHostRuntime()
  const { HostClient } = loadHostClient(runtime.mocks)
  const client = new HostClient({ readyTimeoutMs: 120, restartDelayMs: 60_000, stopGraceMs: 50 })

  client.start('C:\\fake-host.exe')
  const child = runtime.children[0]
  runtime.emitLine(JSON.stringify({ event: 'host.ready', data: { version: 'test' } }))

  assert.equal(client.isReady, true)
  await delay(200)
  assert.equal(child.killed, false)
  assert.equal(client.isRunning, true)
  await client.stop()
})

test('late exit from a finalized child does not drop the next child', async () => {
  const runtime = createFakeHostRuntime()
  const { HostClient } = loadHostClient(runtime.mocks)
  const client = new HostClient({ readyTimeoutMs: 2_000, restartDelayMs: 60_000, stopGraceMs: 50 })

  client.start('C:\\fake-host-one.exe')
  const first = runtime.children[0]
  first.emit('error', new Error('ENOENT'))
  assert.equal(client.isRunning, false)

  client.start('C:\\fake-host-two.exe')
  assert.equal(runtime.children.length, 2)
  const second = runtime.children[1]
  runtime.emitLine(JSON.stringify({ event: 'host.ready', data: { generation: 2 } }))
  assert.equal(client.isReady, true)

  first.emit('exit', 1, null)
  first.kill()
  assert.equal(client.isRunning, true)
  assert.equal(client.isReady, true)
  assert.equal(second.killed, false)
  await client.stop()
})
