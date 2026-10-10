/**
 * Native dialog and shell-open helpers behind the bridge `dialog:*` methods
 * plus the dedicated `dialog:select-*` IPC channels used by the preload API.
 */
import { BrowserWindow, dialog, ipcMain, shell } from 'electron'
import { resolveSafeOpenPath } from './safe-open-path'

const DIALOG_BRIDGE_METHODS = new Set([
  'dialog:select-json-file',
  'dialog:open-file',
  'dialog:save-file',
  'dialog:select-folder',
  'dialog:open-path',
  'dialog:open-url'
])

export function isDialogBridgeMethod(method: string): boolean {
  return DIALOG_BRIDGE_METHODS.has(method)
}

export async function invokeDialogBridgeMethod(
  method: string,
  params: unknown,
  owner: BrowserWindow | null
): Promise<unknown> {
  const dialogParams = (params ?? {}) as {
    title?: unknown
    filters?: unknown
    defaultPath?: unknown
  }
  const dialogFilters = Array.isArray(dialogParams.filters)
    ? dialogParams.filters.filter((filter): filter is { name: string; extensions: string[] } => {
        if (filter == null || typeof filter !== 'object') return false
        const record = filter as { name?: unknown; extensions?: unknown }
        return typeof record.name === 'string' && Array.isArray(record.extensions)
      })
    : undefined
  const dialogTitle = typeof dialogParams.title === 'string' ? dialogParams.title : undefined
  const dialogDefaultPath =
    typeof dialogParams.defaultPath === 'string' ? dialogParams.defaultPath : undefined

  if (method === 'dialog:select-json-file') {
    const options = {
      title: dialogTitle ?? 'Import keyboard backlight profile',
      properties: ['openFile'] as ('openFile')[],
      filters: dialogFilters ?? [{ name: 'Json Files', extensions: ['json'] }]
    }
    const result = owner == null
      ? await dialog.showOpenDialog(options)
      : await dialog.showOpenDialog(owner, options)
    return result.canceled ? null : (result.filePaths[0] ?? null)
  }
  if (method === 'dialog:open-file') {
    const options = {
      title: dialogTitle ?? 'Open file',
      defaultPath: dialogDefaultPath,
      properties: ['openFile'] as ('openFile')[],
      filters: dialogFilters
    }
    const result = owner == null
      ? await dialog.showOpenDialog(options)
      : await dialog.showOpenDialog(owner, options)
    return result.canceled ? null : (result.filePaths[0] ?? null)
  }
  if (method === 'dialog:save-file') {
    const options = {
      title: dialogTitle ?? 'Save file',
      defaultPath: dialogDefaultPath,
      filters: dialogFilters
    }
    const result = owner == null
      ? await dialog.showSaveDialog(options)
      : await dialog.showSaveDialog(owner, options)
    return result.canceled ? null : (result.filePath ?? null)
  }
  if (method === 'dialog:select-folder') {
    const options = {
      title: dialogTitle ?? 'Select folder',
      properties: ['openDirectory'] as ('openDirectory')[]
    }
    const result = owner == null
      ? await dialog.showOpenDialog(options)
      : await dialog.showOpenDialog(owner, options)
    return result.canceled ? null : (result.filePaths[0] ?? null)
  }
  if (method === 'dialog:open-path') {
    const target = resolveSafeOpenPath((params as { path?: unknown } | null)?.path)
    const error = await shell.openPath(target)
    return { ok: error.length === 0 }
  }
  // dialog:open-url
  const url = (params as { url?: unknown } | null)?.url
  if (typeof url !== 'string' || url.length === 0) {
    throw new Error('A URL is required.')
  }
  let parsed: URL
  try {
    parsed = new URL(url)
  } catch {
    throw new Error('Invalid URL.')
  }
  if (parsed.protocol !== 'https:' && parsed.protocol !== 'http:') {
    throw new Error('Only http(s) URLs can be opened.')
  }
  await shell.openExternal(parsed.toString())
  return { ok: true }
}

/** Registers the preload file-picker channels (dialog:select-*). */
export function registerFileDialogIpc(getOwner: () => BrowserWindow | null): void {
  const ownerOrFocused = (): BrowserWindow | null => getOwner() ?? BrowserWindow.getFocusedWindow()

  ipcMain.handle('dialog:select-json-file', async () => {
    const options = {
      title: 'Import keyboard backlight profile',
      properties: ['openFile'] as ('openFile')[],
      filters: [{ name: 'Json Files', extensions: ['json'] }]
    }
    const owner = ownerOrFocused()
    const result = owner == null
      ? await dialog.showOpenDialog(options)
      : await dialog.showOpenDialog(owner, options)
    return result.canceled ? null : (result.filePaths[0] ?? null)
  })

  // Mirrors the process-trigger exe picker (OpenFileDialog with the exe filter).
  // Windows filters .exe; macOS/Linux leave the dialog unfiltered (an .app
  // bundle or ELF/Mach-O binary is picked by the user).
  ipcMain.handle('dialog:select-exe-file', async () => {
    const options = {
      title: 'Open',
      properties: ['openFile'] as ('openFile')[],
      ...(process.platform === 'win32'
        ? { filters: [{ name: 'Exe Files (.exe)', extensions: ['exe'] }] }
        : {})
    }
    const owner = ownerOrFocused()
    const result = owner == null
      ? await dialog.showOpenDialog(options)
      : await dialog.showOpenDialog(owner, options)
    return result.canceled ? null : (result.filePaths[0] ?? null)
  })

  // Mirrors the play-sound step file picker (audio files). C:\Windows\Media
  // is a Windows-only default location.
  ipcMain.handle('dialog:select-audio-file', async () => {
    const options = {
      title: 'Import',
      ...(process.platform === 'win32' ? { defaultPath: 'C:\\Windows\\Media' } : {}),
      properties: ['openFile'] as ('openFile')[],
      filters: [
        { name: 'Audio Files', extensions: ['wav', 'mp3', 'ogg', 'flac', 'aac', 'm4a', 'wma'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    }
    const owner = ownerOrFocused()
    const result = owner == null
      ? await dialog.showOpenDialog(options)
      : await dialog.showOpenDialog(owner, options)
    return result.canceled ? null : (result.filePaths[0] ?? null)
  })
}
