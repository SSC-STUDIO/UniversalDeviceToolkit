import './custom.css'
import { useEffect, useId, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowSync24Regular, FluentIcon } from './icons/fluent'
import { useLoadingStore } from '../stores/loadingStore'

/**
 * Global loading overlay — port of Electron Controls/Loading/LoadingChrome.xaml
 * (ILoadingChromeOwner). Renders when the LoadingStore has an active session:
 * spinner, label/message, optional determinate progress bar and cancel action.
 */

export default function LoadingOverlay(): React.JSX.Element | null {
  const { t } = useTranslation()
  const active = useLoadingStore((s) => s.active)
  const overlayRef = useRef<HTMLDivElement>(null)
  const labelId = useId()
  const messageId = useId()
  const sessionId = active?.id

  useEffect(() => {
    if (sessionId == null) return undefined
    const overlay = overlayRef.current
    if (overlay == null) return undefined

    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const focusTarget = overlay.querySelector<HTMLElement>('button') ?? overlay
    focusTarget.focus()

    const parent = overlay.parentElement
    const siblings =
      parent == null
        ? []
        : Array.from(parent.children).filter(
            (node): node is HTMLElement => node !== overlay && node instanceof HTMLElement
          )
    for (const sibling of siblings) {
      sibling.inert = true
    }

    return () => {
      for (const sibling of siblings) {
        sibling.inert = false
      }
      if (previous != null && previous.isConnected) previous.focus()
    }
  }, [sessionId])

  if (active == null) return null

  const determinate = active.progress != null
  const progressValue = Math.min(100, Math.max(0, active.progress ?? 0))
  const hasMessage = active.message != null && active.message !== ''

  return (
    <div
      ref={overlayRef}
      className="udt-loading-overlay"
      role="dialog"
      aria-modal="true"
      aria-busy="true"
      aria-live="polite"
      aria-labelledby={labelId}
      aria-describedby={hasMessage ? messageId : undefined}
      tabIndex={-1}
    >
      <div className="udt-loading-overlay__card">
        <FluentIcon size={40} spin color="var(--udt-accent-secondary)">
          <ArrowSync24Regular />
        </FluentIcon>
        <div id={labelId} className="udt-loading-overlay__message">
          {active.label}
        </div>
        {hasMessage && (
          <div id={messageId} className="udt-loading-overlay__sub">
            {active.message}
          </div>
        )}
        {determinate && (
          <div
            className="udt-loading-overlay__progress"
            role="progressbar"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={progressValue}
          >
            <div className="udt-loading-overlay__progress-bar" style={{ width: `${progressValue}%` }} />
          </div>
        )}
        {active.canCancel && (
          <button
            type="button"
            className="udt-btn udt-btn--secondary"
            onClick={() => useLoadingStore.getState().cancel(active.id)}
          >
            {t('common.cancel', { defaultValue: 'Cancel' })}
          </button>
        )}
      </div>
    </div>
  )
}
