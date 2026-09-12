import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation } from 'react-router-dom'
import { invoke, on } from '../api/bridge'
import type { SystemInfo } from '../api/system'
import DeviceInfoModal from './DeviceInfoModal'
import './TitleBar.css'

const DRAG_STYLE = { WebkitAppRegion: 'drag' } as React.CSSProperties
const NO_DRAG_STYLE = { WebkitAppRegion: 'no-drag' } as React.CSSProperties
const APP_DISPLAY_NAME = 'Universal Device Toolkit'

const PAGE_LABELS: Record<string, string> = {
  '/dashboard': 'nav.dashboard',
  '/keyboard': 'nav.keyboard',
  '/automation': 'nav.automation',
  '/macro': 'nav.macro',
  '/optimization': 'nav.windowsOptimization',
  '/settings': 'nav.settings',
  '/about': 'nav.about'
}

export default function TitleBar(): React.JSX.Element {
  const { t } = useTranslation()
  const location = useLocation()
  const [isMaximized, setIsMaximized] = useState(false)
  const [deviceModel, setDeviceModel] = useState<string | null>(null)
  const [deviceInfoOpen, setDeviceInfoOpen] = useState(false)

  // macOS uses the native traffic lights (red/yellow/green) instead of the
  // custom right-aligned caption buttons; leave room for them on the left.
  const isMac = window.bridge?.platform === 'darwin'

  useEffect(() => {
    let disposed = false
    void window.bridge?.isMaximized().then((value) => {
      if (!disposed) setIsMaximized(value)
    })
    const offChanged = window.bridge?.onMaximizedChanged((value) => {
      if (!disposed) setIsMaximized(value)
    })
    return () => {
      disposed = true
      offChanged?.()
    }
  }, [])

  useEffect(() => {
    let disposed = false

    const loadDeviceModel = async (): Promise<void> => {
      try {
        const systemInfo = await invoke<SystemInfo>('system.info')
        const model = systemInfo.model?.trim()
        if (!disposed && model) setDeviceModel(model)
      } catch (error) {
        // Fall back to the i18n titlebar.deviceName; the initial request can
        // race the host startup, so host.ready retries it.
        if (!disposed) console.warn('[titlebar] failed to load device model:', error)
      }
    }

    void loadDeviceModel()
    const offHostReady = on('host.ready', () => {
      void loadDeviceModel()
    })

    return () => {
      disposed = true
      offHostReady()
    }
  }, [])

  const toggleMaximize = (): void => {
    window.bridge?.maximizeToggle()
  }

  const openLogFolder = (): void => {
    void invoke<{ ok: boolean }>('log.open-folder').catch((error) => {
      console.warn('[titlebar] failed to open log folder:', error)
    })
  }

  const pageKey = PAGE_LABELS[location.pathname] ?? 'nav.dashboard'
  const windowTitle = `${APP_DISPLAY_NAME} - ${t(pageKey)}`

  useEffect(() => {
    document.title = windowTitle
  }, [windowTitle])

  return (
    <>
      <div className={`udt-titlebar udt-titlebar--original${isMac ? ' udt-titlebar--mac' : ''}`}>
        <span className="udt-titlebar__title" style={DRAG_STYLE} onDoubleClick={toggleMaximize}>
          <span className="udt-titlebar__title-text" title={windowTitle}>{windowTitle}</span>
        </span>
        {/* Right chrome: Log + device stay immediately before caption buttons (Electron parity). */}
        <div className="udt-titlebar__chrome" style={NO_DRAG_STYLE}>
          {/* Native title tooltips: antd Tooltip here would pull the rc-trigger
              stack into the eager titlebar bundle for two hover hints. */}
          <div className="udt-titlebar__trailing">
            <button
              type="button"
              className="udt-titlebar__log-button"
              title={t('titlebar.openLogs')}
              aria-label={t('titlebar.openLogs')}
              onClick={openLogFolder}
            >
              {t('titlebar.log')}
            </button>
            <div className="udt-titlebar__device-slot">
              <button
                type="button"
                className="udt-titlebar__device-button"
                title={t('titlebar.deviceInfo')}
                aria-label={t('titlebar.deviceInfo')}
                onClick={() => setDeviceInfoOpen(true)}
              >
                {deviceModel ?? t('titlebar.deviceName')}
              </button>
            </div>
          </div>
          {!isMac && (
          <div className="udt-titlebar__window-controls">
            <button
              type="button"
              aria-label={t('common.minimize')}
              onClick={() => window.bridge?.minimize()}
            >
              <svg width="11" height="11" viewBox="0 0 11 11" aria-hidden="true">
                <rect x="1" y="5" width="9" height="1" fill="currentColor" />
              </svg>
            </button>
            <button
              type="button"
              aria-label={isMaximized ? t('common.restore') : t('common.maximize')}
              onClick={toggleMaximize}
            >
              {isMaximized ? (
                <svg width="11" height="11" viewBox="0 0 11 11" aria-hidden="true">
                  <rect x="1" y="3" width="7" height="7" fill="none" stroke="currentColor" />
                  <path d="M 3 3 L 3 1 L 10 1 L 10 8 L 8 8" fill="none" stroke="currentColor" />
                </svg>
              ) : (
                <svg width="11" height="11" viewBox="0 0 11 11" aria-hidden="true">
                  <rect x="1" y="1" width="9" height="9" fill="none" stroke="currentColor" />
                </svg>
              )}
            </button>
            <button
              type="button"
              aria-label={t('common.windowClose')}
              className="udt-titlebar__close"
              onClick={() => window.bridge?.closeWindow()}
            >
              <svg width="11" height="11" viewBox="0 0 11 11" aria-hidden="true">
                <path d="M 1 1 L 10 10 M 10 1 L 1 10" stroke="currentColor" />
              </svg>
            </button>
          </div>
          )}
        </div>
      </div>
      <DeviceInfoModal
        key={deviceInfoOpen ? 'device-info-open' : 'device-info-closed'}
        open={deviceInfoOpen}
        onClose={() => setDeviceInfoOpen(false)}
      />
    </>
  )
}
