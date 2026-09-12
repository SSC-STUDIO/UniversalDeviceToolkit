/**
 * Stops the dev HTTP bridge by killing the process listening on UDT_DEV_BRIDGE_PORT.
 */
import { execFile } from 'child_process'
import { promisify } from 'util'

const execFileAsync = promisify(execFile)
const port = Number(process.env.UDT_DEV_BRIDGE_PORT ?? 17831)

async function findPidsWindows() {
  const { stdout } = await execFileAsync('netstat', ['-ano'], { windowsHide: true })
  const pids = new Set()
  for (const line of stdout.split(/\r?\n/)) {
    if (!line.includes(`127.0.0.1:${port}`) && !line.includes(`0.0.0.0:${port}`)) continue
    if (!line.includes('LISTENING')) continue
    const parts = line.trim().split(/\s+/)
    const pid = Number(parts[parts.length - 1])
    if (Number.isFinite(pid) && pid > 0) pids.add(pid)
  }
  return [...pids]
}

async function findPidsUnix() {
  const { stdout } = await execFileAsync('lsof', ['-ti', `tcp:${port}`])
  return stdout
    .split(/\r?\n/)
    .map((value) => Number(value.trim()))
    .filter((pid) => Number.isFinite(pid) && pid > 0)
}

async function killPid(pid) {
  if (process.platform === 'win32') {
    await execFileAsync('taskkill', ['/PID', String(pid), '/F'], { windowsHide: true })
  } else {
    process.kill(pid, 'SIGTERM')
  }
}

async function main() {
  let pids = []
  try {
    pids = process.platform === 'win32' ? await findPidsWindows() : await findPidsUnix()
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    console.error(`[dev-bridge:stop] failed to list listeners on port ${port}: ${message}`)
    process.exit(1)
  }

  if (pids.length === 0) {
    console.log(`[dev-bridge:stop] no process listening on port ${port}`)
    return
  }

  for (const pid of pids) {
    try {
      await killPid(pid)
      console.log(`[dev-bridge:stop] stopped PID ${pid} (port ${port})`)
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error)
      console.error(`[dev-bridge:stop] failed to stop PID ${pid}: ${message}`)
    }
  }
}

void main()
