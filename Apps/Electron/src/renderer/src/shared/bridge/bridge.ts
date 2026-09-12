export interface BridgeError {
  code: number
  message: string
}

export interface BridgeResponse<T> {
  result: T
}

export interface BridgeEvent<T> {
  event: string
  data: T
}

export type JsonValue = unknown

export interface HostStatus {
  running: boolean
  ready: boolean
  lastError: string | null
  readyPayload: unknown
}

const HOST_ERROR_CODE = /\[UDT:(-?\d+)\]/

export class BridgeInvokeError extends Error implements BridgeError {
  readonly code: number

  constructor(message: string, code = -32603, options?: { cause?: unknown }) {
    super(message, options)
    this.name = 'BridgeInvokeError'
    this.code = code
  }
}

function getBridge(): typeof window.bridge {
  return window.bridge
}

function requireBridge(): NonNullable<typeof window.bridge> {
  const api = getBridge()
  if (api == null) {
    throw new BridgeInvokeError('Bridge is not available')
  }
  return api
}

function isHostStatus(value: unknown): value is HostStatus {
  if (value == null || typeof value !== 'object' || Array.isArray(value)) return false
  const record = value as Record<string, unknown>
  return (
    typeof record.running === 'boolean' &&
    typeof record.ready === 'boolean' &&
    (record.lastError === null || typeof record.lastError === 'string')
  )
}

function isFatalHostFailure(status: HostStatus): boolean {
  return Boolean(status.lastError) && !status.running && !status.ready
}

function toBridgeInvokeError(error: unknown): BridgeInvokeError {
  if (error instanceof BridgeInvokeError) return error
  const message = sanitizeBridgeError(error)
  const hostCode = parseHostErrorCode(message)
  return new BridgeInvokeError(message, hostCode ?? -32603, { cause: error })
}

/** Strip Electron's verbose ipcRenderer.invoke wrapper from Error.message. */
export function sanitizeBridgeError(error: unknown): string {
  const raw = error instanceof Error ? error.message : String(error)
  return raw
    .replace(/^Error invoking remote method '[^']+':\s*/i, '')
    .replace(/^Error:\s*/i, '')
    .trim()
}

/** JSON-RPC code preserved by the Host client as `[UDT:<code>]`. */
export function parseHostErrorCode(error: unknown): number | null {
  const match = sanitizeBridgeError(error).match(HOST_ERROR_CODE)
  if (match == null) return null
  const code = Number(match[1])
  return Number.isFinite(code) ? code : null
}

export function stripHostErrorPrefix(message: string): string {
  return message.replace(HOST_ERROR_CODE, '').trim()
}

export function isHostUnavailableError(message: string): boolean {
  return /host is not running|host did not become ready|host exited|host spawn failed|host executable not found|bridge is not available/i.test(
    message
  )
}

/**
 * Map stable Host codes to UI copy. Unknown codes keep the Host message
 * without the `[UDT:]` prefix.
 */
export function localizeHostError(
  error: unknown,
  translate: (key: string, options?: { defaultValue: string }) => string
): string {
  const raw = sanitizeBridgeError(error)
  const fallback = stripHostErrorPrefix(raw) || raw
  const code = parseHostErrorCode(raw)
  switch (code) {
    case -1006:
      return translate('optimization.elevationRequired', { defaultValue: fallback })
    case -1010:
      return translate('optimization.network.proxyMissing', { defaultValue: fallback })
    case -1011:
      return translate('optimization.network.hostsModeRefused', { defaultValue: fallback })
    case -1012:
      return translate('optimization.network.startRefused', { defaultValue: fallback })
    case -32099:
      return translate('common.notSupportedOnPlatform', { defaultValue: fallback })
    default:
      return fallback
  }
}

/** Typed invoke wrapper over the preload bridge. */
export async function invoke<T = JsonValue>(method: string, params: JsonValue = {}): Promise<T> {
  const api = requireBridge()
  try {
    const result: unknown = await api.invoke(method, params)
    if (result instanceof Error) {
      throw result
    }
    return result as T
  } catch (error) {
    throw toBridgeInvokeError(error)
  }
}

/** Invoke that rejects when the Host returns a non-object payload. */
export async function invokeObject<T extends object>(
  method: string,
  params: JsonValue = {}
): Promise<T> {
  const result = await invoke<unknown>(method, params)
  if (result == null || typeof result !== 'object') {
    throw new BridgeInvokeError(`Host method ${method} returned an invalid result`)
  }
  return result as T
}

/** Subscribe to a host event; returns an unsubscribe function. */
export function on<T = JsonValue>(event: string, callback: (data: T) => void): () => void {
  const api = getBridge()
  if (api == null) {
    return () => undefined
  }
  return api.on(event, callback as (data: unknown) => void)
}

export async function getHostStatus(): Promise<HostStatus> {
  const api = getBridge()
  if (api?.getHostStatus == null) {
    throw new BridgeInvokeError('Bridge is not available')
  }
  const status: unknown = await api.getHostStatus()
  if (!isHostStatus(status)) {
    throw new BridgeInvokeError('Host status response is invalid')
  }
  return status
}

/**
 * Wait until the Host has published {@code host.ready} (or is already ready).
 * Subscribes first, then reads the status snapshot so a just-emitted ready
 * event cannot be missed between the check and the listener.
 */
export async function waitForHostReady(timeoutMs = 45000): Promise<void> {
  requireBridge()

  await new Promise<void>((resolve, reject) => {
    let settled = false
    let timer = 0
    let offReady = (): void => undefined
    let offError = (): void => undefined

    const finish = (action: () => void): void => {
      if (settled) return
      settled = true
      window.clearTimeout(timer)
      offReady()
      offError()
      action()
    }

    const rejectWith = (message: string): void => {
      finish(() => reject(new BridgeInvokeError(message)))
    }

    const applyHostStatus = async (): Promise<void> => {
      try {
        const latest = await getHostStatus()
        if (latest.ready) {
          finish(() => resolve())
          return
        }
        if (isFatalHostFailure(latest)) {
          rejectWith(latest.lastError ?? 'Host failed to start')
        }
      } catch (error) {
        rejectWith(sanitizeBridgeError(error))
      }
    }

    offReady = on('host.ready', () => {
      finish(() => resolve())
    })
    offError = on<{ message?: string; fatal?: boolean }>('host.error', (data) => {
      if (data?.fatal) {
        const message =
          typeof data.message === 'string' && data.message.trim().length > 0
            ? data.message
            : 'Host failed to start'
        rejectWith(message)
        return
      }
      void applyHostStatus()
    })

    timer = window.setTimeout(() => {
      void getHostStatus()
        .then((latest) => {
          if (latest.ready) {
            finish(() => resolve())
            return
          }
          rejectWith(latest.lastError ?? 'Host did not become ready in time')
        })
        .catch((error: unknown) => {
          rejectWith(sanitizeBridgeError(error) || 'Host did not become ready in time')
        })
    }, timeoutMs)

    void applyHostStatus()
  })
}
