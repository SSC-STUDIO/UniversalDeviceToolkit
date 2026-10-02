import { useTranslation } from 'react-i18next'
import { shimmerDelayStyle, skeletonBoneClass, type SkeletonBoneVariant } from '../utils/skeleton'

const LIST_ROW_ELEMENTS = 4

type RadiusToken = 'small' | 'control' | 'round' | 'micro'

function joinClass(...parts: Array<string | undefined | false>): string {
  return parts.filter(Boolean).join(' ')
}

function radiusStyle(radius?: RadiusToken | number): React.CSSProperties | undefined {
  if (radius == null) return undefined
  if (typeof radius === 'number') return { borderRadius: radius }
  const map: Record<RadiusToken, string> = {
    small: 'var(--udt-radius-small)',
    control: 'var(--udt-radius-control)',
    round: 'var(--udt-radius-round)',
    micro: 'var(--udt-radius-micro)'
  }
  return { borderRadius: map[radius] }
}

export interface SkeletonBoneProps {
  width?: number | string
  height?: number | string
  delay?: number
  variant?: SkeletonBoneVariant
  className?: string
  style?: React.CSSProperties
  radius?: RadiusToken | number
  'aria-label'?: string
  role?: React.AriaRole
}

/** Lowest-level shimmer bone — prefer this over raw `.udt-skeleton` divs. */
export function SkeletonBone({
  width,
  height,
  delay = 0,
  variant = 'default',
  className,
  style,
  radius,
  'aria-label': ariaLabel,
  role
}: SkeletonBoneProps): React.JSX.Element {
  const sizeStyle: React.CSSProperties = {
    ...(width != null ? { width } : {}),
    ...(height != null ? { height } : {}),
    ...radiusStyle(radius),
    ...style
  }
  return (
    <div
      className={skeletonBoneClass(variant, className)}
      style={{ ...shimmerDelayStyle(delay), ...sizeStyle }}
      role={role}
      aria-label={ariaLabel}
      aria-hidden={ariaLabel == null && role == null ? true : undefined}
    />
  )
}

/** Trailing control placeholder shape for card skeletons. */
export type SkeletonCardAccessory = 'none' | 'switch' | 'select'

export interface SkeletonCardProps {
  lines?: number
  withIcon?: boolean
  accessory?: SkeletonCardAccessory
  className?: string
  staggerBase?: number
  /** When false, a parent already exposes role=status (avoid nested live regions). */
  announce?: boolean
}

export function SkeletonCard({
  lines = 2,
  withIcon = false,
  accessory = 'none',
  className,
  staggerBase = 0,
  announce = true
}: SkeletonCardProps): React.JSX.Element {
  const { t } = useTranslation()
  const count = Math.max(1, lines)
  const lineStart = staggerBase + (withIcon ? 1 : 0)
  const accessoryStep = lineStart + count

  return (
    <div
      className={joinClass('udt-skeleton-card', className)}
      role={announce ? 'status' : undefined}
      aria-label={announce ? t('common.loading') : undefined}
      aria-hidden={announce ? undefined : true}
    >
      {withIcon && <SkeletonBone delay={staggerBase} variant="on-card" className="udt-skeleton-icon" />}
      <div className="udt-skeleton-card__copy">
        {Array.from({ length: count }, (_, index) => (
          <SkeletonBone
            key={index}
            delay={lineStart + index}
            variant="on-card"
            className={
              index === 0 ? 'udt-skeleton-line' : 'udt-skeleton-line udt-skeleton-line--secondary'
            }
          />
        ))}
      </div>
      {accessory === 'switch' && (
        <SkeletonBone delay={accessoryStep} variant="on-card" className="udt-skeleton-switch" radius="round" />
      )}
      {accessory === 'select' && (
        <SkeletonBone delay={accessoryStep} variant="on-card" className="udt-skeleton-select" radius="control" />
      )}
    </div>
  )
}

export function SkeletonList({
  rows = 3,
  className,
  withIcon = true,
  accessory = 'switch'
}: {
  rows?: number
  className?: string
  withIcon?: boolean
  accessory?: SkeletonCardAccessory
}): React.JSX.Element {
  const { t } = useTranslation()
  return (
    <div className={joinClass('udt-skeleton-list', className)} role="status" aria-label={t('common.loading')}>
      {Array.from({ length: Math.max(1, rows) }, (_, index) => (
        <SkeletonCard
          key={index}
          lines={2}
          withIcon={withIcon}
          accessory={accessory}
          staggerBase={index * LIST_ROW_ELEMENTS}
          announce={false}
        />
      ))}
    </div>
  )
}

const GAUGE_SVG_RADIUS = 42
const GAUGE_SWEEP = 270 / 360
const GAUGE_ROTATE = 135

export interface SkeletonGaugeRingProps {
  delay?: number
  size?: number
  valueDelay?: number
}

/** Open-bottom 270° ring matching SensorGauge.tsx geometry. */
export function SkeletonGaugeRing({
  delay = 0,
  size,
  valueDelay
}: SkeletonGaugeRingProps): React.JSX.Element {
  const circumference = 2 * Math.PI * GAUGE_SVG_RADIUS
  const arcLength = circumference * GAUGE_SWEEP
  const hintLength = arcLength * 0.58
  const sizeStyle = size != null ? { width: size, height: size } : undefined

  return (
    <div
      className="udt-skeleton udt-skeleton-gauge"
      style={{ ...shimmerDelayStyle(delay), ...sizeStyle }}
      aria-hidden="true"
    >
      <svg className="udt-skeleton-gauge__svg" viewBox="0 0 100 100" aria-hidden="true">
        <circle
          className="udt-skeleton-gauge__track"
          cx="50"
          cy="50"
          r={GAUGE_SVG_RADIUS}
          strokeDasharray={`${arcLength} ${circumference}`}
          transform={`rotate(${GAUGE_ROTATE} 50 50)`}
        />
        <circle
          className="udt-skeleton-gauge__arc"
          cx="50"
          cy="50"
          r={GAUGE_SVG_RADIUS}
          strokeDasharray={`${hintLength} ${circumference}`}
          transform={`rotate(${GAUGE_ROTATE} 50 50)`}
        />
      </svg>
      <SkeletonBone
        delay={valueDelay ?? delay + 1}
        variant="static"
        className="udt-skeleton-gauge__value"
      />
    </div>
  )
}
