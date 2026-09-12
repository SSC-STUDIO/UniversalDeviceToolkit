import { ArrowSync24Regular, Dismiss16Regular, Warning24Filled } from './icons/fluent'
import './AppStatusBanner.css'

/**
 * Persistent corner toast — port of Electron Controls/Shell/AppStatusBanner.xaml(.cs).
 * Hosted in the bottom-right overlay stack (MainWindow._statusNotificationStack),
 * not as a full-width top-of-page bar. Severity: Warning → warning icon;
 * Success → sync icon + clickable action (Electron ArrowSync24 / ActionArea).
 * Closed is raised only from the close button.
 */

export type AppStatusBannerSeverity = 'Warning' | 'Success'

export interface AppStatusBannerProps {
  severity?: AppStatusBannerSeverity
  message: string
  closable?: boolean
  actionLabel?: string
  onAction?: () => void
  onClosed?: () => void
  onClick?: () => void
}

export default function AppStatusBanner({
  severity = 'Warning',
  message,
  closable = true,
  actionLabel,
  onAction,
  onClosed,
  onClick
}: AppStatusBannerProps): React.JSX.Element {
  const isSuccess = severity === 'Success'
  return (
    <div
      role="status"
      aria-live="polite"
      className={`udt-status-banner${isSuccess ? ' udt-status-banner--success' : ' udt-status-banner--warning'}${onClick ? ' udt-status-banner--clickable' : ''}`}
      onClick={onClick}
    >
      <span className="udt-status-banner__icon" aria-hidden="true">
        {isSuccess ? <ArrowSync24Regular /> : <Warning24Filled />}
      </span>
      <span className="udt-status-banner__message">{message}</span>
      {actionLabel && onAction && (
        <button
          type="button"
          className="udt-status-banner__action"
          onClick={(event) => {
            event.stopPropagation()
            onAction()
          }}
        >
          {actionLabel}
        </button>
      )}
      {closable && (
        <button
          type="button"
          className="udt-status-banner__close"
          aria-label="Close"
          onClick={(event) => {
            event.stopPropagation()
            onClosed?.()
          }}
        >
          <Dismiss16Regular />
        </button>
      )}
    </div>
  )
}
