import { spawn } from 'node:child_process'
import { createInterface } from 'node:readline'

const [hostPath] = process.argv.slice(2)
if (!hostPath) {
  throw new Error('Usage: node scripts/smoke-host.mjs <host-executable>')
}

const child = spawn(hostPath, [], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true })
let stderr = ''
let nextId = 1
let childError
const pending = new Map()
const output = createInterface({ input: child.stdout })

child.stderr.on('data', chunk => {
  stderr += chunk.toString()
})
child.once('error', error => {
  childError = error
  for (const request of pending.values()) request.reject(error)
  pending.clear()
})

output.on('line', line => {
  let message
  try {
    message = JSON.parse(line)
  } catch {
    return
  }
  if (typeof message.id !== 'number') return
  const request = pending.get(message.id)
  if (!request) return
  pending.delete(message.id)
  if (message.error) request.reject(new Error(`Host RPC ${message.error.code}: ${message.error.message}`))
  else request.resolve(message.result)
})

function invoke(method) {
  if (childError) return Promise.reject(childError)
  const id = nextId++
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      pending.delete(id)
      reject(new Error(`Host RPC timed out: ${method}; stderr=${stderr}`))
    }, 30_000)
    pending.set(id, {
      resolve(value) {
        clearTimeout(timer)
        resolve(value)
      },
      reject(error) {
        clearTimeout(timer)
        reject(error)
      }
    })
    child.stdin.write(`${JSON.stringify({ id, method, params: {} })}\n`)
  })
}

function waitForExit() {
  if (childError) return Promise.reject(childError)
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      child.kill()
      reject(new Error(`Host did not exit after app.quit; stderr=${stderr}`))
    }, 30_000)
    const rejectForError = error => {
      clearTimeout(timer)
      reject(error)
    }
    child.once('error', rejectForError)
    child.once('exit', code => {
      clearTimeout(timer)
      child.off('error', rejectForError)
      if (code === 0) resolve()
      else reject(new Error(`Host exited with code ${code}; stderr=${stderr}`))
    })
  })
}

try {
  const ping = await invoke('ping')
  if (!ping || ping.pong !== true) throw new Error(`Host ping returned an unexpected payload: ${JSON.stringify(ping)}`)
  const exit = waitForExit()
  await invoke('app.quit')
  await exit
} finally {
  output.close()
  if (!child.killed) child.kill()
}
