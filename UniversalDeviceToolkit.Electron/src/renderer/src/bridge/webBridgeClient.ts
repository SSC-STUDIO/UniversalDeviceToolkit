import type { Bridge } from '../../../preload/index.d'

type EventCallback = (data: unknown) => void

type HostStatusSnapshot = {
  running: boolean
  ready: boolean
  lastError: string | null
  readyPayload: unknown
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === 'object' && !Array.isArray(value)
}

function isHostStatusSnapshot(value: unknown): value is HostStatusSnapshot {
  if (!isRecord(value)) return false
  return (
    typeof value.running === 'boolean' &&
    typeof value.ready === 'boolean' &&
    (value.lastError === null || typeof value.lastError === 'string')
  )
}

async function readJson(response: Response, context: string): Promise<unknown> {
  try {
    return await response.json()
  } catch {
    throw new Error(
      response.ok ? `${context} returned invalid JSON` : `${context} failed (${response.status})`
    )
  }
}

/**
 * Browser dev shim: talks to scripts/dev-bridge-server.mjs over HTTP + SSE.
 * Electron-only APIs are stubbed so UI code can run without crashing.
 * Pass ?udtPlatform=darwin|linux|win32 to preview native layout chrome.
 */
function resolveDevWebPlatform(): string {
  if (typeof window === 'undefined') return 'web'
  const requested = new URLSearchParams(window.location.search).get('udtPlatform')
  if (requested === 'darwin' || requested === 'linux' || requested === 'win32' || requested === 'web') {
    return requested
  }
  return 'web'
}

export function createWebBridge(baseUrl: string): Bridge {
  const normalizedBase = baseUrl.replace(/\/$/, '')
  const listeners = new Map<string, Set<EventCallback>>()
  let eventSource: EventSource | null = null
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null

  const dispatch = (event: string, data: unknown): void => {
    const set = listeners.get(event)
    if (set) {
      for (const callback of set) {
        try {
          callback(data)
        } catch (error) {
          console.error(`[web-bridge] event handler failed: ${error}`)
        }
      }
    }
  }

  const connectEvents = (): void => {
    if (eventSource) {
      eventSource.close()
      eventSource = null
    }
    eventSource = new EventSource(`${normalizedBase}/events`)
    eventSource.onmessage = (message) => {
      try {
        const payload: unknown = JSON.parse(message.data)
        if (isRecord(payload) && typeof payload.event === 'string') {
          dispatch(payload.event, payload.data)
        }
      } catch (error) {
        console.error(`[web-bridge] invalid SSE payload: ${error}`)
      }
    }
    eventSource.onerror = () => {
      eventSource?.close()
      eventSource = null
      if (reconnectTimer === null) {
        reconnectTimer = setTimeout(() => {
          reconnectTimer = null
          connectEvents()
        }, 1500)
      }
    }
  }

  connectEvents()

  const noop = (): void => undefined
  const noopAsync = async (): Promise<void> => undefined
  const noopBool = async (): Promise<boolean> => false
  const noopOpened = async (): Promise<{ opened: boolean }> => ({ opened: false })
  const noopAutorun = async (): Promise<{ ok: boolean; enabled: boolean }> => ({
    ok: false,
    enabled: false
  })
  const noopAutorunGet = async (): Promise<{ enabled: boolean }> => ({ enabled: false })
  const noopMemory = async (): Promise<{
    processes: Array<{ name: string; type: string; workingSetMB: number }>
    totalMB: number
  }> => ({ processes: [], totalMB: 0 })
  const noopNullableString = async (): Promise<string | null> => null
  const noopClipboardOk = async (): Promise<{ ok: boolean }> => ({ ok: true })

  return {
    platform: resolveDevWebPlatform(),
    installerSelection: null,
    invoke: async (method: string, params?: unknown): Promise<unknown> => {
      const response = await fetch(`${normalizedBase}/invoke`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ method, params: params ?? {} })
      })
      const body = await readJson(response, `Bridge invoke ${method}`)
      if (!isRecord(body)) {
        throw new Error(`Bridge invoke ${method} returned an invalid payload`)
      }
      const error = isRecord(body.error) ? body.error : null
      if (error != null) {
        const code = typeof error.code === 'number' ? error.code : -32603
        const text =
          typeof error.message === 'string' && error.message.trim().length > 0
            ? error.message.trim()
            : 'Bridge invoke failed'
        throw new Error(`[UDT:${code}] ${text}`)
      }
      if (!response.ok) {
        throw new Error(`Bridge invoke ${method} failed (${response.status})`)
      }
      return body.result
    },
    getHostStatus: async () => {
      const response = await fetch(`${normalizedBase}/status`)
      const body = await readJson(response, 'Host status')
      if (!response.ok) {
        throw new Error(`Host status request failed (${response.status})`)
      }
      if (!isHostStatusSnapshot(body)) {
        throw new Error('Host status response is invalid')
      }
      return body
    },
    on: (event: string, callback: EventCallback): (() => void) => {
      let set = listeners.get(event)
      if (set == null) {
        set = new Set()
        listeners.set(event, set)
      }
      set.add(callback)
      return () => set.delete(callback)
    },
    minimize: noop,
    maximizeToggle: noop,
    closeWindow: noop,
    setBackgroundMaterial: noopAsync,
    openLogFolder: noopAsync,
    log: (level: string, message: string): void => {
      console.log(`[renderer:${level}] ${message}`)
    },
    openAppFolder: noopOpened,
    openExternal: async (url: string): Promise<{ opened: boolean }> => {
      window.open(url, '_blank', 'noopener,noreferrer')
      return { opened: true }
    },
    openPath: noopOpened,
    quitApp: noop,
    selectExeFile: noopNullableString,
    selectAudioFile: noopNullableString,
    isMaximized: noopBool,
    onMaximizedChanged: (): (() => void) => noop,
    setTrayLanguage: noop,
    refreshTrayMenu: noop,
    writeClipboardLines: noopClipboardOk,
    setAutorun: noopAutorun,
    getAutorun: noopAutorunGet,
    setThemeSource: noop,
    // Browser dev has no main process; themeStore falls back to CSS zoom.
    setUiScale: async (scale: number): Promise<{ ok: boolean; scale: number }> => ({
      ok: false,
      scale
    }),
    getMemoryUsage: noopMemory
  }
}
