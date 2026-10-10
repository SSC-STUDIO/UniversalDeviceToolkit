import { app } from 'electron'
import { appendFileSync, existsSync, mkdirSync, renameSync, statSync } from 'fs'
import { join } from 'path'

/**
 * Main-process logger: leveled lines appended to
 * %LOCALAPPDATA%/UniversalDeviceToolkit/logs/main.log (same folder as Host)
 * with a 2 MB rotation, mirrored to the console.
 * Renderer logs arrive via the `log:write` IPC channel and land in
 * renderer.log; host log lines (host.log events) land in host.log — all
 * three live in the same folder so "Open log folder" shows everything.
 * Override with UDT_LOG_PATH (preferred) or LLT_LOG_PATH.
 */

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error'

const MAX_LOG_BYTES = 2 * 1024 * 1024

let logDir: string | null = null
let currentFile: string | null = null
let currentSize = 0

function logsDirectory(): string {
  if (logDir === null) {
    const override = process.env['UDT_LOG_PATH'] ?? process.env['LLT_LOG_PATH']
    if (typeof override === 'string' && override.length > 0) {
      logDir = override
    } else if (process.platform === 'win32') {
      const appData =
        process.env['UDT_APPDATA_OVERRIDE'] ??
        join(process.env['LOCALAPPDATA'] ?? app.getPath('userData'), 'UniversalDeviceToolkit')
      logDir = join(appData, 'logs')
    } else {
      logDir = join(app.getPath('userData'), 'logs')
    }
    mkdirSync(logDir, { recursive: true })
  }
  return logDir
}

function rotateIfNeeded(target: string): void {
  try {
    if (existsSync(target) && statSync(target).size >= MAX_LOG_BYTES) {
      renameSync(target, `${target}.old`)
    }
  } catch {
    // Rotation is best-effort.
  }
}

function writeLine(level: LogLevel, message: string, target?: string): void {
  const stamp = new Date().toISOString()
  const line = `${stamp} [${level.toUpperCase().padEnd(5)}] ${message}\n`
  const file = target ?? currentFile
  if (file !== null) {
    try {
      currentSize += Buffer.byteLength(line, 'utf8')
      if (currentSize >= MAX_LOG_BYTES) {
        rotateIfNeeded(file)
        currentSize = 0
      }
      appendFileSync(file, line, 'utf8')
    } catch {
      // Logging must never take the app down.
    }
  }
  // Mirror to the console so dev sessions see the same stream.
  const consoleFn = level === 'error' ? console.error : level === 'warn' ? console.warn : console.log
  consoleFn(`[${level}] ${message}`)
}

/** Initializes the main log file (called once after app ready). */
export function initMainLogger(): void {
  if (currentFile !== null) return
  const dir = logsDirectory()
  currentFile = join(dir, 'main.log')
  try {
    currentSize = existsSync(currentFile) ? statSync(currentFile).size : 0
  } catch {
    currentSize = 0
  }
  writeLine('info', 'main logger initialized')
}

/** Writes a renderer-originated log line to renderer.log. */
export function writeRendererLog(level: LogLevel, message: string): void {
  const file = join(logsDirectory(), 'renderer.log')
  writeLine(level, `[renderer] ${message}`, file)
}

/** Writes a host log line (host.log event) to host.log. */
export function writeHostLog(message: string): void {
  const file = join(logsDirectory(), 'host.log')
  writeLine('info', message, file)
}

export { logsDirectory }

/** Shortcut used by IPC handlers. */
export function isValidLogLevel(value: unknown): value is LogLevel {
  return value === 'trace' || value === 'debug' || value === 'info' || value === 'warn' || value === 'error'
}
