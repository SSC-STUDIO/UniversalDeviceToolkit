import { spawn, type ChildProcessWithoutNullStreams } from 'child_process'
import { createInterface } from 'readline'

interface PendingRequest {
  resolve: (value: unknown) => void
  reject: (error: Error) => void
  timer: NodeJS.Timeout
}

/** Hard cap for a single JSON-RPC request. Host operations are request/response
 *  (downloads and long tasks stream progress via events), so 60s is generous;
 *  a hung host must not leave the renderer's loading states spinning forever. */
const PENDING_TIMEOUT_MS = 60_000

interface HostLine {
  event?: string
  data?: unknown
  id?: number
  result?: unknown
  error?: { code: number; message: string }
}

/** Keep the JSON-RPC code in Error.message so IPC and the renderer can map it. */
export function formatHostRpcError(error: { code?: number; message?: string } | undefined): Error {
  const code = typeof error?.code === 'number' ? error.code : -32603
  const text = error?.message?.trim() || 'Host error'
  return new Error(`[UDT:${code}] ${text}`)
}

interface ReadyWaiter {
  resolve: () => void
  reject: (error: Error) => void
}

/** Host boot window: after this the UI must surface an error instead of spinning. */
const READY_TIMEOUT_MS = 15_000
const MAX_RESTART_ATTEMPTS = 3
const RESTART_DELAY_MS = 750
const STOP_GRACE_MS = 5_000

export interface HostClientOptions {
  readyTimeoutMs?: number
  restartDelayMs?: number
  stopGraceMs?: number
}

type FinalizeKind = 'error' | 'exit' | 'timeout'

/**
 * Manages the UniversalDeviceToolkit.Host child process and speaks the
 * newline-delimited JSON-RPC protocol over its stdio.
 */
export class HostClient {
  private child: ChildProcessWithoutNullStreams | null = null
  private rl: ReturnType<typeof createInterface> | null = null
  private pending = new Map<number, PendingRequest>()
  private listeners = new Map<string, Set<(data: unknown) => void>>()
  private anyListeners = new Set<(event: string, data: unknown) => void>()
  private readyWaiters: ReadyWaiter[] = []
  private nextId = 1
  private ready = false
  private stopping = false
  private starting = false
  private hostPath: string | null = null
  private hostArgs: string[] = []
  private lastError: string | null = null
  private restartAttempts = 0
  private restartTimer: ReturnType<typeof setTimeout> | null = null
  private bootWatchdog: ReturnType<typeof setTimeout> | null = null
  private lastReadyData: unknown = null
  private readonly readyTimeoutMs: number
  private readonly restartDelayMs: number
  private readonly stopGraceMs: number

  constructor(options: HostClientOptions = {}) {
    this.readyTimeoutMs = options.readyTimeoutMs ?? READY_TIMEOUT_MS
    this.restartDelayMs = options.restartDelayMs ?? RESTART_DELAY_MS
    this.stopGraceMs = options.stopGraceMs ?? STOP_GRACE_MS
  }

  on(event: string, callback: (data: unknown) => void): () => void {
    if (!this.listeners.has(event)) this.listeners.set(event, new Set())
    this.listeners.get(event)!.add(callback)
    return () => this.listeners.get(event)?.delete(callback)
  }

  /** Forward every host event (sensors.updated, settings.changed, etc.). */
  onAny(callback: (event: string, data: unknown) => void): () => void {
    this.anyListeners.add(callback)
    return () => this.anyListeners.delete(callback)
  }

  get isRunning(): boolean {
    return this.child !== null && !this.child.killed
  }

  get isReady(): boolean {
    return this.ready && this.isRunning
  }

  get lastReadyPayload(): unknown {
    return this.lastReadyData
  }

  get lastFailure(): string | null {
    return this.lastError
  }

  /** Record a fatal start failure (missing binary, etc.) before spawn runs. */
  reportFatalError(message: string): void {
    this.lastError = message
    this.ready = false
    this.emitSynthetic('host.error', { message, fatal: true })
    this.rejectReadyWaiters(new Error(message))
  }

  /**
   * Resolves once {@code host.ready} has been observed for the current child.
   * Rejects on timeout, spawn failure, or unexpected exit.
   */
  waitUntilReady(timeoutMs = this.readyTimeoutMs): Promise<void> {
    if (this.isReady) return Promise.resolve()
    if (this.lastError && !this.child && !this.starting) {
      return Promise.reject(new Error(this.lastError))
    }

    return new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => {
        remove()
        reject(new Error(this.lastError ?? 'Host did not become ready in time'))
      }, timeoutMs)

      const waiter: ReadyWaiter = {
        resolve: () => {
          remove()
          resolve()
        },
        reject: (error) => {
          remove()
          reject(error)
        }
      }

      const remove = (): void => {
        clearTimeout(timer)
        const index = this.readyWaiters.indexOf(waiter)
        if (index >= 0) this.readyWaiters.splice(index, 1)
      }

      this.readyWaiters.push(waiter)
    })
  }

  start(hostPath: string, args: string[] = []): void {
    if (this.child) throw new Error('Host already started')

    this.hostPath = hostPath
    this.hostArgs = args
    this.stopping = false
    this.starting = true
    this.ready = false
    this.lastError = null

    if (this.restartTimer) {
      clearTimeout(this.restartTimer)
      this.restartTimer = null
    }

    console.log(`[host] spawning: ${hostPath}${args.length > 0 ? ` ${args.join(' ')}` : ''}`)

    let child: ChildProcessWithoutNullStreams
    try {
      child = spawn(hostPath, args, {
        stdio: ['pipe', 'pipe', 'pipe'],
        windowsHide: true
      })
    } catch (error) {
      this.starting = false
      const message = `Host spawn failed: ${error instanceof Error ? error.message : String(error)}`
      this.lastError = message
      this.emitSynthetic('host.error', { message })
      this.rejectReadyWaiters(new Error(message))
      return
    }

    this.child = child

    child.stderr.on('data', (d: Buffer) => {
      console.error(`[host] ${d.toString().trim()}`)
    })

    child.on('error', (error) => {
      const failure = new Error(`Host spawn failed: ${error.message}`)
      this.finalizeChild(child, failure, { kind: 'error' })
      this.killChild(child)
    })

    this.rl = createInterface({ input: child.stdout })
    this.rl.on('line', (line) => this.handleLine(line))

    child.on('exit', (code, signal) => {
      const wasReady = this.ready && this.child === child
      const failure = new Error(`Host exited (code=${code ?? 'n/a'} signal=${signal ?? 'n/a'})`)
      this.finalizeChild(child, failure, { kind: 'exit', code, signal, wasReady })
    })

    this.armBootWatchdog(child)

    // Spawn returned — child handle exists; readiness still waits for host.ready.
    this.starting = false
  }

  async invoke(method: string, params: unknown = {}): Promise<unknown> {
    if (this.stopping) {
      return Promise.reject(new Error('Host is shutting down'))
    }

    if (!this.child || !this.child.stdin.writable) {
      if (this.hostPath && !this.stopping) {
        // Child gone (crash / first call before start finished restarting): ensure a spawn.
        if (!this.child && !this.starting) {
          try {
            this.start(this.hostPath, this.hostArgs)
          } catch (error) {
            const message = error instanceof Error ? error.message : String(error)
            return Promise.reject(new Error(message))
          }
        }
      } else {
        return Promise.reject(new Error(this.lastError ?? 'Host is not running'))
      }
    }

    try {
      await this.waitUntilReady()
    } catch (error) {
      return Promise.reject(error instanceof Error ? error : new Error(String(error)))
    }

    if (!this.child || !this.child.stdin.writable) {
      return Promise.reject(new Error(this.lastError ?? 'Host is not running'))
    }

    const id = this.nextId++
    const promise = new Promise<unknown>((resolve, reject) => {
      const timer = setTimeout(() => {
        if (this.pending.delete(id)) {
          reject(new Error(`Host request timed out after ${PENDING_TIMEOUT_MS}ms: ${method}`))
        }
      }, PENDING_TIMEOUT_MS)
      this.pending.set(id, { resolve, reject, timer })
    })
    this.child.stdin.write(`${JSON.stringify({ id, method, params })}\n`)
    return promise
  }

  stop(): Promise<void> {
    this.stopping = true
    this.clearBootWatchdog()
    if (this.restartTimer) {
      clearTimeout(this.restartTimer)
      this.restartTimer = null
    }
    const child = this.child
    if (!child || child.killed) return Promise.resolve()
    return new Promise((resolve) => {
      const timer = setTimeout(() => {
        this.killChild(child)
        resolve()
      }, this.stopGraceMs)
      child.once('exit', () => {
        clearTimeout(timer)
        resolve()
      })
      try {
        if (child.stdin.writable) {
          child.stdin.write(`${JSON.stringify({ id: this.nextId++, method: 'app.quit', params: {} })}\n`)
        } else {
          this.killChild(child)
        }
      } catch {
        this.killChild(child)
      }
    })
  }

  private scheduleRestart(): void {
    if (this.restartAttempts >= MAX_RESTART_ATTEMPTS) {
      console.error(`[host] giving up after ${MAX_RESTART_ATTEMPTS} restart attempts`)
      this.emitSynthetic('host.error', {
        message: this.lastError ?? 'Host failed repeatedly',
        fatal: true
      })
      return
    }
    if (this.restartTimer || !this.hostPath) return

    this.restartAttempts += 1
    console.log(`[host] scheduling restart attempt ${this.restartAttempts}/${MAX_RESTART_ATTEMPTS}`)
    this.restartTimer = setTimeout(() => {
      this.restartTimer = null
      if (this.stopping || this.child || !this.hostPath) return
      try {
        this.start(this.hostPath, this.hostArgs)
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error)
        this.lastError = message
        console.error(`[host] restart failed: ${message}`)
        this.emitSynthetic('host.error', { message, fatal: true })
      }
    }, this.restartDelayMs)
  }

  private setReady(data: unknown): void {
    this.clearBootWatchdog()
    this.ready = true
    this.lastReadyData = data
    this.lastError = null
    this.restartAttempts = 0
    const waiters = this.readyWaiters.splice(0)
    for (const waiter of waiters) waiter.resolve()
  }

  private rejectReadyWaiters(error: Error): void {
    const waiters = this.readyWaiters.splice(0)
    for (const waiter of waiters) waiter.reject(error)
  }

  /**
   * Drop the active child only when {@code child} is still the current handle.
   * A second call, or a late event from a replaced process, is a no-op.
   */
  private finalizeChild(
    child: ChildProcessWithoutNullStreams | null,
    error: Error,
    details: {
      kind: FinalizeKind
      code?: number | null
      signal?: NodeJS.Signals | null
      wasReady?: boolean
    }
  ): void {
    if (this.child !== child) return

    this.clearBootWatchdog()
    this.starting = false
    this.ready = false
    this.lastError = error.message

    for (const [, request] of this.pending) {
      clearTimeout(request.timer)
      request.reject(error)
    }
    this.pending.clear()

    this.rl?.close()
    this.rl = null
    this.child = null

    if (details.kind === 'exit') {
      this.emitSynthetic('host.exited', {
        code: details.code ?? null,
        signal: details.signal ?? null,
        wasReady: details.wasReady === true
      })
    } else {
      this.emitSynthetic('host.error', { message: this.lastError })
    }

    this.rejectReadyWaiters(error)

    if (details.kind === 'exit' && !this.stopping && this.hostPath) {
      this.scheduleRestart()
    }
  }

  private armBootWatchdog(child: ChildProcessWithoutNullStreams): void {
    this.clearBootWatchdog()
    this.bootWatchdog = setTimeout(() => {
      this.bootWatchdog = null
      if (this.child !== child || this.ready || this.stopping) return
      const error = new Error('Host did not become ready in time')
      this.finalizeChild(child, error, { kind: 'timeout' })
      this.killChild(child)
    }, this.readyTimeoutMs)
  }

  private clearBootWatchdog(): void {
    if (!this.bootWatchdog) return
    clearTimeout(this.bootWatchdog)
    this.bootWatchdog = null
  }

  private killChild(child: ChildProcessWithoutNullStreams): void {
    try {
      if (!child.killed) child.kill()
    } catch {
      // Failed spawn or an already-reaped process has no OS handle to signal.
    }
  }

  private emitSynthetic(event: string, data: unknown): void {
    const set = this.listeners.get(event)
    if (set) {
      for (const callback of set) {
        try {
          callback(data)
        } catch (error) {
          console.error(`[host] event handler failed: ${error}`)
        }
      }
    }
    for (const callback of this.anyListeners) {
      try {
        callback(event, data)
      } catch (error) {
        console.error(`[host] any-event handler failed: ${error}`)
      }
    }
  }

  private handleLine(line: string): void {
    let message: HostLine
    try {
      message = JSON.parse(line) as HostLine
    } catch {
      return
    }
    if (!message || typeof message !== 'object') return

    if (typeof message.event === 'string') {
      if (message.event === 'host.ready') {
        this.setReady(message.data)
      }

      const set = this.listeners.get(message.event)
      if (set) {
        for (const callback of set) {
          try {
            callback(message.data)
          } catch (error) {
            console.error(`[host] event handler failed: ${error}`)
          }
        }
      }
      for (const callback of this.anyListeners) {
        try {
          callback(message.event, message.data)
        } catch (error) {
          console.error(`[host] any-event handler failed: ${error}`)
        }
      }
      return
    }

    if (typeof message.id === 'number') {
      const request = this.pending.get(message.id)
      if (!request) return
      clearTimeout(request.timer)
      this.pending.delete(message.id)
      if (message.error) {
        request.reject(formatHostRpcError(message.error))
      } else {
        request.resolve(message.result)
      }
    }
  }
}

export const hostClient = new HostClient()
