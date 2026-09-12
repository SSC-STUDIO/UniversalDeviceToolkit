/**
 * Lazy facade over the echarts runtime. Only type imports are static (erased
 * at compile time); the actual library is loaded on first use so the ~1.3 MB
 * charts chunk never blocks page navigation or the dashboard's first paint.
 */
import type { EChartsCoreOption, EChartsType } from 'echarts/core'

export type ECharts = EChartsType
export type { EChartsCoreOption }

export type EChartsRuntime = typeof import('./echartsRuntime')

let runtimePromise: Promise<EChartsRuntime> | null = null

/** Import and register the tree-shaken echarts runtime exactly once. */
export function loadECharts(): Promise<EChartsRuntime> {
  runtimePromise ??= import('./echartsRuntime')
  return runtimePromise
}
