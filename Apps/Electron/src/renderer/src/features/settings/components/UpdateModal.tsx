import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { updateApi, type DownloadProgress } from '../../../shared/bridge/update'
import '../../../shared/ui/dialogs/utils.css'
import { ArrowCircleUp24Filled, ArrowDownload24Regular } from '../../../shared/ui/icons/fluent'
import { useUpdateStore } from './updateDialog'

type DownloadState = 'idle' | 'checking' | 'downloading' | 'downloaded' | 'launching' | 'failed'

function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return ''
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export default function UpdateModalHost(): React.JSX.Element | null {
  const request = useUpdateStore((state) => state.request)
  return request ? <UpdateModalHostContent key={request.id} /> : null
}

function UpdateModalHostContent(): React.JSX.Element {
  const { t } = useTranslation()
  const request = useUpdateStore((s) => s.request)
  const settle = useUpdateStore((s) => s.settle)
  const [initialRequest] = useState(request)
  const [checking, setChecking] = useState(() => !request?.options.version)
  const [downloadState, setDownloadState] = useState<DownloadState>('idle')
  const [progress, setProgress] = useState<DownloadProgress | null>(null)
  const [installerPath, setInstallerPath] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const unsubscribeProgressRef = useRef<(() => void) | null>(null)

  useEffect(() => {
    if (!initialRequest) return
    let cancelled = false
    void (async () => {
      try {
        if (!initialRequest.options.version) {
          const result = await updateApi.check(true)
          if (cancelled) return
          if (!result.available) { settle(false); return }
          useUpdateStore.setState((state) => state.request?.id === initialRequest.id
            ? { request: { ...state.request, options: { ...state.request.options, version: result.version ?? null } } }
            : state)
        }
        const result = await updateApi.getRelease()
        const release = result.release
        if (cancelled || release == null) return
        useUpdateStore.setState((state) => state.request?.id === initialRequest.id
          ? { request: { ...state.request, options: {
              version: release.version,
              releaseNotes: release.releaseNotes ?? state.request.options.releaseNotes,
              releaseDate: release.releaseDate ?? state.request.options.releaseDate
            } } }
          : state)
      } catch (error) {
        if (!cancelled) setErrorMessage(error instanceof Error ? error.message : String(error))
      } finally {
        if (!cancelled) setChecking(false)
      }
    })()
    return () => { cancelled = true }
  }, [initialRequest, settle])

  useEffect(() => {
    return () => {
      unsubscribeProgressRef.current?.()
    }
  }, [])

  const notes = useMemo(() => request?.options.releaseNotes ?? null, [request])

  if (!request) return <></>

  const { version, releaseDate } = request.options

  const startDownload = async (): Promise<void> => {
    setDownloadState('downloading')
    setErrorMessage(null)
    setProgress({ percent: 0, receivedBytes: 0, totalBytes: 0, done: false })
    unsubscribeProgressRef.current?.()
    unsubscribeProgressRef.current = updateApi.onDownloadProgress((next) => {
      setProgress(next)
      if (next.error) {
        setErrorMessage(next.error)
        setDownloadState('failed')
      }
    })
    try {
      const result = await updateApi.download()
      if (result.ok && result.path) {
        setInstallerPath(result.path)
        setDownloadState('downloaded')
        setProgress({ percent: 100, receivedBytes: 0, totalBytes: 0, done: true })
      } else {
        setErrorMessage(result.error ?? 'Download failed')
        setDownloadState('failed')
      }
    } catch (error) {
      setErrorMessage((error as Error).message)
      setDownloadState('failed')
    } finally {
      unsubscribeProgressRef.current?.()
      unsubscribeProgressRef.current = null
    }
  }

  const launch = async (): Promise<void> => {
    if (!installerPath) return
    setDownloadState('launching')
    try {
      const result = await updateApi.launchInstaller(installerPath)
      if (!result.ok) {
        setErrorMessage(
          result.error ?? t('wpf.updateWindowlaunchFailed', { defaultValue: 'The installer could not be started.' })
        )
        setDownloadState('failed')
        return
      }
      settle(true)
    } catch {
      setErrorMessage(t('wpf.updateWindowlaunchFailed', { defaultValue: 'The installer could not be started.' }))
      setDownloadState('failed')
    }
  }

  const showProgress = downloadState === 'downloading' || downloadState === 'downloaded' || downloadState === 'launching'

  return (
    <div className="udt-utils-backdrop" onClick={() => settle(false)}>
      <div
        className="udt-utils-modal"
        style={{ width: 720, maxWidth: 'min(92vw, 720px)', maxHeight: 'min(88vh, 560px)' }}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="udt-utils-modal__title">{t('wpf.updateWindowtitle')}</div>
        <div className="udt-utils-modal__body">
          <div style={{ display: 'flex', gap: 16, marginBottom: 16 }}>
            <span
              style={{
                width: 48,
                height: 48,
                flexShrink: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                background: 'var(--udt-surface-chart)',
                borderRadius: 'var(--udt-radius-control)',
                fontSize: 24,
                color: '#4f9df7'
              }}
            >
              <ArrowCircleUp24Filled />
            </span>
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 600 }}>{t('wpf.updateWindowwhatsNew')}</div>
              <div className="udt-utils-text" style={{ fontSize: 12, margin: '2px 0 8px' }}>
                {t('wpf.updateWindowtitle')}
              </div>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                {version && (
                  <span className="udt-utils-chip" style={{ background: 'rgba(79, 157, 247, 0.18)', color: '#7db4f5' }}>
                    {version}
                  </span>
                )}
                {releaseDate && <span className="udt-utils-chip">{releaseDate}</span>}
              </div>
            </div>
          </div>

          <div className="udt-utils-details" style={{ maxHeight: 300, marginTop: 0 }}>
            {checking ? (
              <div className="udt-utils-text">{t('common.loading')}</div>
            ) : notes && notes.trim().length > 0 ? (
              <div className="udt-utils-mono" style={{ whiteSpace: 'pre-wrap' }}>
                {notes}
              </div>
            ) : (
              <div className="udt-utils-text">
                {t('wpf.updateWindowreleaseNotesUnavailable')}
              </div>
            )}
          </div>

          <div className="udt-utils-progress-track" style={{ visibility: showProgress ? 'visible' : 'hidden', marginTop: 12 }}>
            <div
              className="udt-utils-progress-fill"
              style={{ width: `${Math.min(100, progress?.percent ?? 0)}%` }}
            />
          </div>
          {(downloadState === 'downloading' || downloadState === 'downloaded') && (
            <div className="udt-utils-text" style={{ fontSize: 12, marginTop: 6 }}>
              {downloadState === 'downloading'
                ? `${Math.round(progress?.percent ?? 0)}%${progress?.receivedBytes ? ` · ${formatBytes(progress.receivedBytes)} / ${formatBytes(progress.totalBytes)}` : ''}`
                : t('wpf.updateWindowdownloadComplete', { defaultValue: 'Download complete.' })}
            </div>
          )}
          {errorMessage && (
            <div className="udt-utils-text" style={{ fontSize: 12, marginTop: 6, color: 'var(--udt-status-critical-text)' }}>
              {errorMessage}
            </div>
          )}
        </div>
        <div className="udt-utils-modal__actions">
          <button
            type="button"
            className="udt-utils-button"
            disabled={downloadState === 'downloading' || downloadState === 'launching'}
            onClick={() => settle(false)}
          >
            {t('wpf.cancel')}
          </button>
          {downloadState === 'idle' || downloadState === 'failed' ? (
            <button
              type="button"
              className="udt-utils-button udt-utils-button--primary"
              onClick={() => void startDownload()}
            >
              <ArrowDownload24Regular /> {t('wpf.update')}
            </button>
          ) : downloadState === 'downloaded' ? (
            <button type="button" className="udt-utils-button udt-utils-button--primary" onClick={() => void launch()}>
              <ArrowDownload24Regular /> {t('wpf.updateWindowrestartToInstall', { defaultValue: 'Install & Restart' })}
            </button>
          ) : null}
        </div>
      </div>
    </div>
  )
}
