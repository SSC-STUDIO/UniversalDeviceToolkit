import './fanCurve.css'
import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

// Port of WPF Controls/FanCurveControl: 10 vertical sliders in equal columns;
// polyline and thumbs share one coordinate space (thumb center = graph point).

export type FanCurveSensorType = 'CPU' | 'CPUSensor' | 'GPU' | 'GPU2'

export interface FanCurveSensorData {
  type: FanCurveSensorType
  fanSpeeds: number[]
  temps: number[]
}

export interface FanCurveEditorProps {
  value: number[]
  minimum?: number[]
  sensors?: FanCurveSensorData[]
  disabled?: boolean
  height?: number
  onChange?: (value: number[]) => void
  onChangeCommit?: (value: number[]) => void
}

const STEP_MIN = 0
const STEP_MAX = 10
const THUMB_SIZE = 18
const THUMB_RADIUS = THUMB_SIZE / 2
const PLOT_PADDING_X = 14
const PLOT_PADDING_TOP = 12
const GRID_LINES = 5
const LINE_COLOR = '#4f9df7'
const CELSIUS = '\u00B0C'
const INVALID_TEMP = 127

const SENSOR_LABEL_KEYS: Record<FanCurveSensorType, string> = {
  CPU: 'cpu',
  CPUSensor: 'cpuSensor',
  GPU: 'gpu',
  GPU2: 'gpu2'
}

const SENSOR_ORDER: FanCurveSensorType[] = ['CPU', 'CPUSensor', 'GPU', 'GPU2']

function resolveAxisTemps(sensors: FanCurveSensorData[], count: number): (number | undefined)[] {
  const source = sensors.find((sensor) => sensor.temps.length >= count)?.temps
  if (source == null) return Array.from({ length: count }, () => undefined)
  return Array.from({ length: count }, (_, index) => {
    const temp = source[index]
    return temp == null || temp >= INVALID_TEMP ? undefined : temp
  })
}

function pointX(index: number, count: number, width: number): number {
  if (count <= 0 || width <= 0) return 0
  return ((index + 0.5) / count) * width
}

export default function FanCurveEditor({
  value,
  minimum,
  sensors = [],
  disabled = false,
  height = 230,
  onChange,
  onChangeCommit
}: FanCurveEditorProps): React.JSX.Element {
  const { t } = useTranslation()
  const gradientId = useId()
  const plotRef = useRef<HTMLDivElement>(null)
  const [plotSize, setPlotSize] = useState({ width: 0, height: 0 })
  const [dragIndex, setDragIndex] = useState<number | null>(null)
  const [hoverIndex, setHoverIndex] = useState<number | null>(null)

  const values = value.length >= 2 ? value.slice(0, 10) : []
  const count = values.length

  useEffect(() => {
    const el = plotRef.current
    if (!el) return
    const update = (): void => {
      setPlotSize({
        width: Math.max(0, el.clientWidth),
        height: Math.max(0, el.clientHeight)
      })
    }
    update()
    const observer = new ResizeObserver(update)
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  const rangeTop = THUMB_RADIUS
  const rangeBottom = Math.max(rangeTop + 1, plotSize.height - THUMB_RADIUS)

  const valueY = useCallback(
    (v: number): number => {
      if (rangeBottom <= rangeTop) return rangeBottom
      const ratio = Math.min(1, Math.max(0, (v - STEP_MIN) / (STEP_MAX - STEP_MIN)))
      return rangeBottom - ratio * (rangeBottom - rangeTop)
    },
    [rangeTop, rangeBottom]
  )

  const applyValue = useCallback(
    (index: number, raw: number): number[] => {
      let v = Math.min(STEP_MAX, Math.max(STEP_MIN, Math.round(raw)))
      if (minimum && minimum[index] != null) v = Math.max(v, minimum[index])
      const next = [...values]
      next[index] = v
      for (let i = 0; i < index; i++) if (next[i] > v) next[i] = v
      for (let i = index + 1; i < next.length; i++) if (next[i] < v) next[i] = v
      return next
    },
    [values, minimum]
  )

  const valueFromPointer = (clientY: number): number => {
    const el = plotRef.current
    if (!el) return 0
    const rect = el.getBoundingClientRect()
    const y = clientY - rect.top
    const span = rangeBottom - rangeTop
    const ratio = span > 0 ? 1 - (y - rangeTop) / span : 0
    return ratio * STEP_MAX
  }

  const handlePointerDown = (index: number) => (e: React.PointerEvent<HTMLDivElement>): void => {
    if (disabled) return
    e.preventDefault()
    e.currentTarget.setPointerCapture(e.pointerId)
    setDragIndex(index)
    onChange?.(applyValue(index, valueFromPointer(e.clientY)))
  }

  const handlePointerMove = (e: React.PointerEvent<HTMLDivElement>): void => {
    if (dragIndex == null) return
    onChange?.(applyValue(dragIndex, valueFromPointer(e.clientY)))
  }

  const handlePointerUp = (e: React.PointerEvent<HTMLDivElement>): void => {
    if (dragIndex == null) return
    const next = applyValue(dragIndex, valueFromPointer(e.clientY))
    onChange?.(next)
    onChangeCommit?.(next)
    setDragIndex(null)
  }

  const points = useMemo(
    () =>
      values.map((v, i) => ({
        x: pointX(i, count, plotSize.width),
        y: valueY(v)
      })),
    [values, count, plotSize.width, valueY]
  )

  const linePath = points
    .map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(2)},${p.y.toFixed(2)}`)
    .join(' ')

  const areaPath =
    points.length > 0
      ? `${linePath} L${points[points.length - 1].x.toFixed(2)},${rangeBottom.toFixed(2)} L${points[0].x.toFixed(2)},${rangeBottom.toFixed(2)} Z`
      : ''

  const axisTemps = useMemo(() => resolveAxisTemps(sensors, count), [sensors, count])

  const formatTempLabel = (index: number): string => {
    const temp = axisTemps[index]
    return temp == null ? '-' : `${temp}${CELSIUS}`
  }

  const formatTooltipValue = (sensor: FanCurveSensorData, index: number, v: number): string | null => {
    const temp = sensor.temps[index]
    if (temp == null || temp >= INVALID_TEMP) return null
    const rpmIndex = v - 1
    if (rpmIndex < 0) return `0 ${t('fanCurve.rpm')}`
    const rpm = sensor.fanSpeeds[rpmIndex]
    if (rpm == null) return null
    return `${temp}${CELSIUS} @ ${rpm} ${t('fanCurve.rpm')}`
  }

  const activeIndex = dragIndex ?? hoverIndex
  const tooltipRows =
    activeIndex == null || count === 0
      ? []
      : SENSOR_ORDER.map((type) => {
          const sensor = sensors.find((s) => s.type === type)
          if (!sensor) return null
          const valueText = formatTooltipValue(sensor, activeIndex, values[activeIndex] ?? 0)
          if (valueText == null) return null
          return {
            label: t(`fanCurve.${SENSOR_LABEL_KEYS[type]}`),
            value: valueText
          }
        }).filter((row): row is { label: string; value: string } => row != null)

  const tooltipPoint = activeIndex == null ? null : points[activeIndex]

  const chartHeight = height - PLOT_PADDING_TOP

  return (
    <div className={`udt-fan-curve${disabled ? ' udt-fan-curve--disabled' : ''}`}>
      <div className="udt-fan-curve__body">
        <div className="udt-fan-curve__y-labels">
          <span className="udt-fan-curve__y-label udt-fan-curve__y-label--top">
            {t('fanCurve.fanSpeedMax')}
          </span>
          <span className="udt-fan-curve__y-label">80%</span>
          <span className="udt-fan-curve__y-label">60%</span>
          <span className="udt-fan-curve__y-label">40%</span>
          <span className="udt-fan-curve__y-label">20%</span>
          <span className="udt-fan-curve__y-label udt-fan-curve__y-label--bottom">
            {t('fanCurve.fanSpeed')}
          </span>
        </div>

        <div className="udt-fan-curve__plot-wrap">
          <div className="udt-fan-curve__plot" style={{ height }}>
            <div
              className="udt-fan-curve__plot-inner"
              ref={plotRef}
              style={{ height: chartHeight }}
              onPointerMove={handlePointerMove}
              onPointerUp={handlePointerUp}
              onPointerCancel={handlePointerUp}
            >
              {plotSize.width > 0 && count > 1 && (
                <>
                <svg
                  className="udt-fan-curve__graph"
                  width={plotSize.width}
                  height={plotSize.height}
                  aria-hidden="true"
                >
                  <defs>
                    <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0" stopColor={LINE_COLOR} stopOpacity={0.431} />
                      <stop offset="1" stopColor={LINE_COLOR} stopOpacity={0.094} />
                    </linearGradient>
                  </defs>
                  {Array.from({ length: GRID_LINES + 1 }, (_, i) => {
                    const y = rangeTop + ((rangeBottom - rangeTop) * i) / GRID_LINES
                    return (
                      <line
                        key={`h-${i}`}
                        x1={0}
                        y1={y}
                        x2={plotSize.width}
                        y2={y}
                        className="udt-fan-curve__grid-line"
                      />
                    )
                  })}
                  {points.map((point, i) => (
                    <line
                      key={`v-${i}`}
                      x1={point.x}
                      y1={rangeTop}
                      x2={point.x}
                      y2={rangeBottom}
                      className="udt-fan-curve__grid-line udt-fan-curve__grid-line--column"
                    />
                  ))}
                  <path d={areaPath} fill={`url(#${gradientId})`} />
                  <path
                    d={linePath}
                    fill="none"
                    className="udt-fan-curve__line"
                    strokeWidth={2.25}
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>

                {points.map((point, i) => (
                  <div
                    key={`thumb-${i}`}
                    className={`udt-fan-curve__thumb-hit${dragIndex === i ? ' udt-fan-curve__thumb-hit--dragging' : ''}${hoverIndex === i ? ' udt-fan-curve__thumb-hit--hover' : ''}`}
                    style={{
                      left: `${point.x}px`,
                      top: `${point.y}px`
                    }}
                    onPointerDown={handlePointerDown(i)}
                    onPointerEnter={() => {
                      if (dragIndex == null) setHoverIndex(i)
                    }}
                    onPointerLeave={() => {
                      if (dragIndex == null) setHoverIndex(null)
                    }}
                  >
                    <div className="udt-fan-curve__thumb" />
                  </div>
                ))}
                </>
              )}
            </div>
          </div>

          {tooltipRows.length > 0 && tooltipPoint != null && (
            <div
              className="udt-fan-curve__tooltip"
              style={{
                left: PLOT_PADDING_X + tooltipPoint.x,
                top: PLOT_PADDING_TOP + tooltipPoint.y
              }}
            >
              {tooltipRows.map((row) => (
                <div key={row.label} className="udt-fan-curve__tooltip-row">
                  <dt>{row.label}</dt>
                  <dd>{row.value}</dd>
                </div>
              ))}
            </div>
          )}

          {plotSize.width > 0 && count > 1 && (
            <div
              className="udt-fan-curve__x-labels"
              style={{ width: plotSize.width, marginLeft: PLOT_PADDING_X, marginRight: PLOT_PADDING_X }}
            >
              {points.map((point, i) => (
                <span
                  key={i}
                  className="udt-fan-curve__x-label"
                  style={{ left: `${point.x}px` }}
                  title={formatTempLabel(i)}
                >
                  {formatTempLabel(i)}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
