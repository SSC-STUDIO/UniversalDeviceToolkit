import { app, BrowserWindow } from 'electron'

let getMainWindow: () => BrowserWindow | null = () => null
let restoreMainWindow: (() => void) | null = null

export function setMainWindowRef(getter: () => BrowserWindow | null): void {
  getMainWindow = getter
}

export function setMainWindowRestore(restore: () => void): void {
  restoreMainWindow = restore
}

export function initSingleInstance(): boolean {
  if (!app.requestSingleInstanceLock()) {
    app.quit()
    return false
  }

  app.on('second-instance', () => {
    const window = getMainWindow()
    if (!window || window.isDestroyed()) {
      restoreMainWindow?.()
      return
    }
    if (window.isMinimized()) window.restore()
    window.show()
    window.focus()
  })

  return true
}
