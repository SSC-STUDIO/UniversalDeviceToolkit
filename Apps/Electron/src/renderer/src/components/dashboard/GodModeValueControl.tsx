import { ArrowCounterclockwise24Regular } from '../icons/fluent'
import { Select, Slider } from 'antd'
import { useTranslation } from 'react-i18next'

/**
 * God Mode value row — port of Electron Controls/Dashboard/GodMode/GodModeValueControl.xaml(.cs).
 * Renders title/description with either a stepped slider (Min/Max/Step) or a
 * combo box of explicit steps, plus a reset-to-default button when a default exists.
 */

export interface StepperValue {
  /** Explicit steps; when non-empty a combo box is rendered instead of a slider. */
  steps?: number[]
  /** Slider minimum (when steps are empty). */
  min?: number
  /** Slider maximum (when steps are empty). */
  max?: number
  /** Slider tick/step (when steps are empty). */
  step?: number
  value?: number
  defaultValue?: number
}

export interface GodModeValueControlProps {
  title: string
  description?: string
  unit?: string
  stepper?: StepperValue | null | undefined
  /** Electron clamps out-of-range slider values to the default; falls back to default. */
  onChange?: (value: number) => void
}

/** Electron MathExtensions.RoundNearest(value, step). */
function roundNearest(value: number, step: number): number {
  if (step <= 0) return value
  return Math.round(value / step) * step
}

export default function GodModeValueControl({
  title,
  description,
  unit = '',
  stepper,
  onChange
}: GodModeValueControlProps): React.JSX.Element | null {
  const { t } = useTranslation()

  if (!stepper) return null

  const steps = stepper.steps ?? []
  const hasCombo = steps.length > 0
  const defaultValue = stepper.defaultValue
  const hasDefault = defaultValue != null

  if (hasCombo) {
    const current = stepper.value ?? steps[0]
    return (
      <div className="udt-god-mode-value">
        <div className="udt-god-mode-value__copy">
          <div className="udt-god-mode-value__title">{title}</div>
          {description != null && description !== '' && (
            <div className="udt-god-mode-value__description">{description}</div>
          )}
        </div>
        <Select
          className="udt-god-mode-value__select"
          aria-label={title}
          size="small"
          value={steps.includes(current) ? current : steps[0]}
          onChange={(value) => onChange?.(value)}
          options={steps.map((v) => ({ value: v, label: unit ? `${v} ${unit}` : String(v) }))}
        />
        {hasDefault && (
          <button
            type="button"
            className="udt-god-mode-value__reset"
            aria-label={title}
            title={t('common.resetDefault', { defaultValue: 'Reset to default' })}
            onClick={() => onChange?.(defaultValue!)}
          >
            <ArrowCounterclockwise24Regular />
          </button>
        )}
      </div>
    )
  }

  const min = stepper.min ?? 0
  const max = stepper.max ?? 100
  const step = stepper.step ?? 1
  const rawValue = stepper.value ?? min
  // Electron: out-of-range values fall back to the default, then clamp to [min, max].
  const clamped =
    defaultValue != null && (rawValue < min || rawValue > max)
      ? defaultValue
      : Math.min(max, Math.max(min, roundNearest(rawValue, step)))

  return (
    <div className="udt-god-mode-value">
      <div className="udt-god-mode-value__copy">
        <div className="udt-god-mode-value__title">{title}</div>
        {description != null && description !== '' && (
          <div className="udt-god-mode-value__description">{description}</div>
        )}
      </div>
      <Slider
        className="udt-god-mode-value__slider"
        aria-label={title}
        min={min}
        max={max}
        step={step}
        value={clamped}
        onChange={(value) => onChange?.(value)}
      />
      <span className="udt-god-mode-value__label" aria-live="polite">
        {unit ? `${clamped} ${unit}` : String(clamped)}
      </span>
      {hasDefault && (
        <button
          type="button"
          className="udt-god-mode-value__reset"
          aria-label={title}
          title={t('common.resetDefault', { defaultValue: 'Reset to default' })}
          onClick={() => onChange?.(defaultValue!)}
        >
          <ArrowCounterclockwise24Regular />
        </button>
      )}
    </div>
  )
}
