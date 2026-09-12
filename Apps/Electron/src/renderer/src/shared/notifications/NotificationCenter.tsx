import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSettingsStore } from '../settings/settingsStore'
import {
CheckmarkCircle24Filled,
Dismiss16Regular,
DismissCircle24Filled,
Info24Filled,
Warning24Filled
} from '../ui/icons/fluent'
import { useNotificationCenter, type NotificationItem, type NotificationSeverity } from './notificationCenterStore'
import { sanitizeNotificationPosition } from './notificationSettingsOptions'
import './notifications.css'

/**
 * Right-corner notification stack — port of Electron AppNotificationHost +
 * NotificationItemViewModel: severity icon/color, ×N merge badge, progress
 * bar, per-toast auto-close (hover pauses the timer), close button.
 */

const SEVERITY_COLORS: Record<NotificationSeverity, string> = {
  Success: '#2eb871',
  Info: '#3e8ae0',
  Warning: '#e6a23c',
  Error: '#e84a5f'
}

function SeverityIcon({ severity }: { severity: NotificationSeverity }): React.JSX.Element {
  switch (severity) {
    case 'Success':
      return <CheckmarkCircle24Filled />
    case 'Warning':
      return <Warning24Filled />
    case 'Error':
      return <DismissCircle24Filled />
    default:
      return <Info24Filled />
  }
}

function NotificationToast({ item }: { item: NotificationItem }): React.JSX.Element {
  const { t } = useTranslation()
  const pause = useNotificationCenter((s) => s.pause)
  const resume = useNotificationCenter((s) => s.resume)
  const dismiss = useNotificationCenter((s) => s.dismiss)
  // Close-out animation: slide out before the store removes the item.
  const [closing, setClosing] = useState(false)
  const closeTimerRef = useRef<number | null>(null)

  useEffect(
    () => () => {
      if (closeTimerRef.current !== null) window.clearTimeout(closeTimerRef.current)
    },
    []
  )

  const handleClose = (): void => {
    if (closing) return
    setClosing(true)
    closeTimerRef.current = window.setTimeout(() => dismiss(item.id), 180)
  }

  const color = SEVERITY_COLORS[item.severity]
  const hasProgress = typeof item.progressPercent === 'number'
  const percent = hasProgress ? Math.min(100, Math.max(0, item.progressPercent ?? 0)) : 0
  const title = item.mergeCount > 1 ? `${item.title} ×${item.mergeCount}` : item.title
  const classes = ['udt-notification-item']
  if (closing) classes.push('udt-notification-item--closing')
  const isError = item.severity === 'Error'

  return (
    <div
      className={classes.join(' ')}
      role={isError ? 'alert' : 'status'}
      aria-live={isError ? 'assertive' : 'polite'}
      onMouseEnter={() => pause(item.id)}
      onMouseLeave={() => resume(item.id)}
    >
      <span className="udt-notification-item__icon" style={{ color }} aria-hidden="true">
        <SeverityIcon severity={item.severity} />
      </span>
      <div className="udt-notification-item__copy">
        <div className="udt-notification-item__title" title={title}>
          {title}
        </div>
        {item.message != null && item.message.trim() !== '' && (
          <div className="udt-notification-item__message">{item.message}</div>
        )}
        {hasProgress && (
          <div
            className="udt-notification-item__progress"
            role="progressbar"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={percent}
          >
            <div className="udt-notification-item__progress-fill" style={{ width: `${percent}%` }} />
          </div>
        )}
      </div>
      <button
        type="button"
        className="udt-notification-item__close"
        aria-label={t('common.close', { defaultValue: 'Close' })}
        onClick={handleClose}
      >
        <Dismiss16Regular />
      </button>
    </div>
  )
}

/** NotificationPosition → placement CSS class. */
function positionClass(position: string): string {
  const sanitized = sanitizeNotificationPosition(position)
  switch (sanitized) {
    case 'BottomCenter':
      return 'udt-notification-center--bottom-center'
    case 'TopCenter':
      return 'udt-notification-center--top-center'
    case 'TopRight':
      return 'udt-notification-center--top-right'
    default:
      return 'udt-notification-center--bottom-right'
  }
}

export default function NotificationCenter(): React.JSX.Element {
  const { t } = useTranslation()
  const items = useNotificationCenter((s) => s.items)
  const [prefsReady, setPrefsReady] = useState(() => {
    const application = useSettingsStore.getState().scopes.application
    return typeof application === 'object' && application !== null
  })
  // Subscribed (not getState) so position changes apply immediately.
  const applicationScope = useSettingsStore((s) => s.scopes.application)
  const storedPosition =
    typeof applicationScope === 'object' && applicationScope !== null
      ? ((applicationScope as Record<string, unknown>)['NotificationPosition'] as string | undefined)
      : undefined
  const position = sanitizeNotificationPosition(storedPosition)

  useEffect(() => {
    let cancelled = false
    void useSettingsStore
      .getState()
      .load(['application'])
      .catch(() => undefined)
      .finally(() => {
        if (!cancelled) setPrefsReady(true)
      })
    return () => {
      cancelled = true
    }
  }, [])

  if (items.length === 0 || !prefsReady) return <></>

  const classes = ['udt-notification-center', positionClass(position)]

  // key={position}: remounting on placement change replays the slide-in
  // animation, so moving the notification corner is visible immediately.
  return (
    <div key={position} className={classes.join(' ')} role="region" aria-label={t('common.notifications')}>
      {items.map((item) => (
        <NotificationToast key={item.id} item={item} />
      ))}
    </div>
  )
}
