import { create } from 'zustand'

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

export interface CrashReportRequest {
  id: number
  report: CrashReportInfo
}

let requestSeq = 0

let pendingResolve: ((deleted: boolean) => void) | null = null

export interface CrashReportState {
  request: CrashReportRequest | null
  show: (report: CrashReportInfo) => void
  settle: (deleted: boolean) => void
}

export const useCrashReportStore = create<CrashReportState>((set) => ({
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
