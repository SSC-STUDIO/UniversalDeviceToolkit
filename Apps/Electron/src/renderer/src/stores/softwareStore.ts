import { create } from 'zustand'
import { softwareApi, type SoftwareDisablerApp, type SoftwareStatus } from '../api/software'

/**
 * Software disabler status shared across the UI — mirrors the Electron
 * VantageDisabler / LegionZoneDisabler / FnKeysDisabler listeners. Polled from
 * the host; surfaces the "software running" banners and keyboard backlight
 * conflict state.
 */

export interface SoftwareStatusState {
  statuses: Partial<Record<SoftwareDisablerApp, SoftwareStatus>>
  /** Whether any conflict software is currently enabled/running. */
  anyEnabled: boolean
  loading: boolean
  refresh: () => Promise<void>
  start: (intervalMs?: number) => () => void
}

export const useSoftwareStore = create<SoftwareStatusState>((set, get) => ({
  statuses: {},
  anyEnabled: false,
  loading: false,

  async refresh() {
    const apps: SoftwareDisablerApp[] = ['vantage', 'legionZone', 'fnKeys']
    const entries = await Promise.all(
      apps.map(async (app) => {
        try {
          const result = await softwareApi.getStatus(app)
          return [app, result.status] as const
        } catch {
          return [app, 'NotFound' as const]
        }
      })
    )
    const statuses = Object.fromEntries(entries) as Partial<Record<SoftwareDisablerApp, SoftwareStatus>>
    set({ statuses, anyEnabled: entries.some(([, status]) => status === 'Enabled') })
  },

  start(intervalMs = 5000) {
    void get().refresh()
    const timer = window.setInterval(() => {
      void get().refresh()
    }, intervalMs)
    return () => window.clearInterval(timer)
  }
}))
