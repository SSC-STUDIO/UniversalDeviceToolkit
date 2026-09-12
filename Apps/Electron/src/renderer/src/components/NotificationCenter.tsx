import '../notifications/notifications.css'
import { useEffect, useRef, useState } from 'react'
import {
  CheckmarkCircle24Filled,
  Dismiss16Regular,
  DismissCircle24Filled,
  Info24Filled,
  Warning24Filled
} from './icons/fluent'
import { useTranslation } from 'react-i18next'
import { useSettingsStore } from '../stores/settingsStore'
import { useNotificationCenter, type NotificationItem, type NotificationSeverity } from '../notifications/notificationCenterStore'
import { sanitizeNotificationPosition } from './settings/notificationSettingsOptions'

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

/** Electron SystemSounds.Asterisk equivalent — short two-tone beep. */
function playNotificationSound(): void {
  try {
    const AudioContextClass = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (AudioContextClass === undefined) return
    const context = new AudioContextClass()
    const now = context.currentTime
    const playTone = (frequency: number, start: number, duration: number): void => {
      const oscillator = context.createOscillator()
      const gain = context.createGain()
      oscillator.type = 'sine'
      oscillator.frequency.value = frequency
      gain.gain.setValueAtTime(0.08, start)
      gain.gain.exponentialRampToValueAtTime(0.001, start + duration)
      oscillator.connect(gain)
      gain.connect(context.destination)
      oscillator.start(start)
      oscillator.stop(start + duration)
    }
    playTone(880, now, 0.12)
    playTone(1320, now + 0.1, 0.16)
    window.setTimeout(() => {
      void context.close()
    }, 400)
  } catch {
    // Best-effort sound playback.
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

function readApplicationScope(): Record<string, unknown> {
  const scopes = useSettingsStore.getState().scopes
  return typeof scopes.application === 'object' && scopes.application !== null
    ? (scopes.application as Record<string, unknown>)
    : {}
}

/** Suppression + sound settings (Electron AppNotificationHost.ShouldSuppress/TryPlaySound). */
export function readNotificationPreferences(): {
  suppressed: boolean
  suppressSuccess: boolean
  playSound: boolean
  duration: 'Short' | 'Normal' | 'Long'
  position: string
} {
  const app = readApplicationScope()
  const notifications =
    typeof app['Notifications'] === 'object' && app['Notifications'] !== null
      ? (app['Notifications'] as Record<string, unknown>)
      : {}
  const duration = app['NotificationDuration']
  const position = app['NotificationPosition']
  return {
    suppressed: app['DontShowNotifications'] === true,
    suppressSuccess: notifications['SuccessNotifications'] === false,
    playSound: notifications['NotificationSound'] === true,
    duration:
      duration === 'Short' || duration === 'Long' ? duration : ('Normal' as const),
    position: typeof position === 'string' ? position : 'BottomRight'
  }
}

export function maybePlayNotificationSound(): void {
  if (readNotificationPreferences().playSound) playNotificationSound()
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
