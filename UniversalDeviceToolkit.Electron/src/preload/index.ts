import { contextBridge, ipcRenderer, webUtils } from 'electron'

import { parseInstallerSelectionArguments } from '../shared/installer-selection'

const installerSelection = parseInstallerSelectionArguments(process.argv)

const bridge = {
  /** Runtime platform ('darwin' on macOS) — drives native title bar layout. */
  platform: process.platform,
  /** Selection captured by the NSIS setup wizard, if this install has one. */
  installerSelection,
  invoke: (method: string, params?: unknown): Promise<unknown> =>
    ipcRenderer.invoke('bridge:invoke', method, params),
  getHostStatus: (): Promise<{
    running: boolean
    ready: boolean
    lastError: string | null
    readyPayload: unknown
  }> => ipcRenderer.invoke('host:get-status'),
  on: (event: string, callback: (data: unknown) => void): (() => void) => {
    const listener = (_event: Electron.IpcRendererEvent, receivedEvent: string, data: unknown): void => {
      if (receivedEvent === event) callback(data)
    }
    ipcRenderer.on('bridge:event', listener)
    return () => ipcRenderer.removeListener('bridge:event', listener)
  },
  minimize: (): void => ipcRenderer.send('window:minimize'),
  maximizeToggle: (): void => ipcRenderer.send('window:maximize-toggle'),
  closeWindow: (): void => ipcRenderer.send('window:close'),
  setBackgroundMaterial: (material: 'none' | 'mica' | 'acrylic'): Promise<void> =>
    ipcRenderer.invoke('window:set-background-material', material),
  openLogFolder: (): Promise<void> => ipcRenderer.invoke('shell:open-log-folder'),
  log: (level: string, message: string): void => ipcRenderer.send('log:write', { level, message }),
  openAppFolder: (kind: 'data' | 'temp' | 'log'): Promise<{ opened: boolean }> =>
    ipcRenderer.invoke('shell:open-app-folder', kind),
  openExternal: (url: string): Promise<{ opened: boolean }> =>
    ipcRenderer.invoke('shell:open-external', url),
  openPath: (path: string): Promise<{ opened: boolean }> =>
    ipcRenderer.invoke('shell:open-path', path),
  quitApp: (): void => ipcRenderer.send('app:quit'),
  selectExeFile: (): Promise<string | null> => ipcRenderer.invoke('dialog:select-exe-file'),
  selectAudioFile: (): Promise<string | null> => ipcRenderer.invoke('dialog:select-audio-file'),
  /**
   * Electron 32+ replacement for the removed File.path. Must run in preload
   * (webUtils is not exposed to an isolated renderer).
   */
  getPathForFile: (file: object): string => {
    try {
      return webUtils.getPathForFile(file as never)
    } catch (error) {
      console.warn('[preload] Unable to resolve the selected file path:', error)
      return ''
    }
  },
  isMaximized: (): Promise<boolean> => ipcRenderer.invoke('window:is-maximized'),
  onMaximizedChanged: (callback: (maximized: boolean) => void): (() => void) => {
    const listener = (_event: Electron.IpcRendererEvent, maximized: boolean): void => {
      callback(maximized)
    }
    ipcRenderer.on('window:maximized-changed', listener)
    return () => ipcRenderer.removeListener('window:maximized-changed', listener)
  },
  /** Sync UI language into the main-process tray menu labels. */
  setTrayLanguage: (lang: string): void => {
    ipcRenderer.send('tray:set-language', lang)
  },
  /** Rebuild tray quick actions after automation pipelines change. */
  refreshTrayMenu: (): void => {
    ipcRenderer.send('tray:refresh')
  },
  /** Copy one executable path per line (automation process pickers). */
  writeClipboardLines: (lines: string[]): Promise<{ ok: boolean }> =>
    ipcRenderer.invoke('clipboard:write-lines', { lines }),
  /** macOS login item / Linux XDG autostart. Unused on Windows (Host scheduled task). */
  setAutorun: (enabled: boolean): Promise<{ ok: boolean; enabled: boolean }> =>
    ipcRenderer.invoke('app:set-autorun', enabled),
  getAutorun: (): Promise<{ enabled: boolean }> => ipcRenderer.invoke('app:get-autorun'),
  /** Keep DWM backdrop materials (mica/acrylic) in sync with the in-app theme. */
  setThemeSource: (source: 'system' | 'light' | 'dark'): void => {
    ipcRenderer.send('window:set-theme-source', source)
  },
  /**
   * Push the "Interface scale" setting to the main process, which applies
   * platformBaseZoom x scale to every window/webview via setZoomFactor.
   */
  setUiScale: (scale: number): Promise<{ ok: boolean; scale: number }> =>
    ipcRenderer.invoke('window:set-ui-scale', scale),
  /** Real production memory footprint across every Electron process (MB). */
  getMemoryUsage: (): Promise<{
    processes: Array<{ name: string; type: string; workingSetMB: number }>
    totalMB: number
  }> => ipcRenderer.invoke('app:memory-usage')
}

contextBridge.exposeInMainWorld('bridge', bridge)
