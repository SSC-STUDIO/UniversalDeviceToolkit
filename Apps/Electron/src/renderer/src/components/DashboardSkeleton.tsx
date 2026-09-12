import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import './dashboard/sensor.css'
import './dashboard-parity/dashboardParity.css'
import { SkeletonBone, SkeletonGaugeRing } from './Skeleton'
import { readSensorLayout } from './dashboard/SensorSection'
import { useSettingsStore } from '../stores/settingsStore'
import './DashboardSkeleton.css'

/**
 * Dashboard page skeleton — mirrors the live initial render 1:1:
 * SensorSection (heading with details toggle, gauge + iconed metric rows,
 * chart, legend) plus parity feature groups (group h2 + cards with a
 * select/switch accessory). Reuses the live layout classes so breakpoints,
 * container queries and zoom apply to the skeleton exactly like the real UI.
 * Details blocks are intentionally absent: the live board collapses them
 * until the user toggles "Show details".
 */

const METRIC_ROWS = 3
const METRIC_LABEL_WIDTHS = [52, 44, 40]
const METRIC_VALUE_WIDTHS = [58, 48, 36]

interface SensorSkeletonColumnProps {
  titleWidth: number
  subtitleWidth: number
  metricWidths?: number[]
  staggerBase: number
  /** Live legends differ per column: CPU/GPU show 3 series, battery 2. */
  legendCount?: number
}

/** One CPU/battery/GPU skeleton column — reused by SensorSection while loading. */
export function SensorSkeletonColumn({
  titleWidth,
  subtitleWidth,
  metricWidths = METRIC_LABEL_WIDTHS,
  staggerBase,
  legendCount = 3
}: SensorSkeletonColumnProps): React.JSX.Element {
  return (
    <div className="udt-sensor-panel udt-dsk-sensors__column">
      <div className="udt-sensor-panel__heading udt-dsk-sensors__heading">
        <SkeletonBone delay={staggerBase} width={titleWidth} height={20} radius="small" />
        <SkeletonBone
          delay={staggerBase + 1}
          className="udt-sensor-panel__model udt-dsk-sensors__model"
          width={subtitleWidth}
          height={14}
          radius="small"
        />
        {/* "Show details" toggle pill at the trailing edge (live heading). */}
        <SkeletonBone
          delay={staggerBase + 2}
          className="udt-dsk-sensors__toggle"
          width={66}
          height={18}
          radius="small"
        />
      </div>

      <div className="udt-sensor-panel__summary udt-dsk-sensors__summary">
        <SkeletonGaugeRing delay={staggerBase + 2} />
        <div className="udt-sensor-panel__metrics udt-dsk-sensors__metrics">
          {Array.from({ length: METRIC_ROWS }, (_, row) => (
            <div key={row} className="udt-sensor-panel__metric udt-dsk-sensors__metric">
              {/* dt parity: 14px metric icon + label text. */}
              <span className="udt-dsk-sensors__metric-label">
                <SkeletonBone
                  delay={staggerBase + 3 + row * 3}
                  variant="muted"
                  width={14}
                  height={14}
                  radius="micro"
                />
                <SkeletonBone
                  delay={staggerBase + 3 + row * 3}
                  width={metricWidths[row] ?? METRIC_LABEL_WIDTHS[row]}
                  height={14}
                  radius="small"
                />
              </span>
              <SkeletonBone
                delay={staggerBase + 4 + row * 3}
                variant="muted"
                className="udt-skeleton-metric-bar udt-dsk-sensors__bar"
              />
              <SkeletonBone
                delay={staggerBase + 5 + row * 3}
                width={METRIC_VALUE_WIDTHS[row]}
                height={14}
                radius="small"
              />
            </div>
          ))}
        </div>
      </div>

      <div className="udt-sensor-panel__chart udt-dsk-sensors__trend">
        <div className="udt-skeleton-chart-well">
          <SkeletonBone
            delay={staggerBase + 11}
            variant="chart"
            className="udt-skeleton-chart-well__line"
          />
        </div>
      </div>

      <div className="udt-sensor-panel__legend udt-dsk-sensors__legend">
        {Array.from({ length: legendCount }, (_, item) => (
          <span key={item} className="udt-dsk-sensors__legend-item">
            <SkeletonBone
              delay={staggerBase + 12 + item}
              variant="muted"
              className="udt-skeleton-legend-dot"
              radius="micro"
            />
            <SkeletonBone delay={staggerBase + 12 + item} width={48} height={12} radius="small" />
          </span>
        ))}
      </div>
    </div>
  )
}

interface FeatureCardSkeletonProps {
  titleWidth: number
  descriptionWidth: number
  accessory: 'select' | 'switch'
  staggerBase: number
}

/** DashboardFeatureCard parity: 24px icon + title/description + trailing control. */
function FeatureCardSkeleton({
  titleWidth,
  descriptionWidth,
  accessory,
  staggerBase
}: FeatureCardSkeletonProps): React.JSX.Element {
  return (
    <div className="udt-parity-feature-card">
      <div className="udt-parity-feature-card__body">
        <SkeletonBone delay={staggerBase} variant="on-card" width={24} height={24} radius="small" />
        <div className="udt-parity-feature-card__copy">
          <SkeletonBone
            delay={staggerBase + 1}
            variant="on-card"
            width={titleWidth}
            height={15}
            radius="small"
          />
          <SkeletonBone
            delay={staggerBase + 2}
            variant="on-card"
            width={descriptionWidth}
            height={12}
            radius="small"
            style={{ marginTop: 6 }}
          />
        </div>
        <div className="udt-parity-feature-card__accessory">
          {accessory === 'select' ? (
            <SkeletonBone
              delay={staggerBase + 3}
              variant="on-card"
              className="udt-skeleton-select"
              radius="control"
            />
          ) : (
            <SkeletonBone
              delay={staggerBase + 3}
              variant="on-card"
              className="udt-skeleton-switch"
              radius="round"
            />
          )}
        </div>
      </div>
    </div>
  )
}

/** Typical initial dashboard: two groups (power / graphics) of three cards. */
const SKELETON_GROUPS: Array<{
  titleWidth: number
  cards: Array<Omit<FeatureCardSkeletonProps, 'staggerBase'>>
}> = [
  {
    titleWidth: 72,
    cards: [
      { titleWidth: 96, descriptionWidth: 200, accessory: 'select' },
      { titleWidth: 80, descriptionWidth: 150, accessory: 'select' },
      { titleWidth: 88, descriptionWidth: 220, accessory: 'select' }
    ]
  },
  {
    titleWidth: 64,
    cards: [
      { titleWidth: 88, descriptionWidth: 210, accessory: 'select' },
      { titleWidth: 104, descriptionWidth: 170, accessory: 'switch' },
      { titleWidth: 96, descriptionWidth: 190, accessory: 'switch' }
    ]
  }
]

export default function DashboardSkeleton(): React.JSX.Element {
  const { t } = useTranslation()
  const scopes = useSettingsStore((s) => s.scopes)
  const sensorLayout = useMemo(() => readSensorLayout(scopes), [scopes])

  return (
    <div className="udt-dashboard-skeleton" role="status" aria-label={t('common.loading')}>
      <div className="udt-sensor-board udt-dsk-sensors">
        <div
          className="udt-sensor-board__grid udt-dsk-sensors__grid"
          style={{ ['--udt-sensor-columns' as string]: String(sensorLayout.length) }}
        >
          {sensorLayout.map((columnId, idx) => {
            if (columnId === 'CPU') {
              return (
                <SensorSkeletonColumn
                  key="CPU"
                  titleWidth={72}
                  subtitleWidth={168}
                  staggerBase={idx * 16}
                />
              )
            }
            if (columnId === 'Battery') {
              return (
                <SensorSkeletonColumn
                  key="Battery"
                  titleWidth={64}
                  subtitleWidth={140}
                  metricWidths={[48, 44, 44]}
                  staggerBase={idx * 16}
                  legendCount={2}
                />
              )
            }
            return (
              <SensorSkeletonColumn
                key="GPU"
                titleWidth={68}
                subtitleWidth={152}
                metricWidths={[52, 40, 44]}
                staggerBase={idx * 16}
              />
            )
          })}
        </div>
      </div>
      <div className="udt-parity-feature-groups">
        {SKELETON_GROUPS.map((group, groupIndex) => (
          <div key={groupIndex} className="udt-parity-feature-group">
            <SkeletonBone
              delay={groupIndex * 14}
              className="udt-dsk-groups__title"
              width={group.titleWidth}
              height={26}
              radius="small"
            />
            <div className="udt-parity-feature-group__items">
              {group.cards.map((card, cardIndex) => (
                <FeatureCardSkeleton
                  key={cardIndex}
                  {...card}
                  staggerBase={groupIndex * 14 + 1 + cardIndex * 4}
                />
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
