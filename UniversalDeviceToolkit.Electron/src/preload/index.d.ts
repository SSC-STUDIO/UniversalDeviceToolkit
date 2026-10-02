export interface Bridge {
  /** Runtime platform ('darwin' on macOS) — drives native title bar layout. */
  platform: string
  /** Selection captured by the NSIS setup wizard, if this install has one. */
  installerSelection: {
    language: string
    deviceMode: 'auto' | 'basic'
    features: {
      windowsOptimization: boolean
      networkAcceleration: boolean
      automation: boolean
      macro: boolean
      keyboard: boolean
    }
  } | null
  invoke: (method: string, params?: unknown) => Promise<unknown>
  getHostStatus: () => Promise<{
    running: boolean
    ready: boolean
    lastError: string | null
    readyPayload: unknown
  }>
  on: (event: string, callback: (data: unknown) => void) => () => void
  minimize: () => void
  maximizeToggle: () => void
  closeWindow: () => void
  setBackgroundMaterial: (material: 'none' | 'mica' | 'acrylic') => Promise<void>
  openLogFolder: () => Promise<void>
  /** Renderer → main log channel (leveled; lands in userData/logs/renderer.log). */
  log: (level: string, message: string) => void
  openAppFolder: (kind: 'data' | 'temp' | 'log') => Promise<{ opened: boolean }>
  openExternal: (url: string) => Promise<{ opened: boolean }>
  openPath: (path: string) => Promise<{ opened: boolean }>
  quitApp: () => void
  selectExeFile: () => Promise<string | null>
  selectAudioFile: () => Promise<string | null>
  /** Resolves a renderer File from a file input to a real filesystem path. */
  getPathForFile?: (file: { name: string; size: number; type: string }) => string
  isMaximized: () => Promise<boolean>
  onMaximizedChanged: (callback: (maximized: boolean) => void) => () => void
  setTrayLanguage: (lang: string) => void
  refreshTrayMenu: () => void
  writeClipboardLines: (lines: string[]) => Promise<{ ok: boolean }>
  setAutorun: (enabled: boolean) => Promise<{ ok: boolean; enabled: boolean }>
  getAutorun: () => Promise<{ enabled: boolean }>
  setThemeSource: (source: 'system' | 'light' | 'dark') => void
  /** Applies platformBaseZoom x scale to every surface in the main process. */
  setUiScale: (scale: number) => Promise<{ ok: boolean; scale: number }>
  /** Real production memory footprint across every Electron process (MB). */
  getMemoryUsage: () => Promise<{
    processes: Array<{ name: string; type: string; workingSetMB: number }>
    totalMB: number
  }>
}

declare global {
  interface Window {
    bridge?: Bridge
  }
}

export {}
