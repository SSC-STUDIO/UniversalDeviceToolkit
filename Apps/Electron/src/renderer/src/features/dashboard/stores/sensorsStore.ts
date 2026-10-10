import { create } from 'zustand'
import { sensorsApi, type FpsData, type SensorSnapshot, type SensorsSettings, type SensorsStatus } from '../api/sensors'

export const TREND_POINTS = 60

export interface SensorTrendHistory {
  labels: string[]
  cpuTemperature: (number | null)[]
  cpuUsage: (number | null)[]
  cpuClock: (number | null)[]
  gpuTemperature: (number | null)[]
  gpuUsage: (number | null)[]
  gpuClock: (number | null)[]
  memoryUsage: (number | null)[]
  batteryRate: (number | null)[]
  batteryTemperature: (number | null)[]
}

function emptyTrend(): SensorTrendHistory {
  return {
    labels: [],
    cpuTemperature: [],
    cpuUsage: [],
    cpuClock: [],
    gpuTemperature: [],
    gpuUsage: [],
    gpuClock: [],
    memoryUsage: [],
    batteryRate: [],
    batteryTemperature: []
  }
}

function appendTrendValue<T>(values: readonly T[], value: T, duplicateFirst: boolean): T[] {
  const next = duplicateFirst ? [value, value] : [...values, value]
  return next.length > TREND_POINTS ? next.slice(-TREND_POINTS) : next
}

function pushTrend(history: SensorTrendHistory, snapshot: SensorSnapshot): SensorTrendHistory {
  const time = snapshot.ts ? new Date(snapshot.ts) : new Date()
  const label = time.toLocaleTimeString([], { hour12: false })
  const rateMw = snapshot.battery?.chargeRate
  const batteryRatePoint =
    rateMw != null && Number.isFinite(rateMw) && rateMw !== -1 ? Math.abs(rateMw) / 1000 : null
  const duplicateFirst = history.labels.length === 0
  // TrendChart needs two finite samples to draw a line. Gauges already show the
  // current reading after the first snapshot, so seed a second identical point
  // instead of leaving "Waiting for sensor data" until the next poll.
  return {
    labels: appendTrendValue(history.labels, label, duplicateFirst),
    cpuTemperature: appendTrendValue(history.cpuTemperature, snapshot.cpu?.temperature ?? null, duplicateFirst),
    cpuUsage: appendTrendValue(history.cpuUsage, snapshot.cpu?.usage ?? null, duplicateFirst),
    cpuClock: appendTrendValue(
      history.cpuClock,
      snapshot.cpu?.coreClockAvg ?? snapshot.cpu?.coreClockMax ?? null,
      duplicateFirst
    ),
    gpuTemperature: appendTrendValue(history.gpuTemperature, snapshot.gpu?.temperature ?? null, duplicateFirst),
    gpuUsage: appendTrendValue(history.gpuUsage, snapshot.gpu?.usage ?? null, duplicateFirst),
    gpuClock: appendTrendValue(history.gpuClock, snapshot.gpu?.coreClock ?? null, duplicateFirst),
    memoryUsage: appendTrendValue(history.memoryUsage, snapshot.memory?.usage ?? null, duplicateFirst),
    batteryRate: appendTrendValue(history.batteryRate, batteryRatePoint, duplicateFirst),
    batteryTemperature: appendTrendValue(
      history.batteryTemperature,
      snapshot.battery?.temperature ?? null,
      duplicateFirst
    )
  }
}

interface SensorsStoreState {
  status: SensorsStatus | null
  snapshot: SensorSnapshot | null
  fps: FpsData | null
  settings: SensorsSettings | null
  trend: SensorTrendHistory
  subscribed: boolean
  /** Current polling interval in seconds (drives the refresh context-menu checkmark). */
  intervalSec: number
  loading: boolean
  error: string | null
}

interface SensorsStoreActions {
  loadStatus: () => Promise<void>
  loadSnapshot: () => Promise<void>
  start: (intervalSec?: number) => Promise<void>
  stop: () => Promise<void>
  /** Restart polling with a new interval (Electron SensorsControl refresh menu parity). */
  setInterval: (intervalSec: number) => Promise<void>
  loadSettings: () => Promise<void>
  saveSettings: (partial: SensorsSettings) => Promise<void>
}

const offUpdated: (() => void)[] = []

/** Serialize start/stop so Strict Mode remounts cannot drop the host subscription. */
let lifecycleChain: Promise<void> = Promise.resolve()
let subscriberCount = 0
let activeIntervalSec = 1

function enqueueLifecycle(op: () => Promise<void>): Promise<void> {
  const next = lifecycleChain.then(op, op)
  lifecycleChain = next.then(
    () => undefined,
    () => undefined
  )
  return next
}

export const useSensorsStore = create<SensorsStoreState & SensorsStoreActions>((set, get) => ({
  status: null,
  snapshot: null,
  fps: null,
  settings: null,
  trend: emptyTrend(),
  subscribed: false,
  intervalSec: 1,
  loading: false,
  error: null,

  loadStatus: async () => {
    try {
      const status = await sensorsApi.getStatus()
      set({ status })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  loadSnapshot: async () => {
    try {
      const snapshot = await sensorsApi.getSnapshot()
      set({ snapshot, trend: pushTrend(get().trend, snapshot) })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  start: async (intervalSec = 1) => {
    activeIntervalSec = intervalSec
    await enqueueLifecycle(async () => {
      const wasSubscribed = get().subscribed
      if (wasSubscribed) {
        if (get().intervalSec === intervalSec) {
          // Already subscribed at this interval (Strict Mode double-mount or a
          // redundant restart): keep the single host producer loop instead of
          // re-subscribing, which could race the loop restart and stall data.
          return
        }
        // Interval change while polling: drop the old subscription so the new
        // interval takes effect (Electron SensorsControl refresh menu parity).
        try {
          await sensorsApi.unsubscribe()
        } catch {
          // Best-effort restart below.
        }
        while (offUpdated.length > 0) {
          offUpdated.pop()?.()
        }
        set({ subscribed: false })
      } else {
        subscriberCount += 1
      }
      try {
        await sensorsApi.subscribe(activeIntervalSec)
        offUpdated.push(
          sensorsApi.onUpdated((snapshot) => {
            // The host may publish null while LibreHardwareMonitor data is
            // recovering (e.g. right after a re-subscribe); keep the last good
            // frame instead of crashing the trend push.
            if (snapshot == null) return
            set((state) => ({ snapshot, trend: pushTrend(state.trend, snapshot) }))
          })
        )
        offUpdated.push(sensorsApi.onFpsUpdated((fps) => set({ fps })))
        set({ subscribed: true, error: null, intervalSec: activeIntervalSec })
      } catch (error) {
        if (!wasSubscribed) subscriberCount = Math.max(0, subscriberCount - 1)
        set({ error: (error as Error).message })
      }
    })
  },

  stop: async () => {
    await enqueueLifecycle(async () => {
      subscriberCount = Math.max(0, subscriberCount - 1)
      if (subscriberCount > 0 || !get().subscribed) return
      try {
        await sensorsApi.unsubscribe()
      } catch (error) {
        set({ error: (error as Error).message })
      }
      while (offUpdated.length > 0) {
        offUpdated.pop()?.()
      }
      set({ subscribed: false })
    })
  },

  /** Restart polling with a new interval (Electron SensorsControl refresh menu parity). */
  setInterval: async (intervalSec) => {
    await get().start(intervalSec)
  },

  loadSettings: async () => {
    try {
      const settings = await sensorsApi.getSettings()
      set({ settings })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  },

  saveSettings: async (partial) => {
    try {
      await sensorsApi.setSettings(partial)
      const settings = await sensorsApi.getSettings()
      set({ settings })
    } catch (error) {
      set({ error: (error as Error).message })
    }
  }
}))
