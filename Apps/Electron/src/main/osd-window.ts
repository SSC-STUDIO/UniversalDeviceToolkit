import { createOsdPresentation, DEFAULT_OSD_SETTINGS, type OsdSnapshot, type OsdFpsData, type OsdSettingsStore, type OsdItemName } from '../shared/osd-presentation'
import { randomBytes } from 'node:crypto'
import { BrowserWindow, globalShortcut, powerMonitor, screen } from 'electron'
import { hostClient } from './host-client'
import { effectiveZoom } from './ui-scale'
import { cancelIdleDestroy, scheduleIdleDestroy, setSurfaceVisible } from './ui-activity'

const presentation = createOsdPresentation()

/**
 * On-screen display (OSD) —port of the Electron OsdWindowBase family:
 *
 * - OsdWindowBase.cs: frameless, transparent, always-on-top window; saved
 *   position restore, edge snapping (SnapThreshold), click-through while
 *   locked, sensor refresh loop (OsdRefreshInterval), severity coloring.
 * - OsdBarWindow.xaml(.cs): horizontal bar at the top center.
 * - OsdPanelWindow.xaml(.cs): vertical panel at the left edge.
 *
 * Style ("Panel" | "Bar"), appearance, thresholds and sensor items come from
 * the "osd" settings scope (osd.json). Position is persisted back into the
 * same scope on drag end. Sensor data is consumed from the shared
 * sensors.updated producer (same loop as the dashboard); FPS uses
 * sensors.subscribeFps (ref-counted in the host). OSD visibility is driven by
 * the host's "osd.changed" events (automation steps) and the showOsd setting.
 */

type OsdState = 'Hidden' | 'Show' | 'Toggle'

interface OsdEventData {
  state: OsdState
}

/** Snapshot projection —mirrors api/sensors.ts SensorSnapshot (camelCase). */
const OSD_WIDTH = 320
const OSD_HEIGHT = 96

let osdWindow: BrowserWindow | null = null
let unsubscribe: (() => void) | null = null
let unsubscribeSettings: (() => void) | null = null
let unsubscribeFps: (() => void) | null = null
let unsubscribeDisplay: (() => void) | null = null
let unsubscribePower: (() => void) | null = null
let unsubscribeReady: (() => void) | null = null

let settings: OsdSettingsStore = { ...DEFAULT_OSD_SETTINGS }
let showCpuAverageFrequency = false
let displayMemoryInGigabytes = false
let temperatureUnit: 'C' | 'F' = 'C'

let lastSnapshot: OsdSnapshot | null = null
let lastFps: OsdFpsData | null = null

let visible = false
let showRequested = false
let visibilityRequest = 0
let pageLoaded = false
let sensorsSubscribed = false
let subscribedInterval: number | null = null
let unsubscribeUpdated: (() => void) | null = null
let fpsSubscribed = false
let hostGeneration = 0
let subscriptionRefresh = Promise.resolve()
let positionSaveTimer: ReturnType<typeof setTimeout> | null = null
let lastAppearanceSignature = ''

// ── settings ────────────────────────────────────────────────────────────────

function mergeSettings(value: unknown): void {
  settings = presentation.configure(value)
}

/** Convert the camelCase model back to the PascalCase host store shape. */
function toHostStore(store: OsdSettingsStore): Record<string, unknown> {
  const pascal = (key: string): string => key.charAt(0).toUpperCase() + key.slice(1)
  const result: Record<string, unknown> = {}
  for (const [key, value] of Object.entries(store)) {
    result[pascal(key)] = value
  }
  return result
}

async function readSettings(): Promise<void> {
  try {
    const result = (await hostClient.invoke('settings.get', { scope: 'osd' })) as
      | { value?: unknown }
      | null
      | undefined
    mergeSettings(result?.value)
  } catch (error) {
    console.error('[osd] failed to read settings:', error)
  }
}

/** Retry the initial read —the host may still be starting up. */
async function readSettingsWithRetry(attempts = 10): Promise<void> {
  for (let attempt = 0; attempt < attempts; attempt++) {
    try {
      const result = (await hostClient.invoke('settings.get', { scope: 'osd' })) as
        | { value?: unknown }
        | null
        | undefined
      mergeSettings(result?.value)
      return
    } catch {
      await new Promise((resolve) => setTimeout(resolve, 500))
    }
  }
}

async function readSiblingSettings(): Promise<void> {
  try {
    const [hardware, application] = (await Promise.all([
      hostClient.invoke('settings.get', { scope: 'hardwareSensors' }),
      hostClient.invoke('settings.get', { scope: 'application' })
    ])) as [{ value?: Record<string, unknown> }, { value?: Record<string, unknown> }]
    const hardwareRaw = (hardware.value ?? {}) as Record<string, unknown>
    const applicationRaw = (application.value ?? {}) as Record<string, unknown>
    showCpuAverageFrequency = hardwareRaw['ShowCpuAverageFrequency'] === true
    displayMemoryInGigabytes = hardwareRaw['DisplayMemoryInGigabytes'] === true
    const unit = applicationRaw['TemperatureUnit']
    temperatureUnit = unit === 'F' ? 'F' : 'C'
  } catch {
    // Keep defaults when the host is not reachable yet.
  }
}

/** Persist the whole in-memory store; settings.set replaces the full scope. */
async function writeSettings(): Promise<void> {
  try {
    await hostClient.invoke('settings.set', { scope: 'osd', value: toHostStore(settings) })
    await hostClient.invoke('settings.save', { scopes: ['osd'] })
  } catch (error) {
    console.error('[osd] failed to save settings:', error)
  }
}

// ── window lifecycle ────────────────────────────────────────────────────────

function isMiniStyle(): boolean {
  return settings.selectedStyleIndex === 2
}

function isBarStyle(): boolean {
  return settings.selectedStyleIndex === 1
}

function savedPosition(): { x: number | null; y: number | null } {
  return isBarStyle() || isMiniStyle()
    ? { x: settings.barPositionX, y: settings.barPositionY }
    : { x: settings.panelPositionX, y: settings.panelPositionY }
}

function savePosition(x: number, y: number): void {
  if (isBarStyle() || isMiniStyle()) {
    settings.barPositionX = x
    settings.barPositionY = y
  } else {
    settings.panelPositionX = x
    settings.panelPositionY = y
  }
  if (positionSaveTimer) clearTimeout(positionSaveTimer)
  positionSaveTimer = setTimeout(() => {
    positionSaveTimer = null
    void writeSettings()
  }, 400)
}

/** Overlap with a work area below this is treated as unreachable by the user. */
const MIN_VISIBLE_PX = 24

/**
 * screen.getDisplayMatching always resolves to the nearest display and never
 * throws, so it cannot answer "is this rectangle still on a monitor?" — the
 * work areas have to be intersected explicitly. Without this the OSD keeps a
 * position that belonged to an unplugged or rescaled monitor and stays
 * invisible.
 */
function rectOnScreen(x: number, y: number, width: number, height: number): boolean {
  const minWidth = Math.min(MIN_VISIBLE_PX, width)
  const minHeight = Math.min(MIN_VISIBLE_PX, height)
  return screen.getAllDisplays().some((display) => {
    const area = display.workArea
    const overlapWidth = Math.min(x + width, area.x + area.width) - Math.max(x, area.x)
    const overlapHeight = Math.min(y + height, area.y + area.height) - Math.max(y, area.y)
    return overlapWidth >= minWidth && overlapHeight >= minHeight
  })
}

function isPositionOnScreen(x: number, y: number): boolean {
  const [width, height] = osdWindow && !osdWindow.isDestroyed() ? osdWindow.getSize() : [100, 30]
  return rectOnScreen(x, y, width, height)
}

function setDefaultWindowPosition(): void {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  const { workArea } = screen.getPrimaryDisplay()
  const [width, height] = win.getSize()
  if (isBarStyle() || isMiniStyle()) {
    win.setPosition(
      Math.round(workArea.x + (workArea.width - width) / 2),
      Math.round(workArea.y + (isMiniStyle() ? 10 : 0))
    )
  } else {
    win.setPosition(
      Math.round(workArea.x),
      Math.round(workArea.y + (workArea.height - height) / 2)
    )
  }
}

function setWindowPosition(): void {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  const saved = savedPosition()
  if (saved.x !== null && saved.y !== null && isPositionOnScreen(saved.x, saved.y)) {
    win.setPosition(Math.round(saved.x), Math.round(saved.y))
    return
  }
  setDefaultWindowPosition()
}

/** Electron OnMouseLeftButtonDown snapping + clamping against the work area. */
function snapAndClampPosition(): void {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  const [x, y] = win.getPosition()
  const [width, height] = win.getSize()
  const { workArea } = screen.getDisplayMatching({ x, y, width, height })
  const threshold = Math.max(0, settings.snapThreshold)

  let left = x
  let top = y
  if (Math.abs(left - workArea.x) < threshold) left = workArea.x
  else if (Math.abs(workArea.x + workArea.width - (left + width)) < threshold) {
    left = workArea.x + workArea.width - width
  }

  if (Math.abs(top - workArea.y) < threshold) top = workArea.y
  else if (Math.abs(workArea.y + workArea.height - (top + height)) < threshold) {
    top = workArea.y + workArea.height - height
  }

  left = Math.min(Math.max(left, workArea.x), workArea.x + workArea.width - width)
  top = Math.min(Math.max(top, workArea.y), workArea.y + workArea.height - height)

  if (left !== x || top !== y) win.setPosition(Math.round(left), Math.round(top))
  savePosition(Math.round(left), Math.round(top))
}

function onDisplayMetricsChanged(): void {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  const [x, y] = win.getPosition()
  if (!isPositionOnScreen(x, y)) setDefaultWindowPosition()
}

// ── data refresh ────────────────────────────────────────────────────────────

const FPS_ITEMS: OsdItemName[] = ['Fps', 'LowFps', 'FrameTime']

function fpsItemsActive(): boolean {
  return FPS_ITEMS.some((item) => settings.items.includes(item))
}

async function updateFpsSubscription(generation: number): Promise<void> {
  const shouldSubscribe = visible && fpsItemsActive()
  if (shouldSubscribe) {
    if (!fpsSubscribed) {
      await hostClient.invoke('sensors.subscribeFps', {})
      if (generation !== hostGeneration) return
      fpsSubscribed = true
    }
    if (visible && fpsItemsActive() && !unsubscribeFps) {
      unsubscribeFps = hostClient.on('sensors.fpsUpdated', (data) => {
        if (!visible) return
        lastFps = (data ?? null) as OsdFpsData | null
        updateValues()
      })
    }
  } else if (!shouldSubscribe && fpsSubscribed) {
    fpsSubscribed = false
    unsubscribeFps?.()
    unsubscribeFps = null
    await hostClient.invoke('sensors.unsubscribeFps', {})
  }
}

async function updateSensorSubscription(generation: number): Promise<void> {
  if (!visible) {
    if (sensorsSubscribed) {
      sensorsSubscribed = false
      subscribedInterval = null
      await hostClient.invoke('sensors.unsubscribe', { subscriberId: 'osd' })
    }
    return
  }
  const intervalSec = Math.max(0.5, settings.osdRefreshInterval)
  if (!sensorsSubscribed || subscribedInterval !== intervalSec) {
    await hostClient.invoke('sensors.subscribe', { intervalSec, subscriberId: 'osd' })
    if (generation !== hostGeneration) return
    sensorsSubscribed = true
    subscribedInterval = intervalSec
    void hostClient
      .invoke('sensors.getSnapshot', {})
      .then((snapshot) => {
        if (generation !== hostGeneration || !visible || snapshot == null) return
        lastSnapshot = snapshot as OsdSnapshot
        updateValues()
      })
      .catch((error) => console.error('[osd] failed to read sensor snapshot:', error))
  }
  if (visible && !unsubscribeUpdated) {
    unsubscribeUpdated = hostClient.on('sensors.updated', (data) => {
      if (!visible) return
      lastSnapshot = (data ?? null) as OsdSnapshot | null
      updateValues()
    })
  }
}

function startRefresh(): void {
  const generation = hostGeneration
  // Host subscriptions are stateful; complete an old subscribe before a hide
  // removes it, and never apply an old Host's result to its replacement.
  subscriptionRefresh = subscriptionRefresh.then(async () => {
    if (generation !== hostGeneration) return
    await updateSensorSubscription(generation)
    if (generation !== hostGeneration) return
    await updateFpsSubscription(generation)
  }).catch((error) => console.error('[osd] failed to update subscriptions:', error))
}

function stopRefresh(): void {
  unsubscribeUpdated?.()
  unsubscribeUpdated = null
  unsubscribeFps?.()
  unsubscribeFps = null
  startRefresh()
}

function onHostReady(): void {
  hostGeneration++
  sensorsSubscribed = false
  subscribedInterval = null
  fpsSubscribed = false
  unsubscribeUpdated?.()
  unsubscribeUpdated = null
  unsubscribeFps?.()
  unsubscribeFps = null
  lastSnapshot = null
  lastFps = null
  if (visible) {
    updateValues()
    startRefresh()
  }
}

// ── rendering ───────────────────────────────────────────────────────────────

function buildRenderModel() {
  return presentation.render(settings, lastSnapshot, lastFps, {
    showCpuAverageFrequency, displayMemoryInGigabytes, temperatureUnit
  })
}

function buildOsdUrl(): string {
  return `data:text/html;charset=utf-8,${encodeURIComponent(presentation.document(randomBytes(16).toString('base64')))}`
}

type ContentSize = [number, number]

function isContentSize(value: unknown): value is ContentSize {
  return (
    Array.isArray(value) &&
    value.length === 2 &&
    typeof value[0] === 'number' &&
    Number.isFinite(value[0]) &&
    typeof value[1] === 'number' &&
    Number.isFinite(value[1])
  )
}

function resizeToContent(win: BrowserWindow, size: ContentSize): void {
  // scrollWidth/Height are CSS px; the window is sized in DIPs, so convert
  // through the shared zoom factor (ui-scale.ts applies it to every surface).
  const zoom = effectiveZoom()
  win.setSize(
    Math.max(1, Math.round(size[0] * zoom)),
    Math.max(1, Math.round(size[1] * zoom))
  )
}

async function fitToContent(): Promise<void> {
  const win = osdWindow
  if (!win || win.isDestroyed()) return
  try {
    const size: unknown = await win.webContents.executeJavaScript(
      '[document.body.scrollWidth, document.body.scrollHeight]'
    )
    if (win.isDestroyed() || !isContentSize(size)) return
    resizeToContent(win, size)
  } catch {
    // Page not ready yet.
  }
}

function updateValues(): void {
  const win = osdWindow
  if (!win || win.isDestroyed() || !visible) return
  const serializedModel = encodeURIComponent(JSON.stringify(buildRenderModel()))
  const renderExpression =
    `globalThis.udtRender(JSON.parse(decodeURIComponent(${JSON.stringify(serializedModel)})))`
  void win.webContents
    .executeJavaScript(renderExpression)
    .then((size: unknown) => {
      if (!win.isDestroyed() && isContentSize(size)) resizeToContent(win, size)
    })
    .catch(() => undefined)
}

async function applyAppearance(): Promise<boolean> {
  const win = osdWindow
  if (!win || win.isDestroyed()) return false
  try {
    await win.loadURL(buildOsdUrl())
  } catch (error) {
    console.error('[osd] failed to load OSD page:', error)
    return false
  }
  if (win.isDestroyed() || win !== osdWindow) return false
  pageLoaded = true
  await fitToContent()
  if (win.isDestroyed() || win !== osdWindow) return false
  win.setIgnoreMouseEvents(settings.isLocked)
  if (visible) updateValues()
  return true
}

/** Fields that require a full page rebuild when they change. */
function appearanceSignature(store: OsdSettingsStore): string {
  return [
    store.selectedStyleIndex,
    store.backgroundOpacity,
    store.backgroundColor,
    store.fontSize,
    store.cornerRadiusTop,
    store.cornerRadiusBottom,
    store.isLocked,
    store.categoryColor,
    store.labelColor,
    store.valueColor,
    store.warningColor,
    store.criticalColor,
    store.separatorColor
  ].join('|')
}

/** Electron ApplyAppearanceSettings + RecalculatePosition on settings change. */
function onSettingsChanged(data: unknown): void {
  const changed = (data as { scope?: string; reason?: string } | null)?.scope
  if (changed === 'osd') {
    void readSettings().then(() => {
      const win = osdWindow
      if (win && !win.isDestroyed()) {
        const signature = appearanceSignature(settings)
        if (signature !== lastAppearanceSignature) {
          lastAppearanceSignature = signature
          if (pageLoaded) void applyAppearance()
        } else if (visible) {
          updateValues()
        }

        const resetRequested =
          (isBarStyle() && settings.barPositionX === null && settings.barPositionY === null) ||
          (!isBarStyle() && settings.panelPositionX === null && settings.panelPositionY === null)
        if (resetRequested) setDefaultWindowPosition()
      }

      if (settings.showOsd && !visible && !showRequested) {
        showOsd()
      } else if (!settings.showOsd && (visible || showRequested)) {
        hideOsd()
      }
      if (visible) startRefresh()
    })
  } else if (changed === 'hardwareSensors' || changed === 'application') {
    void readSiblingSettings().then(() => {
      if (visible) updateValues()
    })
  }
}

// ── visibility ──────────────────────────────────────────────────────────────

function showOsd(): void {
  showRequested = true
  const request = ++visibilityRequest
  // Lazy creation: the window is built on first show, not at startup.
  cancelIdleDestroy('osd')
  ensureOsdWindow()
  const win = osdWindow
  if (!win || win.isDestroyed()) return

  const apply = (): void => {
    if (request !== visibilityRequest || !showRequested || win !== osdWindow || win.isDestroyed()) return
    if (win.isVisible()) return
    setWindowPosition()
    win.show()
    visible = true
    setSurfaceVisible('osd', true)
    settings.showOsd = true
    void writeSettings()
    startRefresh()
  }

  if (!pageLoaded) {
    void applyAppearance().then((loaded) => {
      if (loaded) apply()
    })
  } else {
    apply()
  }
}

function hideOsd(persistPreference = true): void {
  showRequested = false
  visibilityRequest++
  const win = osdWindow
  if (win && !win.isDestroyed() && win.isVisible()) {
    win.hide()
  }
  visible = false
  setSurfaceVisible('osd', false)
  if (persistPreference) {
    settings.showOsd = false
    void writeSettings()
  }
  stopRefresh()
  scheduleIdleDestroy('osd', releaseOsdWindow)
}

function handleOsdChanged(data: unknown): void {
  const state = (data as OsdEventData | null)?.state
  if (state === 'Hidden') {
    hideOsd()
  } else if (state === 'Toggle') {
    if (showRequested || visible) {
      hideOsd()
    } else {
      showOsd()
    }
  } else if (state === 'Show') {
    showOsd()
  }
}

// ── public API ──────────────────────────────────────────────────────────────

export function toggleOsd(): void {
  if (showRequested || visible) {
    hideOsd(true)
  } else {
    showOsd()
  }
}

export function initOsdWindow(): void {
  // Lazy window creation: registering the subscriptions is cheap (no
  // renderer process), the BrowserWindow itself is only created when the OSD
  // actually needs to show (showOsd setting or osd.changed event). Each OSD
  // window costs a renderer process (~60-90MB), so never build it at startup.
  if (osdWindow && !osdWindow.isDestroyed()) return

  try {
    if (!globalShortcut.isRegistered('CommandOrControl+Shift+O')) {
      globalShortcut.register('CommandOrControl+Shift+O', () => {
        toggleOsd()
      })
    }
  } catch {
    // best-effort global shortcut registration
  }

  if (!unsubscribe) {
    unsubscribe = hostClient.on('osd.changed', handleOsdChanged)
  }
  if (!unsubscribeSettings) {
    unsubscribeSettings = hostClient.on('settings.changed', onSettingsChanged)
  }
  if (!unsubscribeReady) {
    unsubscribeReady = hostClient.on('host.ready', onHostReady)
  }
  if (!unsubscribeDisplay) {
    const listener = (): void => onDisplayMetricsChanged()
    screen.on('display-metrics-changed', listener)
    unsubscribeDisplay = () => screen.removeListener('display-metrics-changed', listener)
  }
  // Electron OsdWindowBase listened to SystemEvents.PowerModeChanged: hide the OSD
  // while the machine suspends so it never stays pinned over the lock screen.
  // Transient only — do not persist showOsd=false, or resume can never restore it.
  if (!unsubscribePower) {
    const onSuspend = (): void => hideOsd(false)
    const onResume = (): void => {
      if (settings.showOsd) showOsd()
    }
    powerMonitor.on('suspend', onSuspend)
    powerMonitor.on('resume', onResume)
    unsubscribePower = () => {
      powerMonitor.removeListener('suspend', onSuspend)
      powerMonitor.removeListener('resume', onResume)
    }
  }

  // If the persisted setting enables the OSD, create + show it on startup.
  void readSettingsWithRetry().then(() => {
    void readSiblingSettings().then(() => {
      if (settings.showOsd) {
        showOsd()
      }
    })
  })
}

function ensureOsdWindow(): void {
  if (osdWindow && !osdWindow.isDestroyed()) return

  osdWindow = new BrowserWindow({
    width: OSD_WIDTH,
    height: OSD_HEIGHT,
    show: false,
    frame: false,
    transparent: true,
    backgroundColor: '#00000000',
    // Always-on-top + skip-taskbar is the OSD contract on every platform:
    // Linux pins it above normal windows without an entry in the taskbar/dock
    // (some WMs also honor it as an override-redirect-style float); Windows
    // keeps it above apps and out of Alt+Tab. macOS keeps it above windows on
    // the active Space (see setVisibleOnAllWorkspaces below).
    alwaysOnTop: true,
    skipTaskbar: true,
    resizable: false,
    focusable: false,
    hasShadow: false,
    webPreferences: {
      sandbox: true,
      contextIsolation: true,
      nodeIntegration: false,
      backgroundThrottling: true
    }
  })

  // macOS limitation: macOS has no overlay/coverage API for third-party apps,
  // so the OSD cannot be drawn above a game running in true fullscreen
  // (exclusive display capture) — the game will simply cover it. macOS Spaces
  // "Full Screen" is handled by setVisibleOnAllWorkspaces below; only direct
  // display-grabbing fullscreen (e.g. games) defeats the OSD, same as the
  // Windows client's limitation on exclusive-fullscreen games.

  // macOS: Mission Control Spaces would hide the OSD when the user switches
  // desktops; pin it to every Space so it behaves like the Windows always-on-top
  // OSD. Older macOS may reject the call — the OSD still works on the active Space.
  if (process.platform === 'darwin') {
    try {
      osdWindow.setVisibleOnAllWorkspaces(true)
    } catch {
      // ignore — visible-on-current-Space fallback
    }
  }

  osdWindow.on('closed', () => {
    osdWindow = null
    pageLoaded = false
    visible = false
    showRequested = false
    visibilityRequest++
    setSurfaceVisible('osd', false)
    stopRefresh()
  })

  osdWindow.on('moved', () => {
    if (visible) snapAndClampPosition()
  })
}

export function isOsdVisible(): boolean {
  return visible && osdWindow != null && !osdWindow.isDestroyed() && osdWindow.isVisible()
}

function releaseOsdWindow(): void {
  visible = false
  showRequested = false
  visibilityRequest++
  stopRefresh()
  setSurfaceVisible('osd', false)
  if (osdWindow && !osdWindow.isDestroyed()) {
    osdWindow.destroy()
  }
  osdWindow = null
  visible = false
  pageLoaded = false
}

/** Destroy the OSD renderer but keep host subscriptions for on-demand show. */
export function suspendOsdWindow(): void {
  cancelIdleDestroy('osd')
  releaseOsdWindow()
}

export function destroyOsdWindow(): void {
  cancelIdleDestroy('osd')
  try {
    globalShortcut.unregister('CommandOrControl+Shift+O')
  } catch {
    // best-effort
  }
  if (positionSaveTimer) {
    clearTimeout(positionSaveTimer)
    positionSaveTimer = null
  }
  unsubscribe?.()
  unsubscribe = null
  unsubscribeSettings?.()
  unsubscribeSettings = null
  unsubscribeReady?.()
  unsubscribeReady = null
  unsubscribeDisplay?.()
  unsubscribeDisplay = null
  unsubscribePower?.()
  unsubscribePower = null
  releaseOsdWindow()
}
