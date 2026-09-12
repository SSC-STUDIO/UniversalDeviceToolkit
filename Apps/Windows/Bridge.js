(() => {
  'use strict'
  // Replaced with serialized, trusted startup data before document creation.
  const startup = __UDT_STARTUP_JSON__
  const transport = window.chrome.webview
  const pending = new Map()
  const listeners = new Map()
  let nextId = 0

  function request(method, params) {
    const id = ++nextId
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        pending.delete(id)
        reject(new Error(`Shell request timed out: ${method}`))
      }, 65000)
      pending.set(id, { resolve, reject, timer })
      try {
        transport.postMessage({ id, method, params: params ?? null })
      } catch (error) {
        clearTimeout(timer)
        pending.delete(id)
        reject(error)
      }
    })
  }

  function send(method, params) {
    void request(method, params).catch(error => console.error(`[shell] ${method}`, error))
  }

  function on(event, callback) {
    let callbacks = listeners.get(event)
    if (!callbacks) {
      callbacks = new Set()
      listeners.set(event, callbacks)
    }
    callbacks.add(callback)
    return () => {
      callbacks.delete(callback)
      if (callbacks.size === 0) listeners.delete(event)
    }
  }

  transport.addEventListener('message', ({ data }) => {
    if (data == null || typeof data !== 'object') return
    if (typeof data.event === 'string') {
      for (const callback of listeners.get(data.event) ?? []) {
        try { callback(data.data) } catch (error) { console.error('[shell] Event callback failed', error) }
      }
      return
    }
    const completion = pending.get(data.id)
    if (!completion) return
    pending.delete(data.id)
    clearTimeout(completion.timer)
    if (data.error) {
      const code = typeof data.error.code === 'number' ? `[UDT:${data.error.code}] ` : ''
      completion.reject(new Error(`${code}${data.error.message ?? 'Native shell request failed.'}`))
    }
    else completion.resolve(data.result)
  })

  window.addEventListener('pagehide', () => {
    for (const completion of pending.values()) {
      clearTimeout(completion.timer)
      completion.reject(new Error('The application page was closed.'))
    }
    pending.clear()
    listeners.clear()
  })

  window.bridge = Object.freeze({
    platform: 'win32',
    installerSelection: startup.installerSelection,
    invoke: (method, params) => request('bridge:invoke', { method, params }),
    getHostStatus: () => request('host:get-status'),
    on,
    minimize: () => send('window:minimize'),
    maximizeToggle: () => send('window:maximize-toggle'),
    closeWindow: () => send('window:close'),
    setBackgroundMaterial: material => request('window:set-background-material', material),
    openLogFolder: () => request('shell:open-log-folder'),
    log: (level, message) => send('log:write', { level, message }),
    openAppFolder: kind => request('shell:open-app-folder', kind),
    openExternal: url => request('shell:open-external', url),
    openPath: path => request('shell:open-path', path),
    quitApp: () => send('app:quit'),
    selectExeFile: () => request('dialog:select-exe-file'),
    selectAudioFile: () => request('dialog:select-audio-file'),
    // Browser-selected images use BootLogoModal's existing data-URL fallback.
    getPathForFile: () => '',
    isMaximized: () => request('window:is-maximized'),
    onMaximizedChanged: callback => on('window:maximized-changed', callback),
    setTrayLanguage: language => send('tray:set-language', language),
    refreshTrayMenu: () => send('tray:refresh'),
    writeClipboardLines: lines => request('clipboard:write-lines', { lines }),
    setAutorun: enabled => request('app:set-autorun', enabled),
    getAutorun: () => request('app:get-autorun'),
    setThemeSource: source => send('window:set-theme-source', source),
    setUiScale: scale => request('window:set-ui-scale', scale),
    getMemoryUsage: () => request('app:memory-usage')
  })
})()
