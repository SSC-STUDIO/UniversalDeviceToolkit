/**
 * System power actions — Electron-side counterpart of the Electron Lib power
 * controller (restart/shutdown/sleep).
 *
 * - Windows: `shutdown.exe` handles elevation the same way the Electron client
 *   does (process elevation for power actions).
 * - macOS: AppleScript via `osascript` for restart/shutdown, `pmset sleepnow`
 *   for sleep; no elevation needed.
 * - Linux: `systemctl` (systemd); returns an error result when the tool is
 *   unavailable or the call fails.
 */
import { spawn } from 'child_process'

export interface PowerActionResult {
  ok: boolean
  error?: string
}

function singleSettlement(
  resolve: (result: PowerActionResult) => void
): (result: PowerActionResult) => void {
  let settled = false
  return (result) => {
    if (settled) return
    settled = true
    resolve(result)
  }
}

function runWindowsShutdown(args: string[]): Promise<PowerActionResult> {
  return new Promise((resolve) => {
    const settle = singleSettlement(resolve)
    const child = spawn('shutdown.exe', args, {
      windowsHide: true,
      detached: true,
      stdio: 'ignore'
    })
    child.once('error', (error) => settle({ ok: false, error: error.message }))
    child.once('exit', (code) => settle({ ok: code === 0 }))
  })
}

/** Spawn a platform command and resolve with its exit status. */
function runCommand(command: string, args: string[]): Promise<PowerActionResult> {
  return new Promise((resolve) => {
    const settle = singleSettlement(resolve)
    const child = spawn(command, args, {
      windowsHide: true,
      stdio: 'ignore'
    })
    child.once('error', (error) => settle({ ok: false, error: error.message }))
    child.once('exit', (code) => {
      if (code === 0) {
        settle({ ok: true })
      } else {
        settle({ ok: false, error: `${command} exited with code ${code}` })
      }
    })
  })
}

function runOsxScript(command: 'restart' | 'shut down'): Promise<PowerActionResult> {
  return runCommand('osascript', ['-e', `tell app "System Events" to ${command}`])
}

const UNSUPPORTED_PLATFORM: PowerActionResult = {
  ok: false,
  error: 'Power actions are not supported on this platform.'
}

export function restartSystem(): Promise<PowerActionResult> {
  if (process.platform === 'win32') return runWindowsShutdown(['/r', '/t', '0'])
  if (process.platform === 'darwin') return runOsxScript('restart')
  if (process.platform === 'linux') return runCommand('systemctl', ['reboot'])
  return Promise.resolve(UNSUPPORTED_PLATFORM)
}

export function shutdownSystem(): Promise<PowerActionResult> {
  if (process.platform === 'win32') return runWindowsShutdown(['/s', '/t', '0'])
  if (process.platform === 'darwin') return runOsxScript('shut down')
  if (process.platform === 'linux') return runCommand('systemctl', ['poweroff'])
  return Promise.resolve(UNSUPPORTED_PLATFORM)
}

export function sleepSystem(): Promise<PowerActionResult> {
  if (process.platform === 'win32') return runWindowsShutdown(['/h'])
  // pmset sleepnow is the reliable macOS sleep trigger; AppleScript's
  // "System Events to sleep" is inconsistent across macOS versions.
  if (process.platform === 'darwin') return runCommand('pmset', ['sleepnow'])
  if (process.platform === 'linux') return runCommand('systemctl', ['suspend'])
  return Promise.resolve(UNSUPPORTED_PLATFORM)
}
