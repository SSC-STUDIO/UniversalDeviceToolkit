import { useState } from 'react'

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

/** Plot inset inside the 0..100 viewBox so a 0 or full-scale stroke is not clipped. */
const PLOT_TOP = 8
const PLOT_BOTTOM = 92

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

function hasDrawableLine(series: TrendSeries[]): boolean {
  return series.some(
    (item) => item.data.filter((value) => value != null && Number.isFinite(value)).length >= 2
  )
}

function seriesMax(series: TrendSeries): number {
  if (series.max != null && series.max > 0) return series.max
  const observed = series.data.filter(
    (value): value is number => value != null && Number.isFinite(value) && value >= 0
  )
  return Math.max(1, ...observed) * 1.08
}

function yOf(normalized: number): number {
  return PLOT_BOTTOM - Math.min(1, Math.max(0, normalized)) * (PLOT_BOTTOM - PLOT_TOP)
}

function pointList(values: readonly (number | null)[], max: number): { x: number; y: number }[][] {
  const count = values.length
  const segments: { x: number; y: number }[][] = []
  let current: { x: number; y: number }[] = []
  values.forEach((value, index) => {
    if (value == null || !Number.isFinite(value) || value < 0 || max <= 0) {
      if (current.length >= 2) segments.push(current)
      current = []
      return
    }
    const x = count <= 1 ? 0 : (index / (count - 1)) * 100
    current.push({ x, y: yOf(value / max) })
  })
  if (current.length >= 2) segments.push(current)
  return segments
}

function linePoints(points: { x: number; y: number }[]): string {
  return points.map((point) => `${point.x.toFixed(2)},${point.y.toFixed(2)}`).join(' ')
}

function areaPath(points: { x: number; y: number }[]): string {
  const first = points[0]
  const last = points[points.length - 1]
  if (first == null || last == null) return ''
  const commands = points.map(
    (point, index) => `${index === 0 ? 'M' : 'L'} ${point.x.toFixed(2)} ${point.y.toFixed(2)}`
  )
  return `${commands.join(' ')} L ${last.x.toFixed(2)} ${PLOT_BOTTOM} L ${first.x.toFixed(2)} ${PLOT_BOTTOM} Z`
}

const AXIS_MARKS = [
  { label: '100%', normalized: 1 },
  { label: '75%', normalized: 0.75 },
  { label: '50%', normalized: 0.5 },
  { label: '25%', normalized: 0.25 }
]

export default function TrendChart({
  series,
  labels,
  height,
  emptyLabel
}: TrendChartProps): React.JSX.Element {
  const [hoverIndex, setHoverIndex] = useState<number | null>(null)
  const drawable = hasDrawableLine(series)
  const waiting = emptyLabel != null && emptyLabel !== '' && !drawable
  const sampleCount = Math.max(0, ...series.map((item) => item.data.length))

  const moveHover = (clientX: number, bounds: DOMRect): void => {
    if (sampleCount < 2 || bounds.width <= 0) {
      setHoverIndex(null)
      return
    }
    const ratio = Math.min(1, Math.max(0, (clientX - bounds.left) / bounds.width))
    setHoverIndex(Math.round(ratio * (sampleCount - 1)))
  }

  return (
    <div className="udt-trend-chart" style={height != null ? { minHeight: height } : undefined}>
      <div className="udt-trend-chart__axis" aria-hidden="true">
        {AXIS_MARKS.map((mark) => (
          <span key={mark.label} style={{ top: `${yOf(mark.normalized)}%` }}>
            {mark.label}
          </span>
        ))}
      </div>
      <svg
        className="udt-trend-chart__canvas"
        viewBox="0 0 100 100"
        preserveAspectRatio="none"
        role="img"
        onMouseMove={(event) => moveHover(event.clientX, event.currentTarget.getBoundingClientRect())}
        onMouseLeave={() => setHoverIndex(null)}
      >
        {AXIS_MARKS.filter((mark) => mark.normalized < 1).map((mark) => (
          <line
            key={mark.label}
            x1="0"
            x2="100"
            y1={yOf(mark.normalized)}
            y2={yOf(mark.normalized)}
            className="udt-trend-chart__grid"
          />
        ))}
        <line x1="0" x2="100" y1={PLOT_BOTTOM} y2={PLOT_BOTTOM} className="udt-trend-chart__baseline" />
        {series.map((item) =>
          pointList(item.data, seriesMax(item)).map((points, index) => (
            <path
              key={`${item.name}-area-${index}`}
              d={areaPath(points)}
              fill={withAlpha(item.color, 0.22)}
              stroke="none"
            />
          ))
        )}
        {series.map((item) =>
          pointList(item.data, seriesMax(item)).map((points, index) => (
            <polyline
              key={`${item.name}-line-${index}`}
              points={linePoints(points)}
              fill="none"
              stroke={item.color}
              strokeWidth="1.6"
              strokeLinejoin="round"
              strokeLinecap="round"
            />
          ))
        )}
      </svg>
      {hoverIndex != null && labels[hoverIndex] != null && (
        <div
          className="udt-trend-chart__tooltip"
          style={{ left: `${(hoverIndex / Math.max(1, sampleCount - 1)) * 100}%` }}
        >
          <div>{labels[hoverIndex]}</div>
          {series.map((item) => {
            const value = item.data[hoverIndex]
            const text =
              value == null || !Number.isFinite(value) ? '—' : formatTooltipValue(item, value)
            return (
              <div key={item.name}>
                <i style={{ background: item.color }} />
                {item.name} {text}
              </div>
            )
          })}
        </div>
      )}
      {waiting && (
        <div className="udt-trend-chart__empty" role="status">
          {emptyLabel}
        </div>
      )}
    </div>
  )
}
