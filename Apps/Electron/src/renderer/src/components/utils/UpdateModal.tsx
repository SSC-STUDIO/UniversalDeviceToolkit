import { useEffect, useMemo, useRef, useState } from 'react'
import { create } from 'zustand'
import { useTranslation } from 'react-i18next'
import { ArrowCircleUp24Filled, ArrowDownload24Regular } from '../icons/fluent'
import { updateApi, type DownloadProgress } from '../../api/update'
import './utils.css'

/**
 * Port of Electron UpdateWindow: shows the newest version and its release notes
 * and offers to download/install it.
 *
 * The main process downloads the Electron installer from the same public
 * application release the Host selected (stable or prerelease) and launches
 * the recorded verified path with NSIS `/S`, quitting the app.
 */

export interface UpdateModalOptions {
  version?: string | null
  releaseNotes?: string | null
  releaseDate?: string | null
}

interface UpdateRequest {
  id: number
  options: UpdateModalOptions
}

let requestSeq = 0
let pendingResolve: ((downloaded: boolean) => void) | null = null

interface UpdateState {
  request: UpdateRequest | null
  show: (options: UpdateModalOptions) => void
  settle: (downloaded: boolean) => void
}

const useUpdateStore = create<UpdateState>((set) => ({
  request: null,
  show: (options) => set({ request: { id: ++requestSeq, options } }),
  settle: (downloaded) => {
    pendingResolve?.(downloaded)
    pendingResolve = null
    set({ request: null })
  }
}))

export function openUpdateModal(options: UpdateModalOptions): Promise<boolean> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useUpdateStore.getState().show(options)
  })
}

type DownloadState = 'idle' | 'checking' | 'downloading' | 'downloaded' | 'launching' | 'failed'

function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return ''
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export default function UpdateModalHost(): React.JSX.Element {
  const { t } = useTranslation()
  const request = useUpdateStore((s) => s.request)
  const settle = useUpdateStore((s) => s.settle)
  const [checking, setChecking] = useState(false)
  const [downloadState, setDownloadState] = useState<DownloadState>('idle')
  const [progress, setProgress] = useState<DownloadProgress | null>(null)
  const [installerPath, setInstallerPath] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const unsubscribeProgressRef = useRef<(() => void) | null>(null)

  useEffect(() => {
    if (!request) return
    if (request.options.version) return
    setChecking(true)
    void updateApi
      .check(true)
      .then((result) => {
        if (result.available) {
          useUpdateStore.setState({
            request: { ...request, options: { ...request.options, version: result.version ?? null } }
          })
        } else {
          settle(false)
        }
      })
      .catch(() => settle(false))
      .finally(() => setChecking(false))
  }, [request, settle])

  useEffect(() => {
    if (!request) return
    void updateApi.getRelease().then((result) => {
      if (result.release == null) return
      useUpdateStore.setState({
        request: {
          ...request,
          options: {
            version: result.release.version,
            releaseNotes: result.release.releaseNotes ?? request.options.releaseNotes,
            releaseDate: result.release.releaseDate ?? request.options.releaseDate
          }
        }
      })
    })
  }, [request])

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
