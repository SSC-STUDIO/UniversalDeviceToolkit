import {
  app,
  BrowserWindow,
  clipboard,
  ipcMain,
  nativeTheme,
  powerMonitor,
  screen,
  session,
  shell,
  type IpcMainInvokeEvent
} from 'electron'
import { join } from 'path'
import { existsSync, mkdirSync, writeFileSync } from 'fs'
import { tmpdir } from 'os'
import { pathToFileURL } from 'url'
import { hostClient } from './host-client'
import {
  initMainLogger,
  isValidLogLevel,
  logsDirectory,
  writeHostLog,
  writeRendererLog
} from './logger'
import { initSingleInstance, setMainWindowRef, setMainWindowRestore } from './single-instance'
import { effectiveZoom, installZoomAutoApply, setUiScale } from './ui-scale'
import { readWindowState, updateWindowState } from './window-state'
import { applyAutorun, readAutorun } from './autorun'
import { getDeviceInfo } from './device-info'
import { invokeDialogBridgeMethod, isDialogBridgeMethod, registerFileDialogIpc } from './dialogs'
import { logMemoryUsage, reportMemoryUsage } from './memory-report'
import { initTray, destroyTray, isTrayActive, refreshTrayMenu, updateTrayLanguage } from './tray'
import { destroyTrayPopup } from './tray-popup'
import { initOsdWindow, destroyOsdWindow, isOsdVisible, suspendOsdWindow } from './osd-window'
import { initStatusWindow, destroyStatusWindow, showStatusWindow } from './status-window'
import { flags, describeFlags, toHostArgs } from './flags'
import {
  buildInstallerHostArguments,
  buildInstallerRendererArguments,
  readInstallerSelection
} from './installer-selection'
import { attachMaximizeWorkAreaClamp, constrainToWorkArea } from './window-helpers'
import { listPowerPlans, setActivePowerPlan } from './power-plans'
import { restartSystem, shutdownSystem, sleepSystem } from './system-power'
import { resolveSafeOpenPath } from './safe-open-path'
import { downloadLatestUpdate, getLatestRelease, launchInstaller, type DownloadProgress } from './update-downloader'
import { installApplicationMenu } from './menu'
import {
  cancelAllIdleDestroys,
  isUiActive,
  setSurfaceVisible,
  setUiActivityHandler
} from './ui-activity'

// The Windows AppUserModelId (taskbar grouping, notifications) is meaningless
// on macOS/Linux; setting it there is a no-op, so keep the call Windows-only.
if (process.platform === 'win32') {
  app.setAppUserModelId('com.universaldevicetoolkit.app')
}

// Force the app name to use the product name everywhere (taskbar window preview,
// notifications, dev tools, etc.) instead of falling back to package.json name which
// still ends in '-electron' and would surface as a separate 'Electron' window in the
// Windows 11 taskbar window preview alongside the main app.
app.setName('Universal Device Toolkit')
// Disable the noisy Chromium DevTools shortcut defaults; the app never opens DevTools
// in production, and an unsuppressed F12 would spawn a hidden 'Electron' frame that the
// taskbar window preview surfaces as a third entry next to the main window.
app.commandLine.appendSwitch(
  'disable-features',
  [
    'OutOfBlinkCors',
    'TranslateUI',
    'MediaRouter',
    'AutofillServerCommunication',
    'OptimizationHints',
    'SpareRendererForSitePerProcess',
    'BackForwardCache',
    'CalculateNativeWinOcclusion',
    'InterestFeedContentSuggestions',
    'GlobalMediaControls',
    'PreloadMediaEngagementData',
    'AutofillEnableAccountWalletStorage',
    'HeavyAdIntervention',
    'EyeDropper',
    'AudioServiceOutOfProcess',
    'CertificateTransparencyComponentUpdater',
    'WidgetLayering',
    'DownloadBubble',
    'DownloadBubbleV2',
    'NotificationTriggers',
    'PushMessaging',
    'WebBluetooth',
    'WebUSB',
    'WebHID',
    'WebNFC',
    'WebAuthentication',
    'TabHoverCardImages'
  ].join(',')
)
app.commandLine.appendSwitch('disable-background-networking')
app.commandLine.appendSwitch('disable-component-update')
app.commandLine.appendSwitch('disable-component-extensions-with-background-pages')
app.commandLine.appendSwitch('disable-client-side-phishing-detection')
app.commandLine.appendSwitch('disable-default-apps')
app.commandLine.appendSwitch('disable-extensions')
app.commandLine.appendSwitch('disable-sync')
app.commandLine.appendSwitch('disable-speech-api')
app.commandLine.appendSwitch('disable-domain-reliability')
app.commandLine.appendSwitch('disable-hang-monitor')
app.commandLine.appendSwitch('disable-prompt-on-repost')
app.commandLine.appendSwitch('disable-breakpad')
app.commandLine.appendSwitch('disable-gpu-shader-disk-cache')
app.commandLine.appendSwitch('disable-spell-checking')
app.commandLine.appendSwitch('disable-cloud-import')
app.commandLine.appendSwitch('disable-component-cloud-policy')
app.commandLine.appendSwitch('disable-print-preview')
app.commandLine.appendSwitch('disable-logging')
app.commandLine.appendSwitch('disable-site-isolation-trials')
app.commandLine.appendSwitch('no-pings')
app.commandLine.appendSwitch('no-first-run')
app.commandLine.appendSwitch('no-default-browser-check')
app.commandLine.appendSwitch('disk-cache-size', '4194304')
app.commandLine.appendSwitch('media-cache-size', '4194304')
app.commandLine.appendSwitch('js-flags', '--optimize-for-size --max-old-space-size=96 --expose_gc --initial-heap-size=4')
app.commandLine.appendSwitch('renderer-process-limit', '1')
// --single-process merges renderers into the main process so memory usage can be
// inspected as a single entry (debug/dev only).
if (flags.singleProcess) {
  app.commandLine.appendSwitch('single-process')
}

// Renderer zoom is owned by ui-scale.ts (platform base density x user scale);
// every window created from here on picks it up.
installZoomAutoApply()

/**
 * Design minimum content size in CSS px. The DIP minimum is derived from the
 * effective zoom so every platform (and every "Interface scale" level) shows
 * the same minimum amount of UI; container queries handle narrower layouts.
 */
const DESIGN_MIN_CONTENT_WIDTH = 1024
const DESIGN_MIN_CONTENT_HEIGHT = 640

const PROJECT_ROOT = join(__dirname, '..', '..')

if (!initSingleInstance()) {
  app.exit(0)
}

let mainWindow: BrowserWindow | null = null
let isQuitting = false
/**
 * True while the shell is tray-only: no main BrowserWindow, Host still running.
 * Prevents window-all-closed from quitting and keeps the tray alive.
 */
let trayOnlySession = false
/** Tray navigation requested before the recreated renderer finished loading. */
let pendingTrayRoute: string | null = null
/** Cancels a pending tray-background destroy if restore wins the race. */
let backgroundDestroyGeneration = 0
/** One-shot bypass after a close was already decided (sync preventDefault, then real close). */
let allowCloseOnce = false
let installerSelection: ReturnType<typeof readInstallerSelection> = null
/** Path returned by the last successful downloadLatestUpdate in this process. */
let lastVerifiedInstallerPath: string | null = null
let lastPowerActionAt = 0
type WindowsBackgroundMaterial = 'none' | 'mica' | 'acrylic'
let currentBackgroundMaterial: WindowsBackgroundMaterial = 'none'

function assertMainFrame(event: IpcMainInvokeEvent): void {
  const win = mainWindow
  if (
    win == null
    || win.isDestroyed()
    || event.sender.id !== win.webContents.id
    || event.senderFrame !== win.webContents.mainFrame
  ) {
    throw new Error('IPC request rejected from an untrusted frame.')
  }
}

function claimPowerAction(): void {
  const now = Date.now()
  if (now - lastPowerActionAt < 2000) {
    throw new Error('Power actions are rate limited.')
  }
  lastPowerActionAt = now
}

function isAllowedRendererNavigation(targetUrl: string): boolean {
  const developmentUrl = process.env['ELECTRON_RENDERER_URL']
  if (!app.isPackaged && developmentUrl) {
    try {
      return new URL(targetUrl).origin === new URL(developmentUrl).origin
    } catch {
      return false
    }
  }

  const rendererUrl = pathToFileURL(join(__dirname, '../renderer/index.html')).toString()
  return targetUrl.split(/[?#]/, 1)[0] === rendererUrl
}

function applyMainWindowBackgroundMaterial(material: WindowsBackgroundMaterial): void {
  if (process.platform !== 'win32' || !mainWindow || mainWindow.isDestroyed()) return
  try {
    if (material === 'none') {
      mainWindow.setBackgroundMaterial('none')
      mainWindow.setBackgroundColor(nativeTheme.shouldUseDarkColors ? '#202020' : '#ffffff')
    } else {
      mainWindow.setBackgroundColor('#00000000')
      mainWindow.setBackgroundMaterial(material)
    }
  } catch (error) {
    console.error('[main] failed to apply window background material:', error)
  }
}

function reapplyMainWindowBackgroundMaterial(): void {
  if (process.platform !== 'win32' || !mainWindow || mainWindow.isDestroyed()) return
  // DWM can clear the backdrop while nativeTheme.themeSource is changing.
  setTimeout(() => applyMainWindowBackgroundMaterial(currentBackgroundMaterial), 50)
}

if (process.platform === 'win32') {
  nativeTheme.on('updated', reapplyMainWindowBackgroundMaterial)
}

/** Opaque Linux stand-in for WindowBackdrop.css --udt-mica-chrome (light). */
const LINUX_MICA_CHROME_LIGHT = '#d5ded5'
const LINUX_WINDOW_BACKGROUND_DARK = '#202020'

function linuxWindowBackgroundColor(): string {
  return nativeTheme.shouldUseDarkColors ? LINUX_WINDOW_BACKGROUND_DARK : LINUX_MICA_CHROME_LIGHT
}

function applyLinuxWindowBackgroundColor(): void {
  if (process.platform !== 'linux' || !mainWindow || mainWindow.isDestroyed()) return
  try {
    mainWindow.setBackgroundColor(linuxWindowBackgroundColor())
  } catch (error) {
    console.error('[main] failed to apply Linux window background:', error)
  }
}

if (process.platform === 'linux') {
  nativeTheme.on('updated', applyLinuxWindowBackgroundColor)
}

function resolveHostPath(): string {
  const fromEnv = process.env['UDT_HOST_PATH']
  if (fromEnv) {
    if (!existsSync(fromEnv)) {
      throw new Error(`UDT_HOST_PATH does not exist: ${fromEnv}`)
    }
    if (!isRunnableHostBinary(fromEnv)) {
      throw new Error(
        `UDT_HOST_PATH is an incomplete Host (missing runtimeconfig.json/deps.json, or a Unix FDD apphost without sidecars): ${fromEnv}`
      )
    }
    return fromEnv
  }

  // __dirname = <project>/out/main -> project root is two levels up.
  // The .NET Host is a console binary: UniversalDeviceToolkit.Host.exe on
  // Windows, extension-less executable on Linux/macOS.
  const hostExeName =
    process.platform === 'win32' ? 'UniversalDeviceToolkit.Host.exe' : 'UniversalDeviceToolkit.Host'
  const windowsTfm = 'net10.0-windows10.0.26100.0'
  const portableTfm = 'net10.0'
  const hostRoot = join(PROJECT_ROOT, '..', 'UniversalDeviceToolkit.Host')
  const candidates: string[] = []

  // Packaged builds only: Host is copied into resources/host by electron-builder.
  // In dev, process.resourcesPath points at Electron's own resources and must not
  // take priority over the sibling Host project output.
  if (app.isPackaged) {
    candidates.push(join(process.resourcesPath, 'host', hostExeName))
  }

  // Dev: sibling Host project output. Windows uses the windows TFM + win-x64 RID.
  // Linux/macOS use portable net10.0 (bin/Debug/net10.0 from `dotnet build -p:UDTWindows=false`).
  if (process.platform === 'win32') {
    candidates.push(
      join(hostRoot, 'bin', 'x64', 'Debug', windowsTfm, 'win-x64', hostExeName),
      join(hostRoot, 'bin', 'x64', 'Release', windowsTfm, 'win-x64', hostExeName),
      join(hostRoot, 'publish', 'win-x64', hostExeName)
    )
  } else {
    const ridPrefix = process.platform === 'darwin' ? 'osx' : 'linux'
    const rid = `${ridPrefix}-${process.arch}`
    candidates.push(
      join(hostRoot, 'bin', 'Debug', portableTfm, hostExeName),
      join(hostRoot, 'bin', 'Release', portableTfm, hostExeName),
      join(hostRoot, 'bin', 'Debug', portableTfm, rid, hostExeName),
      join(hostRoot, 'bin', 'Release', portableTfm, rid, hostExeName),
      join(hostRoot, 'publish', rid, hostExeName),
      join(hostRoot, 'publish', `${ridPrefix}-x64`, hostExeName),
      join(hostRoot, 'publish', `${ridPrefix}-arm64`, hostExeName)
    )
  }
  candidates.push(join(PROJECT_ROOT, 'host', hostExeName))

  const incomplete: string[] = []
  for (const candidate of candidates) {
    if (!existsSync(candidate)) continue
    if (isRunnableHostBinary(candidate)) return candidate
    incomplete.push(candidate)
  }

  const incompleteHint =
    incomplete.length === 0
      ? ''
      : `\nIncomplete Host (exe without runtimeconfig.json/deps.json; rebuild UniversalDeviceToolkit.Host -c Debug):\n` +
        incomplete.map((path) => `  - ${path}`).join('\n')

  throw new Error(
    `Host executable not found. Build UniversalDeviceToolkit.Host or set UDT_HOST_PATH.\n` +
      candidates.map((path) => `  - ${path}`).join('\n') +
      incompleteHint
  )
}

function hostSidecarPath(hostPath: string, extension: string): string {
  if (process.platform === 'win32' && hostPath.toLowerCase().endsWith('.exe')) {
    return `${hostPath.slice(0, -4)}.${extension}`
  }
  return `${hostPath}.${extension}`
}

function isRunnableHostBinary(hostPath: string): boolean {
  if (
    existsSync(hostSidecarPath(hostPath, 'runtimeconfig.json')) &&
    existsSync(hostSidecarPath(hostPath, 'deps.json'))
  ) {
    return true
  }
  return process.platform !== 'win32' && !existsSync(`${hostPath}.dll`)
}

/** Events that arrived before the BrowserWindow could receive IPC. */
const bufferedHostEvents: Array<{ event: string; data: unknown }> = []
const BUFFERED_HOST_EVENT_LIMIT = 64
let hostEventsAttached = false

function bufferOrSendHostEvent(event: string, data: unknown): void {
  if (mainWindow && !mainWindow.isDestroyed() && !mainWindow.webContents.isLoadingMainFrame()) {
    mainWindow.webContents.send('bridge:event', event, data)
    return
  }
  bufferedHostEvents.push({ event, data })
  if (bufferedHostEvents.length > BUFFERED_HOST_EVENT_LIMIT) {
    bufferedHostEvents.shift()
  }
}

function launchVerifiedInstaller(params: unknown): Promise<{ ok: boolean }> {
  const requested = (params as { path?: unknown; token?: unknown } | null) ?? {}
  const requestedPath = typeof requested.path === 'string' ? requested.path : ''
  const requestedToken = typeof requested.token === 'string' ? requested.token : ''
  if (lastVerifiedInstallerPath == null || lastVerifiedInstallerPath.length === 0) {
    throw new Error('No verified installer is available.')
  }
  if (requestedPath.length > 0 && requestedPath !== lastVerifiedInstallerPath) {
    const left = requestedPath.replaceAll('/', '\\')
    const right = lastVerifiedInstallerPath.replaceAll('/', '\\')
    if (left.toLowerCase() !== right.toLowerCase()) {
      throw new Error('Installer path is not the verified download.')
    }
  }
  if (requestedToken.length > 0 && requestedToken !== lastVerifiedInstallerPath) {
    throw new Error('Installer token is not the verified download.')
  }
  return launchInstaller(lastVerifiedInstallerPath)
}

async function invokeBridgeMethod(method: string, params?: unknown): Promise<unknown> {
  if (method === 'settings.set') {
    const p = params as { scope?: string; values?: Record<string, unknown> } | undefined
    if (p?.scope === 'application' && p.values) {
      minimizeToTrayCache = {
        ...minimizeToTrayCache,
        ...readMinimizeFlags(p.values)
      }
    }
  }
  if (method === 'log.open-folder') return openLogFolder()
  if (method === 'device.info') return getDeviceInfo()
  if (method === 'status-window.show') {
    void showStatusWindow()
    return { shown: true }
  }

  if (isDialogBridgeMethod(method)) {
    return invokeDialogBridgeMethod(method, params, mainWindow ?? BrowserWindow.getFocusedWindow())
  }
  if (flags.disableUpdateChecker) {
    if (method === 'app.update.check') {
      return { available: false, version: null, error: '--disable-update-checker' }
    }
    if (method === 'app.update.status') {
      return { status: 'Disabled', disable: true }
    }
  }
  if (method === 'powerPlans.getList') {
    if (process.platform !== 'win32') return { plans: [] }
    return listPowerPlans().then(
      (plans) => ({ plans }),
      () => ({ plans: [] })
    )
  }
  if (method === 'powerPlans.setActive') {
    if (process.platform !== 'win32') {
      throw new Error('Windows only')
    }
    const guid = (params as { guid?: unknown } | null)?.guid
    if (typeof guid !== 'string' || guid.length === 0) {
      throw new Error('A power plan GUID is required.')
    }
    return setActivePowerPlan(guid).then(() => ({ ok: true }))
  }
  if (method === 'power.restart') {
    claimPowerAction()
    return restartSystem()
  }
  if (method === 'power.shutdown') {
    claimPowerAction()
    return shutdownSystem()
  }
  if (method === 'power.sleep') {
    claimPowerAction()
    return sleepSystem()
  }
  if (method === 'update.getRelease') {
    return getLatestRelease().then((release) => ({ release }))
  }
  if (method === 'update.download') {
    lastVerifiedInstallerPath = null
    const started = Date.now()
    return downloadLatestUpdate((progress: DownloadProgress) => {
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.send('bridge:event', 'update.download-progress', {
          ...progress,
          elapsedMs: Date.now() - started
        })
      }
    }).then(
      (path) => {
        lastVerifiedInstallerPath = path
        return { ok: true, path }
      },
      (error: Error) => {
        lastVerifiedInstallerPath = null
        return { ok: false, error: error.message }
      }
    )
  }
  if (method === 'update.launchInstaller') {
    return launchVerifiedInstaller(params)
  }
  return hostClient.invoke(method, params)
}

function parseWindowVisibilityAction(data: unknown): 'Show' | 'Hide' | null {
  if (data == null || typeof data !== 'object') return null
  const action = (data as { action?: unknown }).action
  if (action === 'Show' || action === 'Hide') return action
  return null
}

function applyHostWindowVisibility(data: unknown): void {
  const action = parseWindowVisibilityAction(data)
  if (action == null) return
  if (action === 'Hide') {
    if (cachedShouldMinimizeToTray(['MinimizeOnClose', 'MinimizeToTray'])) {
      enterBackground()
      return
    }
    const win = mainWindow
    if (win && !win.isDestroyed()) win.hide()
    return
  }
  restoreMainWindow()
}

function flushPendingTrayNavigation(window: BrowserWindow): void {
  if (!pendingTrayRoute || window.isDestroyed()) return
  const route = pendingTrayRoute
  pendingTrayRoute = null
  window.webContents.send('bridge:event', 'tray:navigate', { route })
}

function flushBufferedHostEvents(window: BrowserWindow): void {
  if (window.isDestroyed()) return
  // Always re-emit the latest host.ready so late renderer subscribers (dashboard
  // waitForHost, title bar, startup gates) observe readiness after a fast Host boot.
  const readyPayload = hostClient.lastReadyPayload
  if (hostClient.isReady) {
    window.webContents.send('bridge:event', 'host.ready', readyPayload ?? {})
  }
  for (const item of bufferedHostEvents) {
    if (item.event === 'host.ready') continue
    window.webContents.send('bridge:event', item.event, item.data)
  }
  bufferedHostEvents.length = 0
  flushPendingTrayNavigation(window)
}

function attachHostEventForwarding(): void {
  if (hostEventsAttached) return
  hostEventsAttached = true
  // Forward all host events (sensors.updated, settings.changed, osd.changed, …).
  // A fixed whitelist previously dropped sensors.updated, so gauges stayed at "-".
  hostClient.onAny((event, data) => {
    // Aggregate host log lines into the unified log folder.
    if (event === 'host.log') {
      writeHostLog(typeof data === 'string' ? data : JSON.stringify(data))
    }
    bufferOrSendHostEvent(event, data)
  })
}

function forwardHostEvents(window: BrowserWindow): void {
  attachHostEventForwarding()
  const flush = (): void => flushBufferedHostEvents(window)
  window.webContents.on('did-finish-load', flush)
  if (!window.webContents.isLoadingMainFrame()) {
    flush()
  }
}

type MinimizeSetting = 'MinimizeOnClose' | 'MinimizeToTray'

interface MinimizeToTrayCache {
  MinimizeOnClose: boolean
  MinimizeToTray: boolean
}

// Matches ApplicationSection defaults: MinimizeToTray on, MinimizeOnClose off.
let minimizeToTrayCache: MinimizeToTrayCache = {
  MinimizeOnClose: false,
  MinimizeToTray: true
}

function readMinimizeFlags(value: Record<string, unknown> | undefined): MinimizeToTrayCache {
  return {
    MinimizeOnClose: value?.['MinimizeOnClose'] === true,
    MinimizeToTray: value?.['MinimizeToTray'] !== false
  }
}

function cachedShouldMinimizeToTray(keys: MinimizeSetting[]): boolean {
  return keys.some((key) => minimizeToTrayCache[key])
}

function hideMainWindowToTray(): void {
  enterBackground()
}

function enterBackground(): void {
  if (isQuitting) return
  trayOnlySession = true
  persistMainWindowBounds()
  destroyStatusWindow()
  destroyTrayPopup()
  if (!isOsdVisible()) {
    suspendOsdWindow()
  }
  const win = mainWindow
  if (win && !win.isDestroyed()) {
    if (win.isVisible()) win.hide()
    const generation = ++backgroundDestroyGeneration
    setImmediate(() => {
      if (isQuitting || generation !== backgroundDestroyGeneration) return
      const pending = mainWindow
      if (pending && !pending.isDestroyed()) {
        pending.destroy()
      }
      // Trim once after the renderer is gone; a pre-destroy pass would clear
      // caches the live DOM immediately repopulates and pay an extra main
      // process GC pause while the window is still hiding.
      void trimChromiumCaches()
    })
  } else {
    setSurfaceVisible('main', false)
    void trimChromiumCaches()
  }
  setTimeout(() => {
    void trimChromiumCaches()
    void logMemoryUsage('tray background')
  }, 1500)
}

function restoreMainWindow(route?: string): void {
  if (isQuitting) return
  backgroundDestroyGeneration++
  if (route) pendingTrayRoute = route
  if (mainWindow && !mainWindow.isDestroyed()) {
    if (mainWindow.isMinimized()) mainWindow.restore()
    mainWindow.show()
    mainWindow.focus()
    if (pendingTrayRoute && !mainWindow.webContents.isLoadingMainFrame()) {
      flushPendingTrayNavigation(mainWindow)
    }
    return
  }
  createWindow()
}

async function trimChromiumCaches(): Promise<void> {
  try {
    await session.defaultSession.clearCache()
    await session.defaultSession.clearStorageData({
      storages: ['serviceworkers', 'cachestorage']
    })
    if (typeof global.gc === 'function') {
      try {
        global.gc()
      } catch {
        // Optional V8 garbage collection
      }
    }
    const proc = process as unknown as { trimWorkingSet?: () => void }
    if (typeof proc.trimWorkingSet === 'function') {
      try {
        proc.trimWorkingSet()
      } catch {
        // Optional Windows working-set trim
      }
    }
  } catch (error) {
    console.error('[main] failed to clear session cache:', error)
  }
}

function forceCloseMainWindow(): void {
  trayOnlySession = false
  const win = mainWindow
  if (!win || win.isDestroyed()) {
    allowCloseOnce = false
    return
  }
  allowCloseOnce = true
  setImmediate(() => {
    const pending = mainWindow
    if (!pending || pending.isDestroyed()) {
      allowCloseOnce = false
      return
    }
    pending.close()
  })
}

async function refreshMinimizeToTrayCache(): Promise<void> {
  try {
    const result = (await hostClient.invoke('settings.get', { scope: 'application' })) as
      | { value?: Record<string, unknown> }
      | null
      | undefined
    minimizeToTrayCache = readMinimizeFlags(result?.value)
  } catch (error) {
    console.error('[main] failed to read settings:', error)
  }
}

function notifyHostUiActivity(active: boolean): void {
  if (!hostClient.isReady) return
  void hostClient
    .invoke('app.setUiActive', { active, pid: process.pid })
    .catch((error) => {
      console.error('[main] failed to apply UI activity scheduling:', error)
    })
}

function broadcastUiVisibility(active: boolean): void {
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send('bridge:event', 'app:ui-visibility', { active })
  }
  notifyHostUiActivity(active)
}

function resolveWindowIcon(): string | undefined {
  // Windows uses the multi-size ICO; Linux/macOS need a PNG (ICO is ignored).
  if (process.platform === 'win32') {
    const candidates = [
      join(PROJECT_ROOT, 'resources', 'icon.ico'),
      join(PROJECT_ROOT, 'buildResources', 'icon.ico'),
      join(PROJECT_ROOT, 'resources', 'icon.png')
    ]
    return candidates.find((candidate) => existsSync(candidate))
  }
  const candidates = [
    join(PROJECT_ROOT, 'resources', 'icon.png'),
    join(PROJECT_ROOT, 'buildResources', 'icon-512.png')
  ]
  return candidates.find((candidate) => existsSync(candidate))
}

/**
 * Mirrors Electron MainWindow.OpenLog: reveal the logs folder in Explorer. Electron's
 * "logs" path is created on demand so the folder exists before the host has
 * written anything.
 */
async function openLogFolder(): Promise<{ ok: boolean }> {
  try {
    const dir = logsDirectory()
    if (!existsSync(dir)) mkdirSync(dir, { recursive: true })
    const error = await shell.openPath(dir)
    return { ok: error.length === 0 }
  } catch (error) {
    console.error('[main] failed to open log folder:', error)
    return { ok: false }
  }
}

// macOS login item / Linux XDG autostart live in autorun.ts.

/**
 * Windows DWM backdrop: mica needs Windows 11 (build 22000+); older Windows
 * gets acrylic (Win10 1809+ system backdrop). macOS/Linux never call this.
 */
function windowsBackgroundMaterial(): 'mica' | 'acrylic' {
  const version = process.getSystemVersion()
  const build = Number(version.split('.').pop())
  if (version.startsWith('10') && Number.isFinite(build) && build >= 22000) {
    return 'mica'
  }
  return 'acrylic'
}

/**
 * Applies the content minimum derived from the design CSS minimum and the
 * effective zoom, and grows the window if it dropped below the new minimum.
 * The main window is frameless (and macOS uses hiddenInset), so window size
 * equals content size and setMinimumSize can carry content semantics.
 */
function applyMainWindowMinSize(): void {
  if (!mainWindow || mainWindow.isDestroyed()) return
  const zoom = effectiveZoom()
  const minWidth = Math.ceil(DESIGN_MIN_CONTENT_WIDTH * zoom)
  const minHeight = Math.ceil(DESIGN_MIN_CONTENT_HEIGHT * zoom)
  mainWindow.setMinimumSize(minWidth, minHeight)
  if (mainWindow.isMaximized() || mainWindow.isFullScreen()) return
  const [width, height] = mainWindow.getContentSize()
  if (width < minWidth || height < minHeight) {
    mainWindow.setContentSize(Math.max(width, minWidth), Math.max(height, minHeight))
  }
}

/** Saves the normal bounds + maximized flag for the next launch. */
function persistMainWindowBounds(): void {
  if (!mainWindow || mainWindow.isDestroyed()) return
  const bounds = mainWindow.getNormalBounds()
  updateWindowState({
    x: bounds.x,
    y: bounds.y,
    width: bounds.width,
    height: bounds.height,
    isMaximized: mainWindow.isMaximized()
  })
}

function createWindow(): void {
  const icon = resolveWindowIcon()
  const isMac = process.platform === 'darwin'
  const zoom = effectiveZoom()
  const minWidth = Math.ceil(DESIGN_MIN_CONTENT_WIDTH * zoom)
  const minHeight = Math.ceil(DESIGN_MIN_CONTENT_HEIGHT * zoom)

  // Restore the previous session's bounds clamped into a visible work area
  // (the saved display may be gone or its DPI/resolution may have changed).
  const persisted = readWindowState()
  const restoredSize = {
    width: Math.max(persisted.width ?? 1024, minWidth),
    height: Math.max(persisted.height ?? 640, minHeight)
  }
  const restoredBounds =
    persisted.x !== undefined && persisted.y !== undefined
      ? constrainToWorkArea({ x: persisted.x, y: persisted.y, ...restoredSize })
      : null

  mainWindow = new BrowserWindow({
    // Default 1024x640 DIP matches the original client. Sizes are content
    // sizes (useContentSize); the minimum is the design CSS minimum converted
    // through the effective zoom so all platforms and scale levels bottom out
    // at the same visible content, with container queries handling narrower
    // layouts below the optimization/cleanup two-column threshold.
    width: restoredBounds?.width ?? restoredSize.width,
    height: restoredBounds?.height ?? restoredSize.height,
    ...(restoredBounds ? { x: restoredBounds.x, y: restoredBounds.y } : {}),
    useContentSize: true,
    minWidth,
    minHeight,
    show: false,
    title: 'Universal Device Toolkit',
    // macOS: native title bar with the traffic lights (red/yellow/green) at the
    // top-left and a vibrancy backdrop — the platform convention. Windows/Linux
    // keep the frameless custom title bar with right-aligned window buttons.
    ...(isMac
      ? {
          titleBarStyle: 'hiddenInset' as const,
          trafficLightPosition: { x: 12, y: 11 },
          vibrancy: 'under-window' as const,
          visualEffectState: 'active' as const
        }
      : process.platform === 'linux'
        ? {
            // Linux: frameless custom title bar like Windows. backgroundMaterial
            // (mica/acrylic) is a Windows-only API — Electron ignores it on
            // Linux. Match the renderer: dark #202020, light mica-approx chrome
            // (WindowBackdrop.css --udt-mica-chrome ≈ #d5ded5).
            autoHideMenuBar: true,
            frame: false,
            backgroundColor: linuxWindowBackgroundColor(),
            backgroundMaterial: 'none' as const
          }
        : {
            autoHideMenuBar: true,
            frame: false,
            backgroundColor: '#00000000',
            backgroundMaterial: windowsBackgroundMaterial()
          }),
    ...(icon ? { icon } : {}),
    webPreferences: {
      preload: join(__dirname, '../preload/index.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      spellcheck: false,
      backgroundThrottling: true,
      additionalArguments:
        installerSelection == null ? [] : buildInstallerRendererArguments(installerSelection),
      // First paint already at the effective zoom (installZoomAutoApply keeps
      // later navigations in sync).
      zoomFactor: zoom
    }
  })

  mainWindow.webContents.setWindowOpenHandler(() => ({ action: 'deny' }))
  mainWindow.webContents.on('will-navigate', (event, targetUrl) => {
    if (!isAllowedRendererNavigation(targetUrl)) {
      event.preventDefault()
      console.warn(`[main] blocked renderer navigation: ${targetUrl}`)
    }
  })

  if (process.platform === 'win32') {
    currentBackgroundMaterial = windowsBackgroundMaterial()
  }

  // Keep the maximized window inside the monitor work area (safety net for
  // desktop-dock overlays).
  attachMaximizeWorkAreaClamp(mainWindow)

  mainWindow.on('ready-to-show', () => {
    if (!mainWindow || mainWindow.isDestroyed()) return
    if (persisted.isMaximized) {
      mainWindow.maximize()
    }
    mainWindow.show()
  })

  // Persist geometry for the next launch (close fires before destruction).
  mainWindow.on('close', persistMainWindowBounds)

  mainWindow.on('close', (event) => {
    if (isQuitting || allowCloseOnce) {
      allowCloseOnce = false
      return
    }
    const win = mainWindow
    if (!win || win.isDestroyed()) return
    // Electron only honors a synchronous preventDefault. An async settings
    // read after this handler returns cannot cancel the close.
    event.preventDefault()
    if (process.platform === 'darwin') {
      // macOS convention: the red traffic light does not quit. Destroy the
      // renderer so tray-only memory drops; Dock / tray recreate it on activate.
      enterBackground()
      return
    }
    if (cachedShouldMinimizeToTray(['MinimizeOnClose', 'MinimizeToTray'])) {
      enterBackground()
      return
    }
    forceCloseMainWindow()
  })

  mainWindow.on('minimize', () => {
    if (cachedShouldMinimizeToTray(['MinimizeToTray'])) {
      hideMainWindowToTray()
    }
  })

  mainWindow.on('maximize', () => {
    const win = mainWindow
    if (win && !win.isDestroyed()) {
      win.webContents.send('window:maximized-changed', true)
    }
  })

  mainWindow.on('unmaximize', () => {
    const win = mainWindow
    if (win && !win.isDestroyed()) {
      win.webContents.send('window:maximized-changed', false)
    }
  })

  mainWindow.on('closed', () => {
    allowCloseOnce = false
    setSurfaceVisible('main', false)
    mainWindow = null
    if (isQuitting || !trayOnlySession) {
      destroyTray()
      destroyOsdWindow()
      destroyStatusWindow()
    }
  })

  const syncMainVisibility = (): void => {
    const win = mainWindow
    if (!win || win.isDestroyed()) {
      setSurfaceVisible('main', false)
      return
    }
    setSurfaceVisible('main', win.isVisible() && !win.isMinimized())
  }
  mainWindow.on('show', syncMainVisibility)
  mainWindow.on('hide', syncMainVisibility)
  mainWindow.on('minimize', syncMainVisibility)
  mainWindow.on('restore', syncMainVisibility)

  if (!app.isPackaged && process.env['ELECTRON_RENDERER_URL']) {
    mainWindow.loadURL(process.env['ELECTRON_RENDERER_URL'])
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }
  forwardHostEvents(mainWindow)
}

function startHost(): void {
  attachHostEventForwarding()
  applyShellLaunchEnvironment()
  try {
    const hostPath = resolveHostPath()
    const hostArgs = toHostArgs(flags)
    for (const argument of buildInstallerHostArguments(installerSelection)) {
      if (!hostArgs.includes(argument)) hostArgs.push(argument)
    }
    console.log(`[main] starting host: ${hostPath}${hostArgs.length > 0 ? ` ${hostArgs.join(' ')}` : ''}`)
    hostClient.start(hostPath, hostArgs)
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    console.error(`[main] failed to start host: ${message}`)
    hostClient.reportFatalError(message)
  }
}

/**
 * Host Autorun.Set creates a logon task from the current process. When Electron
 * spawned Host, that would be Host.exe. Point the task at the UI instead.
 */
function applyShellLaunchEnvironment(): void {
  process.env['UDT_SHELL_PATH'] = process.execPath
  if (app.isPackaged) {
    process.env['UDT_SHELL_ARGS'] = '--minimized'
    return
  }
  const extra = process.argv.slice(1).filter((arg) => arg !== '--minimized')
  const quoted = extra
    .map((arg) => (arg.includes(' ') ? `"${arg}"` : arg))
    .join(' ')
  process.env['UDT_SHELL_ARGS'] = `${quoted} --minimized`.trim()
}

app.whenReady().then(() => {
  initMainLogger()
  session.defaultSession.setSpellCheckerEnabled(false)
  session.defaultSession.setPermissionRequestHandler((_webContents, _permission, callback) => callback(false))
  // Apply the last persisted interface scale before the window exists so the
  // first paint (and the derived minimum size) already match; the renderer
  // re-pushes its localStorage value over IPC right after boot.
  setUiScale(readWindowState().uiScale ?? 1)
  // Notifications: the renderer uses the in-app notification center (no OS
  // Notification API), so no permission wiring is needed here. Should native
  // notifications ever be added: macOS requires the Notification permission
  // (Electron requests it automatically on first use), Linux has no special
  // handling beyond the desktop environment's own notification settings.
  console.log('[main] app ready')
  installerSelection = readInstallerSelection()
  if (installerSelection != null) {
    console.log(
      `[main] installer selection: language=${installerSelection.language}, deviceMode=${installerSelection.deviceMode}, features=${Object.entries(installerSelection.features)
        .map(([key, enabled]) => `${key}=${enabled ? '1' : '0'}`)
        .join(',')}`
    )
  }
  if (flags.isTraceEnabled) {
    console.log(`[main] flags: ${describeFlags(flags)}`)
  }

  // Available before createWindow so the renderer can observe readiness immediately.
  ipcMain.handle('host:get-status', () => ({
    running: hostClient.isRunning,
    ready: hostClient.isReady,
    lastError: hostClient.lastFailure,
    readyPayload: hostClient.lastReadyPayload
  }))

  ipcMain.handle('bridge:invoke', async (event, method: unknown, params?: unknown) => {
    assertMainFrame(event)
    if (typeof method !== 'string' || method.length === 0) {
      throw new Error('A bridge method name is required.')
    }
    if (params != null && typeof params !== 'object') {
      throw new Error('Bridge parameters must be an object.')
    }
    return invokeBridgeMethod(method, params)
  })

  ipcMain.on('window:minimize', () => {
    const win = mainWindow
    if (!win || win.isDestroyed()) return
    if (cachedShouldMinimizeToTray(['MinimizeToTray'])) {
      hideMainWindowToTray()
    } else {
      win.minimize()
    }
  })

  ipcMain.on('window:maximize-toggle', () => {
    if (!mainWindow) return
    if (mainWindow.isMaximized()) {
      mainWindow.unmaximize()
    } else {
      mainWindow.maximize()
    }
  })

  ipcMain.on('window:close', () => {
    if (isQuitting || !mainWindow || mainWindow.isDestroyed()) {
      mainWindow?.close()
      return
    }
    if (process.platform === 'darwin') {
      enterBackground()
      return
    }
    if (cachedShouldMinimizeToTray(['MinimizeOnClose', 'MinimizeToTray'])) {
      enterBackground()
      return
    }
    forceCloseMainWindow()
  })

  ipcMain.handle('window:is-maximized', () => mainWindow?.isMaximized() ?? false)

  // Single source of truth for renderer zoom: the renderer pushes its persisted
  // "Interface scale" here; main applies platformBase x uiScale to every surface
  // and re-derives the content minimum.
  ipcMain.handle('window:set-ui-scale', (_event, scale: unknown) => {
    if (typeof scale !== 'number' || !Number.isFinite(scale) || scale <= 0) {
      throw new Error('A positive finite UI scale is required.')
    }
    const applied = setUiScale(scale)
    updateWindowState({ uiScale: applied })
    applyMainWindowMinSize()
    return { ok: true, scale: applied }
  })

  ipcMain.handle('window:set-background-material', (_event, material: unknown) => {
    // macOS backdrop is the fixed vibrancy chosen at window creation; there is
    // no runtime switch, so return a silent no-op instead of erroring.
    if (process.platform === 'darwin') return
    if (material !== 'none' && material !== 'mica' && material !== 'acrylic') {
      throw new Error(`Unsupported window background material: ${String(material)}`)
    }
    // Windows-only API (DWM backdrop); no-op elsewhere.
    if (process.platform === 'win32') {
      currentBackgroundMaterial = material
      applyMainWindowBackgroundMaterial(material)
    }
  })

  // DWM backdrop materials (mica/acrylic) follow the OS theme, not the in-app
  // theme — a dark app on a light system renders a washed-out white backdrop.
  // Pin nativeTheme to the in-app mode so the material matches (Windows only;
  // harmless no-op elsewhere).
  ipcMain.on('window:set-theme-source', (_event, source: unknown) => {
    if (source !== 'system' && source !== 'light' && source !== 'dark') return
    nativeTheme.themeSource = source
    applyLinuxWindowBackgroundColor()
  })

  ipcMain.handle('shell:open-log-folder', async () => {
    // Unified log folder: main.log / renderer.log / host.log live together.
    shell.openPath(logsDirectory())
  })

  // Renderer → main log channel (utils/logger.ts double-writes console + file).
  ipcMain.on('log:write', (_event, payload: unknown) => {
    const record = (payload ?? {}) as { level?: unknown; message?: unknown }
    const level = isValidLogLevel(record.level) ? record.level : 'info'
    const message = typeof record.message === 'string' ? record.message : String(record.message ?? '')
    writeRendererLog(level, message)
  })

  // Mirrors Electron MainWindow.OpenLog: open the logs directory (created on demand).
  ipcMain.handle('log.open-folder', () => openLogFolder())

  // Mirrors Electron MainWindow.LoadDeviceInfo (MachineCompatibility.GetMachineInformationAsync).
  ipcMain.handle('device.info', () => getDeviceInfo())

  // Mirrors Electron AboutPage "Data" / "Temp" folder buttons (Folders.AppData / Path.GetTempPath).
  ipcMain.handle('shell:open-app-folder', async (_event, kind: unknown) => {
    let target: string
    if (kind === 'data') {
      // Host stores app data next to args.txt (Flags.cs mirrors Folders.AppData,
      // honoring the UDT_APPDATA_OVERRIDE environment variable).
      const override = process.env['UDT_APPDATA_OVERRIDE']
      target = override ?? join(process.env['LOCALAPPDATA'] ?? app.getPath('userData'), 'UniversalDeviceToolkit')
    } else if (kind === 'temp') {
      target = tmpdir()
    } else if (kind === 'log') {
      const result = await hostClient.invoke('app.getLogPath', {}) as { path?: unknown }
      if (typeof result.path !== 'string' || result.path.length === 0) {
        throw new Error('The host did not provide a log file path.')
      }
      target = result.path
    } else {
      throw new Error(`Unsupported folder kind: ${String(kind)}`)
    }
    if (!existsSync(target)) {
      throw new Error(`Folder does not exist: ${target}`)
    }
    const error = await shell.openPath(target)
    if (error) {
      throw new Error(error)
    }
    return { opened: true }
  })

  // Mirrors Electron Process.Start(url) / explorer.exe for file paths (used by the
  // Utils windows: update window, device info warranty link, crash report).
  ipcMain.handle('shell:open-external', async (event, url: unknown) => {
    assertMainFrame(event)
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
    return { opened: true }
  })

  ipcMain.handle('shell:open-path', async (event, path: unknown) => {
    assertMainFrame(event)
    const target = resolveSafeOpenPath(path)
    const error = await shell.openPath(target)
    if (error) {
      throw new Error(error)
    }
    return { opened: true }
  })

  // Port of Electron ClipboardExtensions.SetProcesses/GetProcesses: one line per
  // executable path; reads are filtered to existing paths and deduplicated.
  ipcMain.handle('clipboard:write-lines', (_event, payload: unknown) => {
    const payloadLines = (payload as { lines?: unknown } | null)?.lines
    if (!Array.isArray(payloadLines) || payloadLines.some((line) => typeof line !== 'string')) {
      throw new Error('A lines array of strings is required.')
    }
    const lines = payloadLines as string[]
    clipboard.writeText(lines.join('\n'))
    return { ok: true }
  })

  // Mirrors Electron UnsupportedWindow.Exit / LanguageSelectorWindow.Exit: terminate
  // the whole application instead of hiding the window.
  ipcMain.on('app:quit', () => {
    isQuitting = true
    trayOnlySession = false
    app.quit()
  })

  // macOS / Linux: login item / XDG autostart. Windows uses Host app.setAutorun
  // (scheduled task) and must not also write a registry login item.
  ipcMain.handle('app:set-autorun', (_event, enabled: unknown) => {
    if (process.platform === 'win32') {
      return { ok: true, enabled: enabled === true }
    }
    const openAtLogin = enabled === true
    try {
      applyAutorun(openAtLogin)
    } catch (error) {
      return {
        ok: false,
        enabled: false,
        error: error instanceof Error ? error.message : String(error)
      }
    }
    return { ok: true, enabled: openAtLogin }
  })

  ipcMain.handle('app:get-autorun', () => {
    if (process.platform === 'win32') {
      return { enabled: false }
    }
    return { enabled: readAutorun() }
  })

  // File pickers (dialog:select-*) are registered in dialogs.ts.
  registerFileDialogIpc(() => mainWindow)

  // Tray menu: language + quick-action refresh (Electron TrayHelper PipelinesChanged).
  ipcMain.on('tray:set-language', (_event, lang: unknown) => {
    if (typeof lang === 'string' && lang.length > 0) updateTrayLanguage(lang)
  })
  ipcMain.on('tray:refresh', () => {
    refreshTrayMenu()
  })

  // Attach forwarding before spawn so a fast host.ready is buffered rather than dropped.
  startHost()
  hostClient.on('host.ready', () => {
    notifyHostUiActivity(isUiActive())
    void refreshMinimizeToTrayCache()
  })
  if (hostClient.isReady) {
    void refreshMinimizeToTrayCache()
  }
  const trayOpts = {
    disableTooltip: flags.disableTrayTooltip,
    invokeHost: (method: string, params?: unknown) => hostClient.invoke(method, params),
    restoreWindow: (route?: string) => restoreMainWindow(route)
  }
  setMainWindowRef(() => mainWindow)
  setMainWindowRestore(() => restoreMainWindow())
  setUiActivityHandler(broadcastUiVisibility)
  // macOS: install the native system menu bar (App/File/Edit/View/Window/Help).
  installApplicationMenu()
  ipcMain.handle('app:memory-usage', () => reportMemoryUsage())
  setTimeout(() => {
    void trimChromiumCaches()
    void logMemoryUsage('after startup')
  }, 5000)
  initTray(() => mainWindow, trayOpts)
  if (flags.minimized) {
    // Start tray-only: no main renderer until the user restores from the tray.
    trayOnlySession = true
    notifyHostUiActivity(false)
  } else {
    createWindow()
  }
  // Rebuild the tray after automation.json loads (default Deactivate GPU quick action).
  hostClient.on('host.initialized', () => {
    void refreshMinimizeToTrayCache()
    refreshTrayMenu()
  })
  hostClient.on('host.ready', () => refreshTrayMenu())
  // Navigation item visibility lives in application settings.
  hostClient.on('settings.changed', () => {
    void refreshMinimizeToTrayCache()
    refreshTrayMenu()
  })
  hostClient.on('window.visibility', (data) => applyHostWindowVisibility(data))
  // OSD and the tray status popup are created lazily on first use (each costs
  // a renderer process ~60-90MB); initOsdWindow only registers subscriptions,
  // the window is built when the showOsd setting or an osd.changed event needs it.
  initOsdWindow()
  initStatusWindow()

  // Keep the main window inside a visible work area when display metrics
  // change (monitor unplug, resolution or DPI switch). The OSD window has its
  // own equivalent handler in osd-window.ts.
  const clampMainWindowToWorkArea = (): void => {
    const win = mainWindow
    if (!win || win.isDestroyed() || !win.isVisible()) return
    if (win.isMaximized() || win.isFullScreen()) return
    const bounds = win.getBounds()
    const clamped = constrainToWorkArea(bounds)
    if (
      clamped.x !== bounds.x ||
      clamped.y !== bounds.y ||
      clamped.width !== bounds.width ||
      clamped.height !== bounds.height
    ) {
      win.setBounds(clamped)
    }
  }
  screen.on('display-metrics-changed', clampMainWindowToWorkArea)
  screen.on('display-removed', clampMainWindowToWorkArea)

  // Forward OS power/session events to the renderer (Electron MainWindow listened
  // to the same transitions for OSD/status behavior).
  const systemEvents: Array<[string, string]> = [
    ['suspend', 'system.suspend'],
    ['resume', 'system.resume'],
    ['lock-screen', 'system.lock'],
    ['unlock-screen', 'system.unlock'],
    ['shutdown', 'system.shutdown']
  ]
  for (const [sourceEvent, bridgeEvent] of systemEvents) {
    powerMonitor.on(sourceEvent as 'suspend', () => {
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.send('bridge:event', bridgeEvent, {})
      }
    })
  }

  app.on('activate', () => {
    restoreMainWindow()
  })
})

app.on('window-all-closed', () => {
  console.log('[main] window-all-closed')
  if (trayOnlySession && isTrayActive() && !isQuitting) {
    return
  }
  if (process.platform !== 'darwin') {
    app.quit()
  }
})

app.on('before-quit', (event) => {
  console.log('[main] before-quit, host running:', hostClient.isRunning)
  isQuitting = true
  trayOnlySession = false
  cancelAllIdleDestroys()
  if (!hostClient.isRunning) return
  event.preventDefault()
  void hostClient.stop().finally(() => {
    console.log('[main] host stopped, quitting')
    app.quit()
  })
})

/**
 * Mirrors Electron AppDomain_UnhandledException / CrashReportHelper: save a crash
 * report file under userData/crash-reports and surface it to the renderer
 * (CrashReportNotificationModal) via the bridge event bus.
 */
function writeCrashReport(error: Error, kind: 'uncaughtException' | 'unhandledRejection'): void {
  try {
    const dir = join(app.getPath('userData'), 'crash-reports')
    mkdirSync(dir, { recursive: true })
    const stamp = new Date().toISOString().replace(/[:.]/g, '-')
    const filePath = join(dir, `udt-crash-${stamp}.txt`)
    const lines = [
      `[${kind}]`,
      `time: ${new Date().toLocaleString()}`,
      `version: ${app.getVersion()}`,
      `exception: ${error.name ?? 'Error'}`,
      `message: ${error.message ?? ''}`,
      `stack: ${error.stack ?? ''}`
    ]
    writeFileSync(filePath, lines.join('\n') + '\n', 'utf8')
    const report = {
      path: filePath,
      timestamp: new Date().toLocaleString(),
      appVersion: app.getVersion(),
      exceptionType: error.name ?? 'Error',
      exceptionMessage: error.message ?? '',
      stackTrace: (error.stack ?? '').slice(0, 1200)
    }
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('bridge:event', 'app.crash', report)
    }
  } catch {
    // The crash reporter must never mask the original failure.
  }
}

process.on('uncaughtException', (error) => {
  console.error('[main] uncaughtException:', error)
  writeCrashReport(error, 'uncaughtException')
})

process.on('unhandledRejection', (reason) => {
  const error = reason instanceof Error ? reason : new Error(String(reason))
  console.error('[main] unhandledRejection:', error)
  writeCrashReport(error, 'unhandledRejection')
})
