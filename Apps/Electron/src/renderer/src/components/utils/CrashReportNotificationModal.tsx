import { useMemo } from 'react'
import { create } from 'zustand'
import { useTranslation } from 'react-i18next'
import { Delete24Regular, FolderOpen24Regular, Warning24Regular } from '../icons/fluent'
import { useUtilsDialog } from './useUtilsDialog'
import './utils.css'

/**
 * Port of Electron CrashReportNotificationWindow: notifies the user about a crash
 * report saved locally and lets them view or delete it.
 *
 * The host does not currently expose crash-report discovery/delete IPC, so the
 * modal is driven by explicit data (CrashReportInfo). Opening the report file
 * uses the shell bridge (shell:open-path).
 */

export interface CrashReportInfo {
  /** Path of the report file (displayed in the path chip). */
  path: string
  timestamp?: string
  appVersion?: string
  /** hh:mm:ss style uptime string. */
  uptime?: string
  exceptionType?: string
  exceptionMessage?: string
  innerExceptionType?: string
  innerExceptionMessage?: string
  stackTrace?: string
}

interface CrashReportRequest {
  id: number
  report: CrashReportInfo
}

let requestSeq = 0
let pendingResolve: ((deleted: boolean) => void) | null = null

interface CrashReportState {
  request: CrashReportRequest | null
  show: (report: CrashReportInfo) => void
  settle: (deleted: boolean) => void
}

const useCrashReportStore = create<CrashReportState>((set) => ({
  request: null,
  show: (report) => set({ request: { id: ++requestSeq, report } }),
  settle: (deleted) => {
    pendingResolve?.(deleted)
    pendingResolve = null
    set({ request: null })
  }
}))

export function openCrashReportNotification(report: CrashReportInfo): Promise<boolean> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useCrashReportStore.getState().show(report)
  })
}

/** Stack trace is bounded like the Electron window (max 1200 chars). */
const MAX_STACK_CHARS = 1200

function formatDetails(report: CrashReportInfo, t: (key: string) => string): string {
  if (!report.exceptionType && !report.exceptionMessage) {
    return t('wpf.crashReportNotificationunableToLoad')
  }
  const lines: string[] = []
  if (report.timestamp) lines.push(`${t('wpf.crashReportNotificationfieldtime')}: ${report.timestamp}`)
  if (report.appVersion) lines.push(`${t('wpf.crashReportNotificationfieldversion')}: ${report.appVersion}`)
  if (report.uptime) lines.push(`${t('wpf.crashReportNotificationfielduptime')}: ${report.uptime}`)
  if (lines.length > 0) lines.push('')
  lines.push(`${t('wpf.crashReportNotificationfieldexception')}: ${report.exceptionType ?? '-'}`)
  lines.push(`${t('wpf.crashReportNotificationfieldmessage')}: ${report.exceptionMessage ?? '-'}`)
  if (report.innerExceptionType || report.innerExceptionMessage) {
    lines.push('')
    lines.push(`${t('wpf.crashReportNotificationfieldinner')}: ${report.innerExceptionType ?? '-'}`)
    lines.push(`${t('wpf.crashReportNotificationfieldinnerMessage')}: ${report.innerExceptionMessage ?? '-'}`)
  }
  if (report.stackTrace) {
    lines.push('')
    lines.push(`${t('wpf.crashReportNotificationfieldstack')}:`)
    const stack = report.stackTrace.length > MAX_STACK_CHARS
      ? `${report.stackTrace.slice(0, MAX_STACK_CHARS)}\n…`
      : report.stackTrace
    lines.push(stack)
  }
  return lines.join('\n').trimEnd()
}

export default function CrashReportNotificationModalHost(): React.JSX.Element {
  const { t } = useTranslation()
  const request = useCrashReportStore((s) => s.request)
  const settle = useCrashReportStore((s) => s.settle)
  const { dialogRef, titleId, dialogProps } = useUtilsDialog(request != null, () => settle(false))

  const details = useMemo(() => (request ? formatDetails(request.report, t) : ''), [request, t])

  if (!request) return <></>

  const report = request.report

  const openReport = (): void => {
    void window.bridge?.openPath?.(report.path).catch(() => undefined)
  }

  const deleteReport = (): void => {
    // Deleting the file itself is host-side (CrashReportHelper.DeleteCrashReport);
    // the Electron window closes immediately, so we just resolve here.
    settle(true)
  }

  return (
    <div className="udt-utils-backdrop" onClick={() => settle(false)}>
      <div
        ref={dialogRef}
        className="udt-utils-modal"
        style={{ width: 640, minHeight: 420 }}
        onClick={(event) => event.stopPropagation()}
        {...dialogProps}
      >
        <div className="udt-utils-modal__title" id={titleId}>{t('wpf.crashReportNotificationtitle')}</div>
        <div className="udt-utils-modal__body">
          <div className="udt-utils-banner">
            <Warning24Regular className="udt-utils-banner__icon" />
            <div className="udt-utils-banner__copy">
              <div className="udt-utils-banner__title">{t('wpf.crashReportNotificationmessage')}</div>
              <div className="udt-utils-banner__desc">
                {t('wpf.crashReportNotificationdescription')}
              </div>
            </div>
          </div>
          <div className="udt-utils-card" style={{ padding: 0, overflow: 'hidden' }}>
            <div
              className="udt-utils-text"
              style={{ padding: '10px 14px', borderBottom: '1px solid rgba(128,128,128,0.25)', fontSize: 12, fontWeight: 600 }}
            >
              {t('wpf.crashReportNotificationdetailsHeading')}
            </div>
            <div className="udt-utils-details" style={{ border: 'none', borderRadius: 0, maxHeight: 240 }}>
              <div className="udt-utils-mono">{details}</div>
            </div>
          </div>
          <div className="udt-utils-chip" style={{ marginBottom: 16 }}>
            <FolderOpen24Regular />
            <span>
              {t('wpf.crashReportNotificationreportPath').replace('{0}', report.path)}
            </span>
          </div>
        </div>
        <div className="udt-utils-modal__actions udt-utils-modal__actions--space-between">
          <button type="button" className="udt-utils-button" onClick={deleteReport}>
            <Delete24Regular /> {t('wpf.crashReportNotificationdeleteReport')}
          </button>
          <div style={{ display: 'flex', gap: 10 }}>
            <button type="button" className="udt-utils-button" onClick={openReport}>
              <FolderOpen24Regular /> {t('wpf.crashReportNotificationopenReport')}
            </button>
            <button type="button" className="udt-utils-button udt-utils-button--primary" onClick={() => settle(false)}>
              {t('wpf.crashReportNotificationclose')}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
