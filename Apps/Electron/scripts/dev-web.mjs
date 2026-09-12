/**
 * Starts the dev HTTP bridge (Host stdio proxy) and the renderer Vite dev server.
 */
import { spawn } from 'child_process'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const PROJECT_ROOT = join(__dirname, '..')
const bridgePort = process.env.UDT_DEV_BRIDGE_PORT ?? '17831'
const bridgeUrl = process.env.VITE_DEV_BRIDGE_URL ?? `http://127.0.0.1:${bridgePort}`

process.env.VITE_DEV_BRIDGE_URL = bridgeUrl

async function isBridgeRunning(url) {
  try {
    const res = await fetch(`${url}/status`, { signal: AbortSignal.timeout(2000) })
    if (!res.ok) return false
    const data = await res.json()
    return typeof data === 'object' && data !== null && ('ready' in data || 'running' in data)
  } catch {
    return false
  }
}

/** @type {import('child_process').ChildProcess | null} */
let bridge = null

const viteBin = join(PROJECT_ROOT, 'node_modules', 'vite', 'bin', 'vite.js')
/** @type {import('child_process').ChildProcess | null} */
let vite = null

let exiting = false

function shutdown(code = 0) {
  if (exiting) return
  exiting = true
  bridge?.kill('SIGINT')
  vite?.kill('SIGINT')
  setTimeout(() => process.exit(code), 500)
}

async function main() {
  if (await isBridgeRunning(bridgeUrl)) {
    console.log(`[dev:web] reusing existing bridge at ${bridgeUrl}`)
  } else {
    const bridgeArgs = ['scripts/dev-bridge-server.mjs', ...process.argv.slice(2)]
    bridge = spawn('node', bridgeArgs, {
      cwd: PROJECT_ROOT,
      stdio: 'inherit',
      env: process.env
    })

    bridge.on('exit', (code) => {
      if (!exiting) shutdown(code ?? 1)
    })

    // Wait briefly for the bridge to bind before Vite starts issuing invokes.
    for (let attempt = 0; attempt < 30; attempt++) {
      if (await isBridgeRunning(bridgeUrl)) break
      await new Promise((resolve) => setTimeout(resolve, 200))
    }
  }

  vite = spawn(process.execPath, [viteBin, '--config', 'vite.web.config.ts'], {
    cwd: PROJECT_ROOT,
    stdio: 'inherit',
    env: process.env
  })

  vite.on('exit', (code) => {
    if (!exiting) shutdown(code ?? 1)
  })

  console.log(`[dev:web] bridge URL: ${bridgeUrl}`)
  console.log(`[dev:web] renderer: http://127.0.0.1:5173`)
}

process.on('SIGINT', () => shutdown(0))
process.on('SIGTERM', () => shutdown(0))

void main()
