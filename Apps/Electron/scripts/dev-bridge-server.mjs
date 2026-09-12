/**
 * Dev-only HTTP bridge: spawns UniversalDeviceToolkit.Host over stdio and exposes
 * JSON-RPC invoke + SSE events for browser-based renderer debugging.
 *
 * Not used in production builds.
 */
import { spawn } from 'child_process'
import { createInterface } from 'readline'
import { createServer } from 'http'
import { existsSync } from 'fs'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'
import { isRunnableHost, listHostCandidates } from './host-sidecar.mjs'

const __dirname = dirname(fileURLToPath(import.meta.url))
const PROJECT_ROOT = join(__dirname, '..')
const DEFAULT_PORT = 17831
const PENDING_TIMEOUT_MS = 60_000
const READY_TIMEOUT_MS = 15_000

const port = Number(process.env.UDT_DEV_BRIDGE_PORT ?? DEFAULT_PORT)
const hostArgv = process.argv.slice(2)
const bridgeBaseUrl = `http://127.0.0.1:${port}`

/** Keep the JSON-RPC code in Error.message so the renderer can map it. */
function formatHostRpcError(error) {
  const code = typeof error?.code === 'number' ? error.code : -32603
  const text =
    typeof error?.message === 'string' && error.message.trim().length > 0
      ? error.message.trim()
      : 'Host error'
  return new Error(`[UDT:${code}] ${text}`)
}

/** @type {import('child_process').ChildProcessWithoutNullStreams | null} */
let child = null
/** @type {ReturnType<typeof createInterface> | null} */
let rl = null
/** @type {Map<number, { resolve: (v: unknown) => void, reject: (e: Error) => void, timer: ReturnType<typeof setTimeout> }>} */
const pending = new Map()
/** @type {Set<{ write: (chunk: string) => void, end: () => void }>} */
const sseClients = new Set()
let nextId = 1
let ready = false
let lastError = null
/** @type {unknown} */
let lastReadyData = null
/** @type {Array<{ resolve: () => void, reject: (e: Error) => void }>} */
const readyWaiters = []

function resolveHostPath() {
  const fromEnv = process.env.UDT_HOST_PATH
  if (fromEnv) {
    if (!existsSync(fromEnv)) {
      throw new Error(`UDT_HOST_PATH does not exist: ${fromEnv}`)
    }
    if (!isRunnableHost(fromEnv)) {
      throw new Error(`UDT_HOST_PATH is an incomplete Host (missing runtimeconfig.json/deps.json): ${fromEnv}`)
    }
    return fromEnv
  }

  const candidates = listHostCandidates({ electronRoot: PROJECT_ROOT })

  const incomplete = []
  for (const candidate of candidates) {
    if (!existsSync(candidate)) continue
    if (isRunnableHost(candidate)) return candidate
    incomplete.push(candidate)
  }

  const incompleteHint =
    incomplete.length === 0
      ? ''
      : `\nIncomplete Host (exe without runtimeconfig.json/deps.json; rebuild UniversalDeviceToolkit.Host -c Debug):\n` +
        incomplete.map((path) => `  - ${path}`).join('\n')

  throw new Error(
    `Host executable not found. Build UniversalDeviceToolkit.Host or set UDT_HOST_PATH.\n` +
      candidates.map((path) => `  - ${path}`).join('\n') +
      incompleteHint
  )
}

function toHostArgs(argv) {
  const args = []
  if (argv.includes('--trace')) args.push('--trace')
  if (argv.includes('--safe-start')) args.push('--safe-start')
  if (argv.includes('--no-hardware')) args.push('--no-hardware')
  if (argv.includes('--experimental-gpu-working-mode')) args.push('--experimental-gpu-working-mode')
  const proxyUrl = stringSwitch(argv, '--proxy-url')
  if (proxyUrl) args.push('--proxy-url', proxyUrl)
  const proxyUsername = stringSwitch(argv, '--proxy-username')
  if (proxyUsername) args.push('--proxy-username', proxyUsername)
  const proxyPassword = stringSwitch(argv, '--proxy-password')
  if (proxyPassword) args.push('--proxy-password', proxyPassword)
  if (argv.includes('--proxy-allow-all-certs')) args.push('--proxy-allow-all-certs')
  return args
}

function stringSwitch(argv, key) {
  for (let i = 0; i < argv.length; i++) {
    const value = argv[i]
    if (value.toLowerCase() === key) {
      const next = argv[i + 1]
      return next !== undefined && !next.startsWith('--') ? next : undefined
    }
    if (value.toLowerCase().startsWith(`${key}=`)) {
      return value.slice(key.length + 1)
    }
  }
  return undefined
}

function setReady(data) {
  ready = true
  lastReadyData = data
  lastError = null
  const waiters = readyWaiters.splice(0)
  for (const waiter of waiters) waiter.resolve()
}

function rejectReadyWaiters(error) {
  const waiters = readyWaiters.splice(0)
  for (const waiter of waiters) waiter.reject(error)
}

function broadcastEvent(event, data) {
  const payload = JSON.stringify({ event, data })
  for (const client of sseClients) {
    client.write(`data: ${payload}\n\n`)
  }
}

function emitSynthetic(event, data) {
  broadcastEvent(event, data)
}

function waitUntilReady(timeoutMs = READY_TIMEOUT_MS) {
  if (ready && child && !child.killed) return Promise.resolve()
  if (lastError && !child) return Promise.reject(new Error(lastError))

  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      remove()
      reject(new Error(lastError ?? 'Host did not become ready in time'))
    }, timeoutMs)

    const waiter = {
      resolve: () => {
        remove()
        resolve()
      },
      reject: (error) => {
        remove()
        reject(error)
      }
    }

    const remove = () => {
      clearTimeout(timer)
      const index = readyWaiters.indexOf(waiter)
      if (index >= 0) readyWaiters.splice(index, 1)
    }

    readyWaiters.push(waiter)
  })
}

function handleLine(line) {
  let message
  try {
    message = JSON.parse(line)
  } catch {
    return
  }
  if (!message || typeof message !== 'object') return

  if (typeof message.event === 'string') {
    if (message.event === 'host.ready') {
      setReady(message.data)
    }
    broadcastEvent(message.event, message.data)
    return
  }

  if (typeof message.id === 'number') {
    const request = pending.get(message.id)
    if (!request) return
    clearTimeout(request.timer)
    pending.delete(message.id)
    if (message.error) {
      request.reject(formatHostRpcError(message.error))
    } else {
      request.resolve(message.result)
    }
  }
}

function startHost() {
  const hostPath = resolveHostPath()
  const hostArgs = toHostArgs(hostArgv)
  ready = false
  lastError = null

  console.log(`[dev-bridge] spawning host: ${hostPath}${hostArgs.length > 0 ? ` ${hostArgs.join(' ')}` : ''}`)

  child = spawn(hostPath, hostArgs, {
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true
  })

  child.stderr.on('data', (d) => {
    console.error(`[host] ${d.toString().trim()}`)
  })

  child.on('error', (error) => {
    lastError = `Host spawn failed: ${error.message}`
    console.error(`[dev-bridge] spawn error: ${error.message}`)
    emitSynthetic('host.error', { message: lastError, fatal: true })
    rejectReadyWaiters(new Error(lastError))
  })

  rl = createInterface({ input: child.stdout })
  rl.on('line', handleLine)

  child.on('exit', (code, signal) => {
    const wasReady = ready
    ready = false
    console.error(`[dev-bridge] host exited code=${code} signal=${signal}`)
    const error = new Error(`Host exited (code=${code ?? 'n/a'} signal=${signal ?? 'n/a'})`)
    lastError = error.message
    for (const [, request] of pending) {
      clearTimeout(request.timer)
      request.reject(error)
    }
    pending.clear()
    rl?.close()
    rl = null
    child = null
    emitSynthetic('host.exited', { code, signal, wasReady })
    rejectReadyWaiters(error)
  })
}

async function invoke(method, params = {}) {
  if (!child || !child.stdin.writable) {
    throw new Error(lastError ?? 'Host is not running')
  }

  await waitUntilReady()

  if (!child || !child.stdin.writable) {
    throw new Error(lastError ?? 'Host is not running')
  }

  const id = nextId++
  const promise = new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      if (pending.delete(id)) {
        reject(new Error(`Host request timed out after ${PENDING_TIMEOUT_MS}ms: ${method}`))
      }
    }, PENDING_TIMEOUT_MS)
    pending.set(id, { resolve, reject, timer })
  })
  child.stdin.write(`${JSON.stringify({ id, method, params })}\n`)
  return promise
}

function getStatus() {
  return {
    running: child !== null && !child.killed,
    ready: ready && child !== null && !child.killed,
    lastError,
    readyPayload: lastReadyData
  }
}

function setCors(res) {
  res.setHeader('Access-Control-Allow-Origin', '*')
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type')
}

function readJsonBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = []
    req.on('data', (chunk) => chunks.push(chunk))
    req.on('end', () => {
      try {
        const raw = Buffer.concat(chunks).toString('utf8')
        resolve(raw.length > 0 ? JSON.parse(raw) : {})
      } catch (error) {
        reject(error)
      }
    })
    req.on('error', reject)
  })
}

function stopHost() {
  if (!child || child.killed) return
  try {
    if (child.stdin.writable) {
      child.stdin.write(`${JSON.stringify({ id: nextId++, method: 'app.quit', params: {} })}\n`)
    } else {
      child.kill()
    }
  } catch {
    child.kill()
  }
}

async function probeExistingBridge() {
  try {
    const res = await fetch(`${bridgeBaseUrl}/status`, { signal: AbortSignal.timeout(2000) })
    if (!res.ok) return false
    const data = await res.json()
    return typeof data === 'object' && data !== null && ('ready' in data || 'running' in data)
  } catch {
    return false
  }
}

const server = createServer(async (req, res) => {
  setCors(res)

  if (req.method === 'OPTIONS') {
    res.writeHead(204)
    res.end()
    return
  }

  const url = new URL(req.url ?? '/', `http://127.0.0.1:${port}`)

  if (req.method === 'GET' && url.pathname === '/status') {
    res.writeHead(200, { 'Content-Type': 'application/json' })
    res.end(JSON.stringify(getStatus()))
    return
  }

  if (req.method === 'GET' && url.pathname === '/events') {
    res.writeHead(200, {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      Connection: 'keep-alive'
    })
    res.write('\n')

    const client = {
      write: (chunk) => res.write(chunk),
      end: () => res.end()
    }
    sseClients.add(client)

    req.on('close', () => {
      sseClients.delete(client)
    })

    // Replay readiness for late subscribers.
    if (ready) {
      client.write(`data: ${JSON.stringify({ event: 'host.ready', data: lastReadyData })}\n\n`)
    }
    return
  }

  if (req.method === 'POST' && url.pathname === '/invoke') {
    try {
      const body = await readJsonBody(req)
      const method = body.method
      const params = body.params ?? {}
      if (typeof method !== 'string' || method.length === 0) {
        res.writeHead(400, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ error: { code: -32600, message: 'method is required' } }))
        return
      }
      const result = await invoke(method, params)
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({ result }))
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error)
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({ error: { code: -32603, message } }))
    }
    return
  }

  res.writeHead(404, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify({ error: { message: 'Not found' } }))
})

async function main() {
  if (await probeExistingBridge()) {
    console.log(`[dev-bridge] already running at ${bridgeBaseUrl} — reusing existing instance`)
    console.log(`[dev-bridge] to stop: npm run dev:bridge:stop`)
    return
  }

  server.on('error', async (error) => {
    if (error.code === 'EADDRINUSE') {
      if (await probeExistingBridge()) {
        console.log(`[dev-bridge] another instance is already listening on ${bridgeBaseUrl}`)
        process.exit(0)
      }
      console.error(`[dev-bridge] port ${port} is in use by another process (not the dev bridge).`)
      console.error(`[dev-bridge] free the port, run npm run dev:bridge:stop, or set UDT_DEV_BRIDGE_PORT.`)
      process.exit(1)
    }
    throw error
  })

  server.listen(port, '127.0.0.1', () => {
    startHost()
    console.log(`[dev-bridge] listening on ${bridgeBaseUrl}`)
    console.log(`[dev-bridge] status: GET /status`)
    console.log(`[dev-bridge] invoke: POST /invoke`)
    console.log(`[dev-bridge] events: GET /events (SSE)`)
  })
}

void main()

function shutdown() {
  console.log('[dev-bridge] shutting down')
  for (const client of sseClients) client.end()
  sseClients.clear()
  server.close()
  stopHost()
  setTimeout(() => process.exit(0), 500)
}

process.on('SIGINT', shutdown)
process.on('SIGTERM', shutdown)
