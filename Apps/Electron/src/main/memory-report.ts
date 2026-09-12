/**
 * Real production memory footprint: Electron is multi-process by design
 * (main + GPU + renderer + network + utility). `app.getAppMetrics()` reports
 * every child process; summing WorkingSetMB gives the true total - the number
 * the OS task manager shows for the whole app. Logged once after startup and
 * exposed over the app:memory-usage IPC for on-demand query.
 */
import { app } from 'electron'

export interface MemoryReport {
  processes: Array<{ name: string; type: string; workingSetMB: number }>
  totalMB: number
}

export async function reportMemoryUsage(): Promise<MemoryReport> {
  const metrics = app.getAppMetrics()
  const processes: Array<{ name: string; type: string; workingSetMB: number }> = []
  for (const metric of metrics) {
    // workingSetSize is documented as "memory pinned to physical RAM"; the
    // effective unit varies across Electron versions (bytes vs KB). Resolve
    // it empirically: if the raw value is below 1MB it is KB, not bytes.
    let workingSetMB = Math.round((metric.memory.workingSetSize ?? 0) / 1024 / 1024)
    const raw = metric.memory.workingSetSize ?? 0
    if (raw > 0 && raw < 1024 * 1024) {
      // Raw value is KB -> convert directly to MB.
      workingSetMB = Math.round(raw / 1024)
    }
    processes.push({ name: metric.name ?? metric.type, type: metric.type, workingSetMB })
  }
  const totalMB = processes.reduce((sum, process) => sum + process.workingSetMB, 0)
  return { processes, totalMB }
}

export async function logMemoryUsage(tag: string): Promise<void> {
  const report = await reportMemoryUsage()
  console.log(`[main] memory ${tag}: ${report.totalMB} MB total`)
  for (const process of report.processes) {
    console.log(`  ${process.type.padEnd(12)} ${String(process.workingSetMB).padStart(4)} MB`)
  }
}
