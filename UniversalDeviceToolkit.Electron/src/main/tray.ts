import { app, BrowserWindow, Tray, nativeImage, nativeTheme, screen, type Rectangle } from 'electron'
import { existsSync } from 'fs'
import { join } from 'path'
import { isInstallerOptionalFeatureEnabled } from '../shared/installer-selection'
import { readInstallerSelection } from './installer-selection'
import { trayIconSvgForSymbol, trayNavSvg } from './tray-icons'
import { localizePipelineName, setTrayLanguage, trayStrings } from './tray-i18n'
import {
  destroyTrayPopup,
  hideTrayPopup,
  isTrayPopupVisible,
  showTrayPopup,
  type TrayPopupNode
} from './tray-popup'

let tray: Tray | null = null
let getWindow: (() => BrowserWindow | null) | null = null
let restoreWindow: ((route?: string) => void) | null = null
let invokeHost: ((method: string, params?: unknown) => Promise<unknown>) | null = null
let refreshTimer: ReturnType<typeof setTimeout> | null = null
let building = false
let lastTrayBounds: Rectangle | null = null

interface AutomationPipelineDto {
  id?: string
  iconName?: string | null
  name?: string | null
  trigger?: unknown
}

interface AutomationStateDto {
  pipelines?: AutomationPipelineDto[]
}

interface NavItem {
  route: string
  label: () => string
  iconId: 'home' | 'keyboard' | 'rocket' | 'macro' | 'gauge'
  visibilityKey?: string
}

const NAV_ITEMS: NavItem[] = [
  { route: '/dashboard', label: () => trayStrings().dashboard, iconId: 'home' },
  { route: '/keyboard', label: () => trayStrings().keyboard, iconId: 'keyboard', visibilityKey: 'keyboard' },
  { route: '/automation', label: () => trayStrings().automation, iconId: 'rocket', visibilityKey: 'automation' },
  { route: '/macro', label: () => trayStrings().macro, iconId: 'macro', visibilityKey: 'macro' },
  {
    route: '/optimization',
    label: () => trayStrings().windowsOptimization,
    iconId: 'gauge',
    visibilityKey: 'windowsOptimization'
  }
]

interface TrayIconSource {
  path: string
  /** macOS template image: the system tints it to match the menu bar theme. */
  template: boolean
}

function trayIconSource(): TrayIconSource {
  // macOS: menu bar icons should be monochrome template images so the system
  // adapts them to the active menu bar theme. trayTemplate.png takes priority;
  // tray-dark.png (black glyph + alpha) is a valid template fallback.
  if (process.platform === 'darwin') {
    const templatePath = join(__dirname, '..', '..', 'resources', 'trayTemplate.png')
    if (existsSync(templatePath)) return { path: templatePath, template: true }
    const darkPath = join(__dirname, '..', '..', 'resources', 'tray-dark.png')
    if (existsSync(darkPath)) return { path: darkPath, template: true }
  }
  const dark = nativeTheme.shouldUseDarkColors
  const file = dark ? 'tray-light.png' : 'tray-dark.png'
  return { path: join(__dirname, '..', '..', 'resources', file), template: false }
}

function createTrayImage() {
  const source = trayIconSource()
  const image = nativeImage.createFromPath(source.path)
  if (source.template) image.setTemplateImage(true)
  return image
}

function updateTrayIcon(): void {
  if (!tray) return
  const image = createTrayImage()
  if (!image.isEmpty()) {
    tray.setImage(image)
  }
}

function showWindow(window: BrowserWindow | null, route?: string): void {
  if (!window || window.isDestroyed()) {
    restoreWindow?.(route)
    return
  }
  if (window.isMinimized()) window.restore()
  window.show()
  window.focus()
}

function sendToRenderer(event: string, data: unknown = null): void {
  const win = getWindow?.() ?? null
  if (!win || win.isDestroyed()) return
  win.webContents.send('bridge:event', event, data)
}

function navigate(route: string): void {
  const win = getWindow?.() ?? null
  if (!win || win.isDestroyed()) {
    restoreWindow?.(route)
    return
  }
  showWindow(win)
  // Match Electron TrayHelper delay so the window is foreground before navigation.
  setTimeout(() => sendToRenderer('tray:navigate', { route }), 500)
}

async function readNavigationVisibility(): Promise<Record<string, boolean>> {
  if (!invokeHost) return {}
  try {
    const result = (await invokeHost('settings.get', { scope: 'application' })) as
      | {
          value?: {
            navigationItemsVisibility?: Record<string, boolean>
            NavigationItemsVisibility?: Record<string, boolean>
          }
        }
      | null
      | undefined
    return (
      result?.value?.navigationItemsVisibility ??
      result?.value?.NavigationItemsVisibility ??
      {}
    )
  } catch (error) {
    console.error('[tray] failed to read navigation visibility:', error)
    return {}
  }
}

async function readQuickActions(): Promise<AutomationPipelineDto[]> {
  if (!invokeHost) return []
  try {
    const state = (await invokeHost('automation.getState', {})) as AutomationStateDto | null | undefined
    const pipelines = state?.pipelines ?? []
    // Electron TrayHelper: triggerless pipelines only, reversed so defaults appear near Open/Close.
    return pipelines.filter((p) => p.trigger == null).reverse()
  } catch (error) {
    console.error('[tray] failed to read quick actions:', error)
    return []
  }
}

async function runQuickAction(pipelineId: string | undefined): Promise<void> {
  if (!pipelineId || !invokeHost) return
  try {
    await invokeHost('automation.runNow', { pipelineId })
  } catch (error) {
    console.error('[tray] failed to run quick action:', error)
  }
}

function isNavVisible(key: string | undefined, visibility: Record<string, boolean>): boolean {
  if (!key) return true
  if (Object.prototype.hasOwnProperty.call(visibility, key)) return visibility[key] !== false
  return true
}

async function readPowerModeState(): Promise<{ current: string; states: string[] } | null> {
  if (!invokeHost) return null
  try {
    const isSupported = (await invokeHost('features.isSupported', { feature: 'powerMode' })) as { value?: boolean } | null
    if (isSupported?.value !== true) return null
    const [stateRes, allRes] = await Promise.all([
      invokeHost('features.get', { feature: 'powerMode' }) as Promise<{ value?: string } | null>,
      invokeHost('features.getAllStates', { feature: 'powerMode' }) as Promise<{ value?: string[] } | null>
    ])
    const current = stateRes?.value ?? ''
    const states = allRes?.value ?? []
    if (states.length === 0) return null
    return { current, states }
  } catch {
    return null
  }
}

async function readBatteryBadge(): Promise<string | undefined> {
  if (!invokeHost) return undefined
  try {
    const sensors = (await invokeHost('sensors.get', {})) as { battery?: { chargeLevel?: number } } | null
    const charge = sensors?.battery?.chargeLevel
    if (charge != null && Number.isFinite(charge) && charge >= 0) {
      return `${Math.round(charge)}%`
    }
  } catch {
    // ignore
  }
  return undefined
}

async function buildPopupNodes(): Promise<TrayPopupNode[]> {
  const s = trayStrings()
  const installerFeatures = readInstallerSelection()?.features
  const [visibility, quickActions, powerMode, batteryBadge] = await Promise.all([
    readNavigationVisibility(),
    readQuickActions(),
    readPowerModeState(),
    readBatteryBadge()
  ])
  const nodes: TrayPopupNode[] = []

  nodes.push({ type: 'header', label: 'Universal Device Toolkit', badge: batteryBadge })

  if (powerMode && powerMode.states.length > 0) {
    nodes.push({ type: 'separator' })
    const stateLabel = (st: string): string => {
      switch (st) {
        case 'Quiet': return s.quiet
        case 'Balance': return s.balance
        case 'Performance': return s.performance
        case 'GodMode':
        case 'Custom': return s.custom
        default: return st
      }
    }
    nodes.push({
      type: 'segment',
      label: s.powerMode,
      items: powerMode.states.map((st) => ({
        cmd: `powerMode:${st}`,
        label: stateLabel(st),
        active: st.toLowerCase() === powerMode.current.toLowerCase()
      }))
    })
  }

  nodes.push({ type: 'separator' })

  for (const nav of NAV_ITEMS) {
    if (nav.visibilityKey && !isInstallerOptionalFeatureEnabled(installerFeatures, nav.visibilityKey)) continue
    if (!isNavVisible(nav.visibilityKey, visibility)) continue
    nodes.push({
      type: 'item',
      cmd: `nav:${nav.route}`,
      label: nav.label(),
      iconSvg: trayNavSvg(nav.iconId)
    })
  }

  if (quickActions.length > 0) {
    nodes.push({ type: 'separator' })
    for (const pipeline of quickActions) {
      nodes.push({
        type: 'item',
        cmd: `run:${pipeline.id ?? ''}`,
        label: localizePipelineName(pipeline.name),
        iconSvg: trayIconSvgForSymbol(pipeline.iconName ?? 'Play24')
      })
    }
  }

  nodes.push({ type: 'separator' })
  nodes.push({ type: 'item', cmd: 'open', label: s.open })
  nodes.push({ type: 'item', cmd: 'close', label: s.close })

  return nodes
}

function handlePopupCommand(cmd: string): void {
  if (cmd === 'open') {
    showWindow(getWindow?.() ?? null)
    return
  }
  if (cmd === 'close') {
    // Mirrors Electron Resource.Close → App.ShutdownAsync(true).
    app.quit()
    return
  }
  if (cmd.startsWith('powerMode:')) {
    const targetMode = cmd.slice(10)
    void invokeHost?.('features.set', { feature: 'powerMode', value: targetMode }).then(() => {
      if (lastTrayBounds && isTrayPopupVisible()) {
        void openFlyout(lastTrayBounds)
      }
    })
    return
  }
  if (cmd.startsWith('nav:')) {
    navigate(cmd.slice(4))
    return
  }
  if (cmd.startsWith('run:')) {
    void runQuickAction(cmd.slice(4))
  }
}

function resolveTrayBounds(bounds: Rectangle): Rectangle {
  if (bounds.width > 0 && bounds.height > 0) return bounds
  const fromTray = tray?.getBounds()
  if (fromTray && fromTray.width > 0) return fromTray
  const cursor = screen.getCursorScreenPoint()
  return { x: cursor.x - 8, y: cursor.y - 8, width: 16, height: 16 }
}

async function openFlyout(bounds: Rectangle): Promise<void> {
  const anchor = resolveTrayBounds(bounds)
  lastTrayBounds = anchor
  if (building) {
    scheduleRebuild(80)
    return
  }
  building = true
  try {
    const nodes = await buildPopupNodes()
    await showTrayPopup(nodes, anchor, handlePopupCommand)
  } catch (error) {
    console.error('[tray] failed to show flyout:', error)
  } finally {
    building = false
  }
}

function scheduleRebuild(delayMs = 50): void {
  if (refreshTimer) clearTimeout(refreshTimer)
  refreshTimer = setTimeout(() => {
    refreshTimer = null
    if (!isTrayPopupVisible() || !lastTrayBounds) return
    void openFlyout(lastTrayBounds)
  }, delayMs)
}

export interface TrayOptions {
  /** Mirrors the Electron --disable-tray-tooltip flag. */
  disableTooltip?: boolean
  invokeHost?: (method: string, params?: unknown) => Promise<unknown>
  /** Recreate and show the main window when the tray needs the UI. */
  restoreWindow?: (route?: string) => void
  /** Initial UI language (renderer `udt.lang` / i18n). Defaults to zh-CN. */
  language?: string
}

export function initTray(getWin: () => BrowserWindow | null, options?: TrayOptions): void {
  if (tray) return

  getWindow = getWin
  restoreWindow = options?.restoreWindow ?? null
  invokeHost = options?.invokeHost ?? null
  if (options?.language) setTrayLanguage(options.language)

  const image = createTrayImage()
  tray = new Tray(image.isEmpty() ? nativeImage.createEmpty() : image)
  if (!options?.disableTooltip) {
    tray.setToolTip('Universal Device Toolkit')
  }

  // Custom compact flyout instead of native Menu (Win11 row height is ~44px
  // and cannot be tightened). Left-click still brings the main window forward.
  // Linux (GNOME) has no tray right-click, so left-click opens the flyout menu
  // there; Windows/macOS keep the menu on right-click.
  //
  // macOS: the menu bar icon is the app's persistent handle (macOS has no
  // system tray — the icon replaces it). mac convention would open a menu on
  // left-click, but the mapping is deliberately kept identical to Windows
  // (left-click = show window, right-click = flyout) for cross-platform
  // consistency; the template image lets the system tint it for light/dark
  // menu bar themes.
  // Linux: tray visibility depends on AppIndicator/StatusNotifier support —
  // GNOME hides tray icons unless the "AppIndicator and KStatusNotifierItem
  // Support" extension is installed, so on some desktops the tray does not
  // exist at all; the window itself remains the primary entry point there.
  tray.on('click', (_event, bounds) => {
    if (process.platform === 'linux') {
      void openFlyout(bounds)
      return
    }
    hideTrayPopup()
    showWindow(getWindow?.() ?? null)
  })
  tray.on('double-click', () => {
    hideTrayPopup()
    showWindow(getWindow?.() ?? null)
  })
  tray.on('right-click', (_event, bounds) => {
    void openFlyout(bounds)
  })

  nativeTheme.on('updated', onNativeThemeUpdated)
}

function onNativeThemeUpdated(): void {
  updateTrayIcon()
  scheduleRebuild()
}

/** Rebuild after host ready / automation save / language change. */
export function refreshTrayMenu(): void {
  scheduleRebuild(0)
}

export function updateTrayLanguage(lang: string): void {
  setTrayLanguage(lang)
  scheduleRebuild()
}

export function isTrayActive(): boolean {
  return tray != null
}

export function destroyTray(): void {
  nativeTheme.removeListener('updated', onNativeThemeUpdated)
  if (refreshTimer) {
    clearTimeout(refreshTimer)
    refreshTimer = null
  }
  destroyTrayPopup()
  if (!tray) return
  tray.destroy()
  tray = null
  getWindow = null
  restoreWindow = null
  invokeHost = null
  lastTrayBounds = null
}
