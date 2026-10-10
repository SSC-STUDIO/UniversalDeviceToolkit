import { app, BrowserWindow, screen } from 'electron'
import { hostClient } from './host-client'
import { effectiveZoom } from './ui-scale'
import { cancelIdleDestroy, scheduleIdleDestroy, setSurfaceVisible } from './ui-activity'

/**
 * Tray status popup — port of Electron Windows/Utils/StatusWindow.
 * Shows app version, machine model, power mode, battery (charge + rate),
 * discrete GPU, CPU temperature, and update availability.
 */

const WINDOW_WIDTH = 280
const AUTO_HIDE_MS = 12000
const CURSOR_OFFSET = 8

let statusWindow: BrowserWindow | null = null
let hideTimer: ReturnType<typeof setTimeout> | null = null
let model = 'Universal Device Toolkit'

interface UpdateCheckResult {
  available?: boolean
  version?: string | null
  error?: string | null
}

interface SystemInfoDto {
  vendor?: unknown
  model?: unknown
  machineType?: unknown
  biosVersion?: unknown
}

interface SensorSnapshotDto {
  battery?: {
    chargeLevel?: number | null
    chargeRate?: number | null
    isCharging?: boolean
  }
  cpu?: { temperature?: number | null }
}

interface FeatureStateDto {
  state?: unknown
}

interface DiscreteGpuDto {
  discreteGpu?: {
    supported?: boolean
    state?: string
  }
}

async function readModel(): Promise<string> {
  try {
    const info = (await hostClient.invoke('system.info', {})) as SystemInfoDto | null | undefined
    if (info && typeof info.model === 'string' && info.model.trim().length > 0) {
      return info.model
    }
  } catch {
    // Host not reachable yet — fall back to the app display name.
  }
  return 'Universal Device Toolkit'
}

async function readPowerMode(): Promise<string> {
  try {
    const result = (await hostClient.invoke('feature.getState', { feature: 'powerMode' })) as
      | FeatureStateDto
      | null
      | undefined
    if (typeof result?.state === 'string' && result.state.trim().length > 0) {
      return result.state
    }
  } catch {
    // Feature not available on this machine / platform.
  }
  return '-'
}

function formatChargeRate(rateMw: number | null | undefined, charging: boolean | undefined): string {
  if (typeof rateMw !== 'number' || !Number.isFinite(rateMw) || rateMw === 0) {
    return charging === true ? '充电' : ''
  }
  const watts = Math.abs(rateMw) / 1000
  const label = charging === true || rateMw > 0 ? '充电' : '放电'
  return `${label} ${watts.toFixed(1)}W`
}

async function readSensorLines(): Promise<{ battery: string; cpu: string }> {
  try {
    const snapshot = (await hostClient.invoke('sensors.getSnapshot', {})) as
      | SensorSnapshotDto
      | null
      | undefined
    const level = snapshot?.battery?.chargeLevel
    let battery = '-'
    if (typeof level === 'number' && Number.isFinite(level)) {
      const rate = formatChargeRate(snapshot?.battery?.chargeRate, snapshot?.battery?.isCharging)
      battery = rate.length > 0 ? `${Math.round(level)}% · ${rate}` : `${Math.round(level)}%`
    }
    const temperature = snapshot?.cpu?.temperature
    const cpu =
      typeof temperature === 'number' && Number.isFinite(temperature)
        ? `${Math.round(temperature)}°C`
        : '-'
    return { battery, cpu }
  } catch {
    return { battery: '-', cpu: '-' }
  }
}

async function readDiscreteGpu(): Promise<string> {
  try {
    const result = (await hostClient.invoke('dashboardHardware.getState', {})) as
      | DiscreteGpuDto
      | null
      | undefined
    const gpu = result?.discreteGpu
    if (gpu?.supported !== true) return '-'
    if (typeof gpu.state === 'string' && gpu.state.trim().length > 0) {
      return gpu.state
    }
  } catch {
    // Discrete GPU reporting is Windows-only.
  }
  return '-'
}

async function checkForUpdate(): Promise<UpdateCheckResult> {
  try {
    return ((await hostClient.invoke('app.update.check', { force: true })) as UpdateCheckResult | null) ?? {}
  } catch {
    return {}
  }
}

function staticPageHtml(): string {
  const css = [
    'html,body{margin:0;padding:0;background:transparent;overflow:hidden;',
    'font-family:"Segoe UI",-apple-system,"Noto Sans",system-ui,sans-serif;user-select:none;cursor:default;}',
    'body{-webkit-app-region:drag;}',
    '.status-card{width:252px;background:rgba(30,30,30,0.92);border-radius:10px;',
    'padding:12px 14px;color:#fff;font-size:12px;line-height:1.6;}',
    '.status-title{font-size:13px;font-weight:600;color:#2196F3;}',
    '.status-row{display:flex;justify-content:space-between;margin-top:4px;}',
    '.status-row .k{color:#ADFF2F;flex-shrink:0;margin-right:12px;}',
    '.status-row .v{color:#fff;text-align:right;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}',
    'button{-webkit-app-region:no-drag;margin-top:10px;width:100%;padding:5px 0;',
    'border:none;border-radius:6px;background:#2196F3;color:#fff;font-size:12px;cursor:pointer;}',
    'button:hover{background:#1976D2;}'
  ].join('')
  const html = [
    '<!DOCTYPE html><html><head><meta charset="utf-8"><style>',
    css,
    '</style></head><body><div class="status-card">',
    '<div class="status-title">Universal Device Toolkit</div>',
    '<div class="status-row"><span class="k">版本</span><span class="v" id="sv-version"></span></div>',
    '<div class="status-row"><span class="k">机型</span><span class="v" id="sv-model"></span></div>',
    '<div class="status-row"><span class="k">电源</span><span class="v" id="sv-power"></span></div>',
    '<div class="status-row"><span class="k">电池</span><span class="v" id="sv-battery"></span></div>',
    '<div class="status-row"><span class="k">独显</span><span class="v" id="sv-gpu"></span></div>',
    '<div class="status-row"><span class="k">CPU</span><span class="v" id="sv-cpu"></span></div>',
    '<div class="status-row"><span class="k">更新</span><span class="v" id="sv-update"></span></div>',
    '<button id="sv-check">检查更新</button>',
    '</div></body></html>'
  ].join('')
  return `data:text/html;charset=utf-8,${encodeURIComponent(html)}`
}

function fitToContent(win: BrowserWindow): void {
  void win.webContents
    .executeJavaScript('[document.body.scrollWidth, document.body.scrollHeight]')
    .then((size) => {
      const [width, height] = size as [number, number]
      if (!win.isDestroyed()) {
        // CSS px -> DIP via the shared zoom factor (see ui-scale.ts).
        const zoom = effectiveZoom()
        win.setSize(
          Math.max(1, Math.round(width * zoom)),
          Math.max(1, Math.round(height * zoom))
        )
      }
    })
    .catch(() => undefined)
}

function positionNearCursor(win: BrowserWindow): void {
  const cursor = screen.getCursorScreenPoint()
  const { workArea } = screen.getDisplayNearestPoint(cursor)
  const [width, height] = win.getSize()
  let x = cursor.x + CURSOR_OFFSET
  let y = cursor.y + CURSOR_OFFSET
  if (x + width > workArea.x + workArea.width) x = cursor.x - width - CURSOR_OFFSET
  if (y + height > workArea.y + workArea.height) y = cursor.y - height - CURSOR_OFFSET
  x = Math.max(workArea.x, Math.min(x, workArea.x + workArea.width - width))
  y = Math.max(workArea.y, Math.min(y, workArea.y + workArea.height - height))
  win.setPosition(Math.round(x), Math.round(y))
}

function scheduleAutoHide(): void {
  clearAutoHide()
  hideTimer = setTimeout(() => {
    hideTimer = null
    if (statusWindow && !statusWindow.isDestroyed() && statusWindow.isVisible()) {
      statusWindow.hide()
      setSurfaceVisible('status', false)
      scheduleIdleDestroy('status', destroyStatusWindow)
    }
  }, AUTO_HIDE_MS)
}

function clearAutoHide(): void {
  if (hideTimer) {
    clearTimeout(hideTimer)
    hideTimer = null
  }
}

async function refresh(): Promise<void> {
  const win = statusWindow
  if (!win || win.isDestroyed() || !win.isVisible()) return

  const version = app.getVersion()
  const setText = (id: string, text: string): void => {
    void win.webContents
      .executeJavaScript(`document.getElementById('${id}').textContent = ${JSON.stringify(text)}`)
      .catch(() => undefined)
  }
  setText('sv-version', version)
  setText('sv-model', model)
  setText('sv-power', '…')
  setText('sv-battery', '…')
  setText('sv-gpu', '…')
  setText('sv-cpu', '…')
  setText('sv-update', '检查中…')
  scheduleAutoHide()

  const [update, power, sensors, gpu] = await Promise.all([
    checkForUpdate(),
    readPowerMode(),
    readSensorLines(),
    readDiscreteGpu()
  ])
  if (win.isDestroyed() || !win.isVisible()) return
  const text =
    update.available === true
      ? `可用 ${update.version ?? ''}`
      : update.error
        ? '不可用'
        : '已是最新版本'
  setText('sv-update', text)
  setText('sv-power', power)
  setText('sv-battery', sensors.battery)
  setText('sv-gpu', gpu)
  setText('sv-cpu', sensors.cpu)
  scheduleAutoHide()
}

function ensureWindow(): void {
  if (statusWindow && !statusWindow.isDestroyed()) return

  statusWindow = new BrowserWindow({
    width: WINDOW_WIDTH,
    height: 150,
    show: false,
    frame: false,
    transparent: true,
    backgroundColor: '#00000000',
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

  statusWindow.on('closed', () => {
    statusWindow = null
    clearAutoHide()
  })

  // The page has no preload/IPC; a click on the re-check button flips the
  // document title and main observes the change to trigger a refresh.
  statusWindow.on('page-title-updated', (_event, title) => {
    if (title === 'sv-check-update') void refresh()
  })

  void statusWindow.loadURL(staticPageHtml()).then(() => {
    const win = statusWindow
    if (!win || win.isDestroyed()) return
    fitToContent(win)
    void win.webContents
      .executeJavaScript(
        "document.getElementById('sv-check').addEventListener('click', () => { document.title = 'sv-check-update' })"
      )
      .catch(() => undefined)
  })
}

/** Status window is created on first show, not at startup. */
export function initStatusWindow(): void {
  // Intentionally empty: creating a hidden BrowserWindow here would pin a
  // Chromium renderer (~60-90MB) for the life of the process.
}

/** Show the popup near the cursor, refreshing version/model/update. */
export async function showStatusWindow(): Promise<void> {
  cancelIdleDestroy('status')
  ensureWindow()
  const win = statusWindow
  if (!win || win.isDestroyed()) return
  model = await readModel()
  if (win.isDestroyed()) return
  positionNearCursor(win)
  if (!win.isVisible()) {
    win.show()
  }
  setSurfaceVisible('status', true)
  void refresh()
}

export function destroyStatusWindow(): void {
  cancelIdleDestroy('status')
  clearAutoHide()
  setSurfaceVisible('status', false)
  if (statusWindow && !statusWindow.isDestroyed()) {
    statusWindow.destroy()
  }
  statusWindow = null
}
