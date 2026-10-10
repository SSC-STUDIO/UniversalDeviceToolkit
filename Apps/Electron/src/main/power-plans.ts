/**
 * Windows power plan helpers — Electron-side counterpart of the Electron
 * WindowsPowerPlanController (Win32_PowerPlan WMI). The host does not expose
 * powerPlans.getList, so the main process answers it by parsing `powercfg`.
 */
import { execFile } from 'child_process'

export interface WindowsPowerPlan {
  guid: string
  name: string
  isActive: boolean
}

const POWERCFG = 'powercfg'

function runPowerCfg(args: string[]): Promise<string> {
  return new Promise((resolve, reject) => {
    execFile(
      POWERCFG,
      args,
      { windowsHide: true, timeout: 15000, encoding: 'utf8' },
      (error, stdout) => {
        if (error) {
          reject(new Error(`powercfg ${args.join(' ')} failed: ${error.message}`))
          return
        }
        resolve(stdout)
      }
    )
  })
}

const SCHEME_RE = /GUID:\s*([0-9a-fA-F-]{36})\s*\(([^)]*)\)\s*(\*)?/g

export async function listPowerPlans(): Promise<WindowsPowerPlan[]> {
  // powercfg is a Windows-only tool; macOS/Linux have no power plans.
  if (process.platform !== 'win32') return []
  const output = await runPowerCfg(['/list'])
  const plans: WindowsPowerPlan[] = []
  for (const match of output.matchAll(SCHEME_RE)) {
    plans.push({
      guid: match[1].toUpperCase(),
      name: match[2].trim(),
      isActive: match[3] === '*'
    })
  }
  return plans
}

/** Mirrors Electron EnsureCorrectWindowsPowerSettingsAreSetAsync / activation. */
export async function setActivePowerPlan(guid: string): Promise<void> {
  if (process.platform !== 'win32') {
    throw new Error('Windows only')
  }
  if (!/^[0-9a-fA-F-]{36}$/.test(guid)) {
    throw new Error(`Invalid power plan GUID: ${guid}`)
  }
  await runPowerCfg(['/setactive', guid])
}
