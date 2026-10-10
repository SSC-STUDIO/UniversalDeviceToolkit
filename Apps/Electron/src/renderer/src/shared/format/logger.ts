/**
 * Renderer logger: leveled logs double-written to the devtools console and
 * the main process via `bridge.log` (lands in <userData>/logs/renderer.log).
 */

export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error'

const LEVEL_PRIORITY: Record<LogLevel, number> = {
  trace: 10,
  debug: 20,
  info: 30,
  warn: 40,
  error: 50
}

/** Current minimum level — raise in production builds to reduce noise. */
const MIN_LEVEL: LogLevel = 'trace'

function write(level: LogLevel, args: unknown[]): void {
  if (LEVEL_PRIORITY[level] < LEVEL_PRIORITY[MIN_LEVEL]) return
  const message = args
    .map((arg) => {
      if (typeof arg === 'string') return arg
      if (arg instanceof Error) return arg.stack ?? `${arg.name}: ${arg.message}`
      try {
        return JSON.stringify(arg)
      } catch {
        return String(arg)
      }
    })
    .join(' ')
  const consoleFn =
    level === 'error' ? console.error : level === 'warn' ? console.warn : console.debug
  consoleFn(`[${level}]`, ...args)
  try {
    window.bridge?.log(level, message)
  } catch {
    // Logging must never break the app.
  }
}

export const logger = {
  trace: (...args: unknown[]): void => write('trace', args),
  debug: (...args: unknown[]): void => write('debug', args),
  info: (...args: unknown[]): void => write('info', args),
  warn: (...args: unknown[]): void => write('warn', args),
  error: (...args: unknown[]): void => write('error', args)
}

export default logger
