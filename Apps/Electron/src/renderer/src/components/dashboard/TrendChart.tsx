import { useEffect, useRef, useState } from 'react'
import { loadECharts, type ECharts, type EChartsCoreOption } from '../../utils/echarts'
import { useThemeStore } from '../../stores/themeStore'

export interface TrendSeries {
  name: string
  color: string
  data: (number | null)[]
  max?: number
}

export interface TrendChartProps {
  series: TrendSeries[]
  labels: string[]
  height?: number
  /** Shown over the chart well until at least one finite sample arrives. */
  emptyLabel?: string
}

function withAlpha(hex: string, alpha: number): string {
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}

function formatTooltipValue(series: TrendSeries, value: number): string {
  if (series.max === 100) return `${Math.round(value)}%`
  if (Number.isInteger(value) || Math.abs(value) >= 100) return value.toFixed(0)
  return value.toFixed(1)
}

/** Static identity of the chart skeleton (colors, series structure, theme). */
function skeletonKey(series: TrendSeries[], isDark: boolean): string {
  return `${isDark ? 'd' : 'l'}|${series.map((s) => `${s.name}:${s.color}:${s.max ?? 'auto'}`).join(',')}`
}

function hasDrawableLine(series: TrendSeries[]): boolean {
  return series.some(
    (item) => item.data.filter((value) => value != null && Number.isFinite(value)).length >= 2
  )
}

export default function TrendChart({
  series,
  labels,
  height,
  emptyLabel
}: TrendChartProps): React.JSX.Element {
  const containerRef = useRef<HTMLDivElement>(null)
  // State (not a ref) so the option effects below re-run once the lazily
  // imported echarts runtime finishes loading and the instance exists.
  const [chart, setChart] = useState<ECharts | null>(null)
  const isDark = useThemeStore((s) => s.themeMode === 'dark')
  // Electron _smoothedAutoMax: auto-scaled series converge toward the observed max
  // instead of jumping (rise fast, fall slow).
  const smoothedMaxRef = useRef<Record<string, number>>({})
  // Cached base option: rebuilt only when theme or series structure changes.
  // Cleared on dispose so React Strict Mode remounts re-apply axes/grid.
  const baseOptionRef = useRef<{ key: string; option: EChartsCoreOption } | null>(null)
  const seriesRef = useRef(series)
  const labelsRef = useRef(labels)
  seriesRef.current = series
  labelsRef.current = labels

  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    let disposed = false
    let instance: ECharts | null = null
    let observer: ResizeObserver | null = null
    const handleResize = (): void => {
      instance?.resize()
    }
    void loadECharts().then(({ init }) => {
      if (disposed) return
      // devicePixelRatio keeps output crisp on HiDPI displays and under the
      // main-process zoom factor (SVG today, but harmless and future-proof if
      // the renderer switches to canvas).
      instance = init(el, undefined, { devicePixelRatio: window.devicePixelRatio })
      baseOptionRef.current = null
      window.addEventListener('resize', handleResize)
      observer = new ResizeObserver(handleResize)
      observer.observe(el)
      setChart(instance)
    })
    return () => {
      disposed = true
      window.removeEventListener('resize', handleResize)
      observer?.disconnect()
      instance?.dispose()
      instance = null
      baseOptionRef.current = null
      setChart(null)
    }
  }, [])

  useEffect(() => {
    if (!chart) return
    const key = skeletonKey(series, isDark)
    if (baseOptionRef.current?.key === key) return

    const gridlineColor = isDark ? 'rgba(255, 255, 255, 0.10)' : 'rgba(0, 0, 0, 0.16)'
    const labelColor = isDark ? 'rgba(255, 255, 255, 0.53)' : 'rgba(0, 0, 0, 0.55)'
    const baselineColor = isDark ? '#d2a05a' : 'rgba(210, 160, 90, 0.85)'
    const tooltipBg = isDark ? 'rgba(32, 32, 32, 0.94)' : 'rgba(255, 255, 255, 0.96)'
    const tooltipBorder = isDark ? 'rgba(255, 255, 255, 0.14)' : 'rgba(0, 0, 0, 0.12)'
    const tooltipText = isDark ? 'rgba(255, 255, 255, 0.92)' : 'rgba(0, 0, 0, 0.82)'

    // Gridlines at 75%/50%/25% only (0.5px), warm baseline at the bottom edge.
    // Drawn via markLine on the first series since ECharts cannot hide the
    // 0%/100% splitLines individually.
    const gridlines = [0.75, 0.5, 0.25].map((y) => ({
      yAxis: y,
      lineStyle: { color: gridlineColor, width: 0.5 }
    }))
    const baseline = { yAxis: 0, lineStyle: { color: baselineColor, width: 1 } }

    const normalized = series.map((s) => {
      const fixedMax = s.max != null && s.max > 0 ? s.max : null
      return { name: s.name, color: s.color, fixedMax }
    })

    const option: EChartsCoreOption = {
      animation: false,
      grid: {
        top: 4,
        left: 28,
        right: 6,
        bottom: 4,
        containLabel: false
      },
      tooltip: {
        trigger: 'axis',
        axisPointer: {
          type: 'line',
          lineStyle: { color: isDark ? 'rgba(255,255,255,0.28)' : 'rgba(0,0,0,0.28)', width: 1 }
        },
        backgroundColor: tooltipBg,
        borderColor: tooltipBorder,
        borderWidth: 1,
        padding: [8, 10],
        textStyle: { color: tooltipText, fontSize: 12 },
        extraCssText: 'box-shadow: 0 8px 20px rgba(0,0,0,0.18); border-radius: 8px;',
        formatter: (raw: unknown): string => {
          const items = Array.isArray(raw) ? raw : [raw]
          const first = items[0] as { dataIndex?: number } | undefined
          const index = typeof first?.dataIndex === 'number' ? first.dataIndex : -1
          if (index < 0) return ''
          const time = labelsRef.current[index] ?? ''
          const rows = seriesRef.current.map((item) => {
            const sample = item.data[index]
            const text =
              sample == null || !Number.isFinite(sample) ? '—' : formatTooltipValue(item, sample)
            return `<span style="display:inline-flex;align-items:center;gap:6px"><i style="display:inline-block;width:8px;height:8px;border-radius:50%;background:${item.color};flex:0 0 auto"></i>${item.name} ${text}</span>`
          })
          return [time, ...rows].filter((line) => line !== '').join('<br/>')
        }
      },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: labels,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { show: false }
      },
      yAxis: {
        type: 'value',
        min: 0,
        max: 1,
        interval: 0.25,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: {
          show: true,
          fontSize: 9,
          color: labelColor,
          margin: 4,
          formatter: (value: number): string => {
            const percent = Math.round(value * 100)
            return percent <= 0 ? '' : `${percent}%`
          }
        },
        splitLine: { show: false }
      },
      series: normalized.map((entry, index) => ({
        name: entry.name,
        type: 'line' as const,
        showSymbol: false,
        smooth: 0.5,
        lineStyle: { width: 1.35, color: entry.color, cap: 'round', join: 'round' },
        itemStyle: { color: entry.color },
        areaStyle: {
          // Electron tapers the area polygon toward the latest point (right edge).
          // ECharts cannot reshape the path, so a diagonal gradient approximates
          // the silhouette: strongest top-left, fading toward the bottom-right
          // corner where the tapered tail sits.
          color: {
            type: 'linear' as const,
            x: 0,
            y: 0,
            x2: 1,
            y2: 1,
            colorStops: [
              { offset: 0, color: withAlpha(entry.color, 0.298) },
              { offset: 0.48, color: withAlpha(entry.color, 0.165) },
              { offset: 0.86, color: withAlpha(entry.color, 0.082) },
              { offset: 1, color: withAlpha(entry.color, 0.02) }
            ]
          }
        },
        markLine:
          index === 0
            ? {
                silent: true,
                symbol: 'none',
                animation: false,
                label: { show: false },
                data: [...gridlines, baseline]
              }
            : undefined,
        data: []
      }))
    }

    baseOptionRef.current = { key, option }
    chart.setOption(option, { notMerge: true })
  }, [chart, series, isDark, labels])

  // Data-only update path: rebinds normalized data without rebuilding the
  // static option (grid, axes, area gradients, mark lines).
  useEffect(() => {
    const base = baseOptionRef.current
    if (!chart || !base) return

    const smoothed = { ...smoothedMaxRef.current }
    const dataBySeries = series.map((s) => {
      const fixedMax = s.max != null && s.max > 0 ? s.max : null
      let effectiveMax = fixedMax
      if (effectiveMax == null) {
        const observed = Math.max(
          1,
          ...s.data.filter((v): v is number => v != null && Number.isFinite(v) && v >= 0)
        )
        const target = observed * 1.08
        const previous = smoothed[s.name]
        effectiveMax = previous === undefined ? target : previous + (target - previous) * 0.35
        smoothed[s.name] = effectiveMax
      }
      return s.data.map((v) =>
        v == null || !Number.isFinite(v) ? null : Math.min(1, Math.max(0, v / effectiveMax))
      )
    })
    smoothedMaxRef.current = smoothed

    if (base.key !== skeletonKey(series, isDark)) {
      chart.setOption(base.option, { notMerge: true })
    }
    chart.setOption(
      {
        xAxis: { data: labels },
        series: dataBySeries.map((data, index) => ({ name: series[index]?.name, data }))
      },
      { lazyUpdate: true }
    )
  }, [chart, series, labels, isDark])

  const waiting = emptyLabel != null && emptyLabel !== '' && !hasDrawableLine(series)

  return (
    <div className="udt-trend-chart" style={height != null ? { minHeight: height } : undefined}>
      <div ref={containerRef} className="udt-trend-chart__canvas" />
      {waiting && (
        <div className="udt-trend-chart__empty" role="status">
          {emptyLabel}
        </div>
      )}
    </div>
  )
}
