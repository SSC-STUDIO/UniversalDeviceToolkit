import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import AppStatusBanner from './AppStatusBanner'
import { updateApi } from '../api/update'
import { openUpdateModal } from './utils/UpdateModal'
import type { SoftwareDisablerApp } from '../api/software'
import { useSoftwareStore } from '../stores/softwareStore'
import { useStatusBannerStore } from '../stores/statusBannerStore'
import { useSettingsStore } from '../stores/settingsStore'
import { subscribeUiVisibility } from '../utils/uiVisibility'
import { sanitizeNotificationPosition } from './settings/notificationSettingsOptions'

const BANNER_UPDATE = 'updateAvailable'

function bannerPositionClass(position: string): string {
  const sanitized = sanitizeNotificationPosition(position)
  switch (sanitized) {
    case 'BottomCenter':
      return 'udt-status-banner-stack--bottom-center'
    case 'TopCenter':
      return 'udt-status-banner-stack--top-center'
    case 'TopRight':
      return 'udt-status-banner-stack--top-right'
    default:
      return 'udt-status-banner-stack--bottom-right'
  }
}

const SOFTWARE_BANNERS: { app: SoftwareDisablerApp; id: string; messageKey: string }[] = [
  { app: 'vantage', id: 'vantageRunning', messageKey: 'statusBanner.vantageRunning' },
  { app: 'legionZone', id: 'legionZoneRunning', messageKey: 'statusBanner.legionZoneRunning' },
  { app: 'fnKeys', id: 'fnKeysRunning', messageKey: 'statusBanner.fnKeysRunning' }
]

/**
 * Host for persistent corner toasts — port of Electron MainWindow._statusNotificationStack
 * (AppStatusBanner instances): update available and the three software-conflict
 * warnings (Vantage / Legion Zone / Lenovo Hotkeys).
 * Renders as a bottom-right overlay so page content is not pushed down.
 */
export default function AppStatusBanners(): React.JSX.Element {
  const { t } = useTranslation()
  const banners = useStatusBannerStore((s) => s.banners)
  const show = useStatusBannerStore((s) => s.show)
  const hide = useStatusBannerStore((s) => s.hide)
  const remove = useStatusBannerStore((s) => s.remove)
  const softwareStatuses = useSoftwareStore((s) => s.statuses)
  const softwareStart = useSoftwareStore((s) => s.start)
  const applicationScope = useSettingsStore((s) => s.scopes.application)
  const storedPosition =
    typeof applicationScope === 'object' && applicationScope !== null
      ? ((applicationScope as Record<string, unknown>)['NotificationPosition'] as string | undefined)
      : undefined
  const position = sanitizeNotificationPosition(storedPosition)
  const stackRef = useRef<HTMLDivElement>(null)

  // Keep transient AppNotificationHost toasts stacked above these persistent
  // cards when both share the bottom-right corner (Electron single StackPanel).
  // Measure the live stack so wrapped/high-scale banners do not overlap toasts.
  useEffect(() => {
    const node = stackRef.current
    if (node === null) {
      document.documentElement.style.setProperty('--udt-status-banner-stack-height', '0px')
      return () => {
        document.documentElement.style.removeProperty('--udt-status-banner-stack-height')
      }
    }
    const update = (): void => {
      const height = node.offsetHeight > 0 ? `${node.offsetHeight + 8}px` : '0px'
      document.documentElement.style.setProperty('--udt-status-banner-stack-height', height)
    }
    update()
    const observer = new ResizeObserver(update)
    observer.observe(node)
    return () => {
      observer.disconnect()
      document.documentElement.style.removeProperty('--udt-status-banner-stack-height')
    }
  }, [banners.length])

  useEffect(() => {
    let cancelled = false
    void updateApi
      .check(false)
      .then((result) => {
        if (cancelled || !result?.available) return
        const version = typeof result.version === 'string' && result.version ? result.version : null
        const openUpdater = (): void => {
          void openUpdateModal({ version })
        }
        show({
          id: BANNER_UPDATE,
          severity: 'Success',
          message: version
            ? t('statusBanner.updateAvailableWithVersion', { version })
            : t('statusBanner.updateAvailable'),
          persistent: true,
          closable: true,
          actionLabel: t('wpf.update'),
          onAction: openUpdater,
          onClick: openUpdater
        })
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [show, t])

  // Electron MainWindow software disabler indicators: banners follow the
  // VantageDisabler / LegionZoneDisabler / FnKeysDisabler status (polled).
  useEffect(() => {
    let stopPoll: (() => void) | null = null
    const startPoll = (): void => {
      if (stopPoll) return
      stopPoll = softwareStart(5000)
    }
    const haltPoll = (): void => {
      stopPoll?.()
      stopPoll = null
    }
    if (!document.hidden) startPoll()
    const unsubscribeVisibility = subscribeUiVisibility((active) => {
      if (active) startPoll()
      else haltPoll()
    })
    return () => {
      unsubscribeVisibility()
      haltPoll()
    }
  }, [softwareStart])

  useEffect(() => {
    for (const banner of SOFTWARE_BANNERS) {
      if (softwareStatuses[banner.app] === 'Enabled') {
        show({
          id: banner.id,
          severity: 'Warning',
          message: t(banner.messageKey),
          persistent: true,
          // Electron AppStatusBanner always exposes a close button; session dismiss
          // matches Closed → Collapsed until the user leaves / status changes.
          closable: true
        })
      } else {
        remove(banner.id)
      }
    }
  }, [softwareStatuses, show, remove, t])

  if (banners.length === 0) return <></>
  return (
    <div key={position} ref={stackRef} className={`udt-status-banner-stack ${bannerPositionClass(position)}`}>
      {banners.map((banner) => (
        <AppStatusBanner
          key={banner.id}
          severity={banner.severity}
          message={banner.message}
          closable={banner.closable}
          actionLabel={banner.actionLabel}
          onAction={banner.onAction}
          onClick={banner.onClick}
          onClosed={() => hide(banner.id)}
        />
      ))}
    </div>
  )
}
