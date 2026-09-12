import { memo, useEffect, useId, useRef } from 'react'
import { useThemeStore } from '../../stores/themeStore'

export interface SensorGaugeProps {
  value?: number | null
  min?: number
  max?: number
  unit?: string
  label?: string
  color?: string
  digits?: number
  size?: number
  thickness?: number
}

/** Open-bottom 270° arc (gap at bottom), matching WPF RadialGaugeControl sweep. */
const SWEEP_FRACTION = 270 / 360
const ROTATE_DEG = 135
const SVG_RADIUS = 42
const CAPTION_MIN_DIAMETER = 80

function lighten(hex: string, amount: number): string {
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  const channel = (c: number): number => Math.round(c + (255 - c) * amount)
  return `rgb(${channel(r)}, ${channel(g)}, ${channel(b)})`
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

function applyReadoutMetrics(el: HTMLElement, diameter: number): void {
  const valueFont = clamp(diameter * 0.2, 12, 28)
  const captionFont = clamp(diameter * 0.095, 9, 13)
  const gap = Math.max(2, Math.round(diameter * 0.035))
  el.style.setProperty('--udt-sensor-gauge-value-size', `${valueFont}px`)
  el.style.setProperty('--udt-sensor-gauge-caption-size', `${captionFont}px`)
  el.style.setProperty('--udt-sensor-gauge-gap', `${gap}px`)
  el.classList.toggle('udt-sensor-gauge--compact', diameter < CAPTION_MIN_DIAMETER)
}

function SensorGauge({
  value,
  min = 0,
  max = 100,
  unit,
  label,
  color = '#4f9df7',
  digits = 0,
  size = 110,
  thickness = 6
}: SensorGaugeProps): React.JSX.Element {
  const rootRef = useRef<HTMLDivElement>(null)
  const gradId = useId().replace(/:/g, '')
  const isDark = useThemeStore((s) => s.themeMode === 'dark')

  useEffect(() => {
    const root = rootRef.current
    if (!root) return
    const applySize = (): void => {
      const diameter = Math.round(root.getBoundingClientRect().width)
      if (diameter > 0) applyReadoutMetrics(root, diameter)
    }
    applySize()
    const observer = new ResizeObserver(applySize)
    observer.observe(root)
    window.addEventListener('resize', applySize)
    return () => {
      window.removeEventListener('resize', applySize)
      observer.disconnect()
    }
  }, [])

  const finite = value != null && Number.isFinite(value) ? value : null
  const ratio = finite == null ? 0 : clamp((finite - min) / (max - min), 0, 1)
  const circumference = 2 * Math.PI * SVG_RADIUS
  const arcLength = circumference * SWEEP_FRACTION
  const progressLength = arcLength * ratio
  const trackStroke = isDark ? 'rgba(255, 255, 255, 0.14)' : 'rgba(0, 0, 0, 0.1)'

  const numeric = finite == null ? '—' : finite.toFixed(digits)
  const aria = [numeric, unit, label].filter((part) => part != null && part !== '').join(' ')
  const hasCaption = label != null && label !== ''

  return (
    <div
      ref={rootRef}
      className="udt-sensor-gauge"
      style={{ maxWidth: size, ['--udt-sensor-gauge-color' as string]: color }}
      role="img"
      aria-label={aria}
    >
      <svg className="udt-sensor-gauge__svg" viewBox="0 0 100 100" aria-hidden="true">
        <defs>
          <linearGradient id={gradId} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={lighten(color, 0.28)} />
            <stop offset="100%" stopColor={color} />
          </linearGradient>
        </defs>
        <circle
          className="udt-sensor-gauge__track"
          cx="50"
          cy="50"
          r={SVG_RADIUS}
          fill="none"
          stroke={trackStroke}
          strokeWidth={thickness}
          strokeLinecap="round"
          strokeDasharray={`${arcLength} ${circumference}`}
          transform={`rotate(${ROTATE_DEG} 50 50)`}
        />
        <circle
          className="udt-sensor-gauge__progress"
          cx="50"
          cy="50"
          r={SVG_RADIUS}
          fill="none"
          stroke={`url(#${gradId})`}
          strokeWidth={thickness}
          strokeLinecap="round"
          strokeDasharray={`${progressLength} ${circumference}`}
          transform={`rotate(${ROTATE_DEG} 50 50)`}
        />
      </svg>
      <div className="udt-sensor-gauge__readout">
        <span className="udt-sensor-gauge__value">
          {numeric}
          {unit != null && unit !== '' && <span className="udt-sensor-gauge__unit">{unit}</span>}
        </span>
        {hasCaption && <span className="udt-sensor-gauge__caption">{label}</span>}
      </div>
    </div>
  )
}

// All props are primitives, so memo skips the 1 Hz sensor re-render whenever
// the underlying reading did not change (common for battery/GPU idle values).
export default memo(SensorGauge)
