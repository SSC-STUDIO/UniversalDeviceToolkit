import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router-dom'
import { isInstallerOptionalFeatureEnabled } from '../../../../shared/installer-selection'
import { isActionsNavigationVisible } from '../../../../shared/navigation-visibility'
import { openStatusModal } from '../../features/dashboard/components/statusDialog'
import { on, sanitizeBridgeError } from '../../shared/bridge/bridge'
import type { HostCapabilityMap } from '../../shared/bridge/hostCapabilities'
import NotificationCenter from '../../shared/notifications/NotificationCenter'
import { useSettingsStore } from '../../shared/settings/settingsStore'
import { useHostCapabilitiesStore } from '../../shared/state/hostCapabilitiesStore'
import WindowBackdropController from '../../shared/theme/WindowBackdropController'
import LoadingOverlay from '../../shared/ui/LoadingOverlay'
import {
ChevronLeft16Regular,
ChevronRight16Regular,
Gauge24Filled,
Gauge24Regular,
Home24Filled,
Home24Regular,
Info24Filled,
Info24Regular,
Keyboard24Filled,
Keyboard24Regular,
PlayCircle24Regular,
Settings24Filled,
Settings24Regular
} from '../../shared/ui/icons/fluent'
import UtilsModalHost from '../dialogs/UtilsModalHost'
import UnsupportedDeviceGate from '../startup/UnsupportedDeviceGate'
import AppStatusBanners from './AppStatusBanners'
import DiagnosticModeBanner from './DiagnosticModeBanner'
import TitleBar from './TitleBar'
import './navigation.css'

const NAV_WIDTH_COLLAPSED_FALLBACK = 70
const NAV_WIDTH_COLLAPSED_CSS = '--udt-nav-width-collapsed'
const NAV_WIDTH_EXPANDED_CSS = '--udt-nav-width-expanded'
const DESIGN_WINDOW_WIDTH = 1300
const ABSOLUTE_MAX_EXPANDED = 420
const MIN_EXPANDED_WIDTH = 150
const MIN_CONTENT_WIDTH = 480
const AUTO_COLLAPSE_BELOW = 900
const COLLAPSE_DRAG_THRESHOLD = 115
const NAV_COLLAPSED_STORAGE_KEY = 'udt.navCollapsed'
const NAV_CUSTOM_WIDTH_STORAGE_KEY = 'udt.navCustomWidth'

function readCssNumber(name: string, fallback: number): number {
  const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  const value = Number.parseFloat(raw)
  return Number.isFinite(value) && value > 0 ? value : fallback
}

function getExpandedWidth(windowWidth: number): number {
  const preferred = readCssNumber(NAV_WIDTH_EXPANDED_CSS, 220)
  if (!windowWidth || windowWidth <= 0 || Number.isNaN(windowWidth)) return preferred
  const scaled = preferred * (windowWidth / DESIGN_WINDOW_WIDTH)
  const contentBudget = Math.max(preferred, windowWidth - MIN_CONTENT_WIDTH)
  const ratioCap = windowWidth * 0.28
  const upper = Math.min(ABSOLUTE_MAX_EXPANDED, Math.min(contentBudget, Math.max(preferred, ratioCap)))
  return Math.min(Math.max(scaled, preferred), upper)
}

interface NavItemDef {
  key: string
  icon: (filled: boolean) => React.ReactNode
  labelKey: string
  capability?: keyof HostCapabilityMap
}

const MAIN_ITEMS: NavItemDef[] = [
  { key: '/dashboard', icon: (filled) => filled ? <Home24Filled /> : <Home24Regular />, labelKey: 'nav.dashboard' },
  {
    key: '/keyboard',
    icon: (filled) => filled ? <Keyboard24Filled /> : <Keyboard24Regular />,
    labelKey: 'nav.keyboard',
    capability: 'keyboard'
  },
  {
    key: '/actions',
    icon: () => <PlayCircle24Regular />,
    labelKey: 'nav.actions'
  },
  {
    key: '/tools',
    icon: (filled) => filled ? <Gauge24Filled /> : <Gauge24Regular />,
    labelKey: 'nav.tools',
    capability: 'optimization'
  }
]

const FOOTER_ITEMS: NavItemDef[] = [
  { key: '/settings', icon: (filled) => filled ? <Settings24Filled /> : <Settings24Regular />, labelKey: 'nav.settings' },
  { key: '/about', icon: (filled) => filled ? <Info24Filled /> : <Info24Regular />, labelKey: 'nav.about' }
]

function isRouteActive(pathname: string, key: string): boolean {
  return pathname === key || pathname.startsWith(`${key}/`)
}

interface NavItemProps {
  item: NavItemDef
  label: string
  collapsed: boolean
  active: boolean
  onClick: () => void
}

function NavItem({ item, label, collapsed, active, onClick }: NavItemProps): React.JSX.Element {
  const className = ['udt-nav-item', collapsed && 'udt-nav-item--collapsed', active && 'udt-nav-item--active']
    .filter(Boolean)
    .join(' ')
  return (
    <button
      type="button"
      className={className}
      title={collapsed ? label : undefined}
      aria-label={label}
      aria-current={active ? 'page' : undefined}
      onClick={onClick}
    >
      <span className="udt-nav-accent" />
      <span className="udt-nav-icon" aria-hidden="true">{item.icon(active)}</span>
      <span className="udt-nav-label">{label}</span>
    </button>
  )
}

export default function AppLayout({ children }: { children: ReactNode }): React.JSX.Element {
  const { t, i18n } = useTranslation()
  const isRtl = i18n.dir() === 'rtl'
  const location = useLocation()
  const navigate = useNavigate()
  const scopes = useSettingsStore((s) => s.scopes)
  const hostCapabilities = useHostCapabilitiesStore((s) => s.capabilities)
  const refreshSettings = useSettingsStore((s) => s.refresh)
  const [collapsed, setCollapsed] = useState(() => {
    try {
      if (window.innerWidth < AUTO_COLLAPSE_BELOW) return true
      return localStorage.getItem(NAV_COLLAPSED_STORAGE_KEY) === '1'
    } catch {
      return false
    }
  })
  const [customWidth, setCustomWidth] = useState<number | null>(() => {
    try {
      const saved = localStorage.getItem(NAV_CUSTOM_WIDTH_STORAGE_KEY)
      if (saved) {
        const parsed = Number.parseFloat(saved)
        if (Number.isFinite(parsed) && parsed >= MIN_EXPANDED_WIDTH) return parsed
      }
    } catch {
      // Ignore
    }
    return null
  })
  const [isResizing, setIsResizing] = useState(false)
  const [dragWidth, setDragWidth] = useState<number | null>(null)
  const resizeCleanupRef = useRef<(() => void) | null>(null)
  const [windowWidth, setWindowWidth] = useState(() => window.innerWidth)
  // Primary modifier is Ctrl on Windows/Linux and Cmd (metaKey) on macOS.
  const isMac = window.bridge?.platform === 'darwin'

  // Load the application scope once so navigation visibility settings apply.
  useEffect(() => {
    void refreshSettings(['application'])
  }, [refreshSettings])

  // Electron MainWindow.UpdateNavigationItemsVisibilityFromSettings: dashboard and
  // settings are always visible; everything else defaults to visible unless
  // NavigationItemsVisibility opts it out.
  const navVisibility = useMemo(() => {
    const app = (scopes.application ?? {}) as Record<string, unknown>
    return ((app.NavigationItemsVisibility as Record<string, boolean> | undefined) ?? {})
  }, [scopes.application])
  const installerFeatures = window.bridge?.installerSelection?.features

  const isNavItemVisible = useCallback(
    (item: NavItemDef): boolean => {
      const pageTagMap: Record<string, string> = {
        '/dashboard': 'dashboard',
        '/settings': 'settings',
        '/keyboard': 'keyboard',
        '/actions': 'actions',
        '/tools': 'windowsOptimization',
        '/about': 'about'
      }
      const pageTag: string = pageTagMap[item.key] ?? item.key.replace('/', '')
      if (pageTag === 'dashboard' || pageTag === 'settings') return true
      if (pageTag === 'actions') {
        return isActionsNavigationVisible(installerFeatures, navVisibility, hostCapabilities?.capabilities)
      }
      if (!isInstallerOptionalFeatureEnabled(installerFeatures, pageTag)) return false
      if (item.capability != null && hostCapabilities?.capabilities[item.capability] === false) return false
      if (navVisibility[pageTag] === false) return false
      return true
    },
    [hostCapabilities, installerFeatures, navVisibility]
  )

  const visibleMainItems = useMemo(() => MAIN_ITEMS.filter(isNavItemVisible), [isNavItemVisible])
  const visibleFooterItems = useMemo(() => FOOTER_ITEMS.filter(isNavItemVisible), [isNavItemVisible])
  const visibleNavItems = useMemo(
    () => [...visibleMainItems, ...visibleFooterItems],
    [visibleMainItems, visibleFooterItems]
  )

  const baseExpandedWidth = customWidth ?? getExpandedWidth(windowWidth)
  const navWidth = dragWidth !== null
    ? dragWidth
    : collapsed
      ? readCssNumber(NAV_WIDTH_COLLAPSED_CSS, NAV_WIDTH_COLLAPSED_FALLBACK)
      : baseExpandedWidth

  const handleResize = useCallback((): void => {
    const width = window.innerWidth
    setWindowWidth(width)
    if (width < AUTO_COLLAPSE_BELOW) setCollapsed(true)
  }, [])

  useEffect(() => {
    window.addEventListener('resize', handleResize)
    return () => window.removeEventListener('resize', handleResize)
  }, [handleResize])

  // Persist custom nav width
  useEffect(() => {
    if (customWidth !== null) {
      try {
        localStorage.setItem(NAV_CUSTOM_WIDTH_STORAGE_KEY, String(Math.round(customWidth)))
      } catch {
        // Ignore
      }
    }
  }, [customWidth])

  // Persist the navigation collapse state across sessions (Electron parity:
  // NavigationStore saves NavigationPaneExpanded on exit and restores it).
  useEffect(() => {
    try {
      localStorage.setItem(NAV_COLLAPSED_STORAGE_KEY, collapsed ? '1' : '0')
    } catch {
      // localStorage unavailable — collapse state stays in-memory only
    }
  }, [collapsed])

  const handleResizerPointerDown = useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      if (event.button !== 0 || !event.isPrimary) return
      event.preventDefault()
      resizeCleanupRef.current?.()
      const startX = event.clientX
      const pointerId = event.pointerId
      const direction = getComputedStyle(document.documentElement).direction === 'rtl' ? -1 : 1
      const startWidth = collapsed
        ? readCssNumber(NAV_WIDTH_COLLAPSED_CSS, NAV_WIDTH_COLLAPSED_FALLBACK)
        : (customWidth ?? getExpandedWidth(window.innerWidth))
      const target = event.currentTarget
      try {
        target.setPointerCapture(pointerId)
      } catch (reason: unknown) {
        console.warn('Failed to capture navigation pointer', sanitizeBridgeError(reason))
      }
      setIsResizing(true)

      const onPointerMove = (moveEvent: PointerEvent): void => {
        if (moveEvent.pointerId !== pointerId) return
        const currentX = moveEvent.clientX
        const delta = (currentX - startX) * direction
        const tentativeWidth = startWidth + delta
        const maxAllowed = Math.min(
          ABSOLUTE_MAX_EXPANDED,
          Math.max(MIN_EXPANDED_WIDTH, window.innerWidth - MIN_CONTENT_WIDTH)
        )

        if (tentativeWidth < COLLAPSE_DRAG_THRESHOLD) {
          setCollapsed(true)
          setDragWidth(readCssNumber(NAV_WIDTH_COLLAPSED_CSS, NAV_WIDTH_COLLAPSED_FALLBACK))
        } else {
          setCollapsed(false)
          const clamped = Math.min(Math.max(tentativeWidth, MIN_EXPANDED_WIDTH), maxAllowed)
          setDragWidth(clamped)
          setCustomWidth(clamped)
        }
      }

      const cleanup = (): void => {
        window.removeEventListener('pointermove', onPointerMove)
        window.removeEventListener('pointerup', onPointerUp)
        window.removeEventListener('pointercancel', onPointerUp)
        resizeCleanupRef.current = null
        try {
          if (target.hasPointerCapture(pointerId)) target.releasePointerCapture(pointerId)
        } catch (reason: unknown) {
          console.warn('Failed to release navigation pointer', sanitizeBridgeError(reason))
        }
      }

      const onPointerUp = (upEvent: PointerEvent): void => {
        if (upEvent.pointerId !== pointerId) return
        cleanup()
        setIsResizing(false)
        setDragWidth(null)
      }

      resizeCleanupRef.current = cleanup
      window.addEventListener('pointermove', onPointerMove)
      window.addEventListener('pointerup', onPointerUp)
      window.addEventListener('pointercancel', onPointerUp)
    },
    [collapsed, customWidth]
  )

  useEffect(() => () => resizeCleanupRef.current?.(), [])

  const handleResizerDoubleClick = useCallback((): void => {
    setCollapsed((prev) => !prev)
  }, [])

  useEffect(() => {
    const knownHidden = [...MAIN_ITEMS, ...FOOTER_ITEMS].some(
      (item) => isRouteActive(location.pathname, item.key) && !isNavItemVisible(item)
    )
    if (knownHidden) navigate('/dashboard', { replace: true })
  }, [isNavItemVisible, location.pathname, navigate])

  // Tray navigation (Electron TrayHelper → NavigationStore.Navigate) and optional
  // status popup (legacy Electron-only; not part of the original tray menu).
  useEffect(() => {
    const offNavigate = on('tray:navigate', (data) => {
      const route = (data as { route?: string } | null)?.route
      if (typeof route === 'string' && route.length > 0) navigate(route)
    })
    const offStatus = on('tray:status', () => {
      void openStatusModal()
    })
    return () => {
      offNavigate()
      offStatus()
    }
  }, [navigate])

  // Alt+ArrowLeft/ArrowRight page switching — port of Electron
  // NavigationStoreExtensions.NavigateToPrevious/NavigateToNext: cycles through
  // currently visible main items then footer items, wrapping around at both ends.
  // Windows/Linux only: on macOS the Cmd+Option+Arrow binding in the handler
  // below covers page cycling, so this Alt-based group stays disabled there
  // (avoiding two overlapping Arrow handlers on the same platform).
  useEffect(() => {
    if (isMac) return
    const handleKeyDown = (event: KeyboardEvent): void => {
      if (!event.altKey) return
      if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return
      if (visibleNavItems.length === 0) return
      event.preventDefault()
      const currentIndex = visibleNavItems.findIndex((item) => isRouteActive(location.pathname, item.key))
      let nextIndex: number
      if (event.key === 'ArrowRight') {
        nextIndex = (currentIndex + 1 + visibleNavItems.length) % visibleNavItems.length
      } else {
        const index = currentIndex < 0 ? 0 : currentIndex - 1
        nextIndex = index < 0 ? visibleNavItems.length - 1 : index
      }
      navigate(visibleNavItems[nextIndex].key)
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isMac, location.pathname, navigate, visibleNavItems])

  // Page switching + numbered direct jump — port of Electron MainWindow key
  // bindings (NavigationStore.NavigateToNext/Previous and the numbered nav-item
  // shortcuts).
  // - Windows/Linux: Ctrl+Tab / Ctrl+Shift+Tab cycling + Ctrl+1..9 jump.
  // - macOS: Cmd+Tab is captured by the OS app switcher, so cycling uses the
  //   browser-style Cmd+Option+ArrowLeft/ArrowRight; direct jump uses Cmd+1..9.
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent): void => {
      if (isMac) {
        // Cmd+Option+ArrowLeft/ArrowRight cycles pages (same wrap semantics as
        // the Windows/Linux Alt+Arrow group above).
        if (event.metaKey && event.altKey && !event.ctrlKey && (event.key === 'ArrowRight' || event.key === 'ArrowLeft')) {
          if (visibleNavItems.length === 0) return
          event.preventDefault()
          const currentIndex = visibleNavItems.findIndex((item) => isRouteActive(location.pathname, item.key))
          let nextIndex: number
          if (event.key === 'ArrowRight') {
            nextIndex = (currentIndex + 1 + visibleNavItems.length) % visibleNavItems.length
          } else {
            const index = currentIndex < 0 ? 0 : currentIndex - 1
            nextIndex = index < 0 ? visibleNavItems.length - 1 : index
          }
          navigate(visibleNavItems[nextIndex].key)
          return
        }
        if (!event.metaKey || event.altKey || event.ctrlKey) return
        const digit = Number(event.key)
        if (Number.isInteger(digit) && digit >= 1 && digit <= visibleNavItems.length) {
          const target = visibleNavItems[digit - 1]
          if (target && !isRouteActive(location.pathname, target.key)) {
            event.preventDefault()
            navigate(target.key)
          }
        }
        return
      }
      if (!event.ctrlKey || event.altKey || event.metaKey) return
      if (event.key === 'Tab') {
        if (visibleNavItems.length === 0) return
        event.preventDefault()
        const currentIndex = visibleNavItems.findIndex((item) => isRouteActive(location.pathname, item.key))
        const nextIndex = event.shiftKey
          ? (currentIndex - 1 + visibleNavItems.length) % visibleNavItems.length
          : (currentIndex + 1) % visibleNavItems.length
        navigate(visibleNavItems[nextIndex].key)
        return
      }
      const digit = Number(event.key)
      if (Number.isInteger(digit) && digit >= 1 && digit <= visibleNavItems.length) {
        const target = visibleNavItems[digit - 1]
        if (target && !isRouteActive(location.pathname, target.key)) {
          event.preventDefault()
          navigate(target.key)
        }
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isMac, location.pathname, navigate, visibleNavItems])

  const renderItem = (item: NavItemDef): React.JSX.Element => (
    <NavItem
      key={item.key}
      item={item}
      label={t(item.labelKey)}
      collapsed={collapsed}
      active={isRouteActive(location.pathname, item.key)}
      onClick={() => navigate(item.key)}
    />
  )

  return (
    <div className="udt-app-shell">
      <WindowBackdropController />
      <UnsupportedDeviceGate />
      <UtilsModalHost />
      <LoadingOverlay />
      <NotificationCenter />
      {/* Electron MainWindow._statusNotificationStack: bottom-right overlay, not in-flow. */}
      <AppStatusBanners />
      <TitleBar />
      <div className="udt-app-shell__body">
        <nav
          aria-label={t('common.navigation')}
          className={`udt-nav udt-nav--desktop${collapsed ? ' udt-nav--collapsed' : ''}${isResizing ? ' udt-nav--resizing' : ''}`}
          style={{ width: navWidth }}
        >
          <div className="udt-nav__scroll">
            <div className="udt-nav-group">{visibleMainItems.map(renderItem)}</div>
          </div>
          <div className="udt-nav-group udt-nav-group--footer">{visibleFooterItems.map(renderItem)}</div>
          <button
            type="button"
            aria-label={collapsed ? t('common.expandNavigation') : t('common.collapseNavigation')}
            className={`udt-nav-toggle${collapsed ? ' udt-nav-toggle--collapsed' : ''}`}
            onClick={() => setCollapsed((value) => !value)}
          >
            {collapsed !== isRtl ? <ChevronRight16Regular /> : <ChevronLeft16Regular />}
          </button>
        </nav>
        <div
          role="separator"
          aria-orientation="vertical"
          aria-label={t('common.resizeNavigation', { defaultValue: 'Resize navigation' })}
          className={`udt-nav-resizer${isResizing ? ' udt-nav-resizer--active' : ''}`}
          onPointerDown={handleResizerPointerDown}
          onDoubleClick={handleResizerDoubleClick}
        />
        <div className="udt-app-shell__content">
          <DiagnosticModeBanner />
          <main className="udt-app-shell__main">
            {children}
          </main>
        </div>
      </div>
    </div>
  )
}
