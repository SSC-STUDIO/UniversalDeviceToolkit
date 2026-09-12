import { useEffect, useState } from 'react'
import { create } from 'zustand'
import { useTranslation } from 'react-i18next'
import { ErrorCircle24Filled, Link24Regular } from '../icons/fluent'
import { useUtilsDialog } from './useUtilsDialog'
import './utils.css'

/**
 * Port of Electron UnsupportedWindow: shown at startup for machines the app has not
 * been tested on. The Continue button is gated by a 5-second countdown; Exit
 * quits the whole application (app:quit bridge).
 */

export interface UnsupportedDeviceOptions {
  vendor?: string | null
  model?: string | null
  machineType?: string | null
}

interface UnsupportedDeviceRequest {
  id: number
  options: UnsupportedDeviceOptions
}

let requestSeq = 0
let pendingResolve: ((shouldContinue: boolean) => void) | null = null

interface UnsupportedDeviceState {
  request: UnsupportedDeviceRequest | null
  show: (options: UnsupportedDeviceOptions) => void
  settle: (shouldContinue: boolean) => void
}

const useUnsupportedDeviceStore = create<UnsupportedDeviceState>((set) => ({
  request: null,
  show: (options) => set({ request: { id: ++requestSeq, options } }),
  settle: (shouldContinue) => {
    pendingResolve?.(shouldContinue)
    pendingResolve = null
    set({ request: null })
  }
}))

export function openUnsupportedDevice(options: UnsupportedDeviceOptions): Promise<boolean> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useUnsupportedDeviceStore.getState().show(options)
  })
}

const COUNTDOWN_SECONDS = 5

const CONTRIBUTION_URL = 'https://github.com/SSC-STUDIO/UniversalDeviceToolkit'

export default function UnsupportedDeviceModalHost(): React.JSX.Element {
  const { t } = useTranslation()
  const request = useUnsupportedDeviceStore((s) => s.request)
  const settle = useUnsupportedDeviceStore((s) => s.settle)
  const [countdown, setCountdown] = useState(COUNTDOWN_SECONDS)
  const { dialogRef, titleId, dialogProps } = useUtilsDialog(request != null, null)

  useEffect(() => {
    if (!request) return
    setCountdown(COUNTDOWN_SECONDS)
    const timer = window.setInterval(() => {
      setCountdown((value) => {
        if (value <= 1) {
          window.clearInterval(timer)
          return 0
        }
        return value - 1
      })
    }, 1000)
    return () => window.clearInterval(timer)
  }, [request])

  if (!request) return <></>

  const { vendor, model, machineType } = request.options
  const countdownComplete = countdown === 0

  const exit = (): void => {
    settle(false)
    window.bridge?.quitApp?.()
  }

  return (
    <div className="udt-utils-backdrop">
      <div
        ref={dialogRef}
        className="udt-utils-modal"
        style={{ width: 650, maxWidth: 'min(92vw, 650px)', maxHeight: 'min(88vh, 420px)' }}
        onClick={(event) => event.stopPropagation()}
        {...dialogProps}
      >
        <div className="udt-utils-modal__title" id={titleId}>{t('wpf.unsupportedWindowtitle')}</div>
        <div className="udt-utils-modal__body">
          <div style={{ display: 'flex', gap: 16 }}>
            <ErrorCircle24Filled style={{ fontSize: 44, color: '#e05656', flexShrink: 0 }} />
            <div style={{ flex: 1 }}>
              <p style={{ fontWeight: 600, marginTop: 0, marginBottom: 12 }}>
                {t('wpf.unsupportedWindowmessage')}
              </p>
              <p className="udt-utils-text" style={{ marginTop: 0, marginBottom: 12 }}>
                {t('wpf.unsupportedWindowdisableHint')}
              </p>
              <div className="udt-utils-row" style={{ cursor: 'default' }}>
                <span className="udt-utils-row__label">{t('wpf.unsupportedWindowvendor')}</span>
                <span className="udt-utils-row__value">{vendor ?? '-'}</span>
              </div>
              <div className="udt-utils-row" style={{ cursor: 'default' }}>
                <span className="udt-utils-row__label">{t('wpf.unsupportedWindowmodel')}</span>
                <span className="udt-utils-row__value">{model ?? '-'}</span>
              </div>
              <div className="udt-utils-row" style={{ cursor: 'default', borderBottom: 'none' }}>
                <span className="udt-utils-row__label">{t('wpf.unsupportedWindowmachineType')}</span>
                <span className="udt-utils-row__value">{machineType ?? '-'}</span>
              </div>
              <p className="udt-utils-text" style={{ margin: '14px 0' }}>
                {t('wpf.unsupportedWindowdisclaimer')}
              </p>
              <button
                type="button"
                className="udt-utils-link"
                onClick={() => void window.bridge?.openExternal?.(CONTRIBUTION_URL).catch(() => undefined)}
              >
                <Link24Regular /> {t('wpf.unsupportedWindowdisclaimergitHub')}
              </button>
            </div>
          </div>
        </div>
        <div className="udt-utils-modal__actions">
          <button type="button" className="udt-utils-button udt-utils-button--primary" data-utils-initial-focus="" onClick={exit}>
            {t('wpf.exit')}
          </button>
          <button
            type="button"
            className="udt-utils-button"
            disabled={!countdownComplete}
            onClick={() => settle(true)}
          >
            {countdownComplete ? t('wpf.continue') : `${t('wpf.continue')} (${countdown})`}
          </button>
        </div>
      </div>
    </div>
  )
}
