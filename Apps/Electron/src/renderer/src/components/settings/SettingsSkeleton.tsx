import { SkeletonBone } from '../Skeleton'
import './settings.css'

export type SettingsSectionKey =
  | 'appearance'
  | 'application'
  | 'smartKeys'
  | 'display'
  | 'power'
  | 'update'
  | 'integrations'
  | 'osd'

export function AppearanceSectionSkeleton(): React.JSX.Element {
  return (
    <div
      className="udt-settings-section udt-settings-section--appearance"
      role="status"
      aria-label="Loading appearance settings"
    >
      {/* Card 1: Language */}
      <div className="udt-settings-card udt-settings-card--row">
        <div className="udt-settings-card__header">
          <div className="udt-settings-card__copy">
            <SkeletonBone delay={0} variant="on-card" width={80} height={18} radius="small" />
            <SkeletonBone
              delay={1}
              variant="on-card"
              width={140}
              height={13}
              radius="small"
              style={{ marginTop: 6 }}
            />
          </div>
          <SkeletonBone delay={2} variant="on-card" width={180} height={32} radius="control" />
        </div>
      </div>

      {/* Card 2: Temperature */}
      <div className="udt-settings-card udt-settings-card--row">
        <div className="udt-settings-card__header">
          <div className="udt-settings-card__copy">
            <SkeletonBone delay={2} variant="on-card" width={60} height={18} radius="small" />
            <SkeletonBone
              delay={3}
              variant="on-card"
              width={220}
              height={13}
              radius="small"
              style={{ marginTop: 6 }}
            />
          </div>
          <SkeletonBone delay={4} variant="on-card" width={180} height={32} radius="control" />
        </div>
      </div>

      {/* Card 3: Global Theme Mode & Accent Swatches */}
      <div className="udt-settings-card">
        <div className="udt-settings-card__header" style={{ marginBottom: 12 }}>
          <SkeletonBone delay={4} variant="on-card" width={160} height={18} radius="small" />
        </div>

        {/* 3 Theme Preview Cards */}
        <div className="udt-theme-options">
          {[0, 1, 2].map((i) => (
            <div key={i} className="udt-theme-option" style={{ pointerEvents: 'none' }}>
              <div className="udt-theme-option__preview">
                <SkeletonBone
                  delay={5 + i}
                  variant="on-card"
                  style={{ width: '100%', height: '100%', borderRadius: 'inherit' }}
                />
              </div>
              <div className="udt-theme-option__label-container">
                <SkeletonBone delay={6 + i} variant="on-card" width={60} height={14} radius="small" />
              </div>
            </div>
          ))}
        </div>

        {/* 2 Checkboxes */}
        <div className="udt-theme-accent-options" style={{ marginTop: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, minHeight: 24 }}>
            <SkeletonBone delay={8} variant="on-card" width={18} height={18} radius="small" />
            <SkeletonBone delay={9} variant="on-card" width={220} height={14} radius="small" />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, minHeight: 24 }}>
            <SkeletonBone delay={9} variant="on-card" width={18} height={18} radius="small" />
            <SkeletonBone delay={10} variant="on-card" width={250} height={14} radius="small" />
          </div>
        </div>

        <div className="udt-theme-accent-divider" role="separator" />

        {/* Accent title */}
        <div className="udt-theme-accent-title">
          <SkeletonBone delay={10} variant="on-card" width={110} height={16} radius="small" />
        </div>

        {/* 10 Color Swatches */}
        <div className="udt-settings-swatches">
          {Array.from({ length: 10 }).map((_, i) => (
            <SkeletonBone
              key={i}
              delay={11 + (i % 5)}
              variant="on-card"
              width={40}
              height={40}
              radius="round"
            />
          ))}
        </div>
      </div>

      {/* Card 4: UI Scale */}
      <div className="udt-settings-card udt-settings-card--row">
        <div className="udt-settings-card__header">
          <div className="udt-settings-card__copy">
            <SkeletonBone delay={13} variant="on-card" width={90} height={18} radius="small" />
            <SkeletonBone
              delay={14}
              variant="on-card"
              width={320}
              height={13}
              radius="small"
              style={{ marginTop: 6 }}
            />
          </div>
          <SkeletonBone delay={15} variant="on-card" width={180} height={32} radius="control" />
        </div>
      </div>
    </div>
  )
}

export function GenericSettingsSectionSkeleton({
  rows = 4,
  accessory = 'switch'
}: {
  rows?: number
  accessory?: 'switch' | 'select' | 'none'
}): React.JSX.Element {
  return (
    <div className="udt-settings-section" role="status" aria-label="Loading settings section">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="udt-settings-card udt-settings-card--row">
          <div className="udt-settings-card__header">
            <div className="udt-settings-card__copy">
              <SkeletonBone
                delay={i * 2}
                variant="on-card"
                width={120 + (i % 3) * 40}
                height={18}
                radius="small"
              />
              <SkeletonBone
                delay={i * 2 + 1}
                variant="on-card"
                width={200 + (i % 4) * 50}
                height={13}
                radius="small"
                style={{ marginTop: 6 }}
              />
            </div>
            {accessory === 'switch' && (
              <SkeletonBone
                delay={i * 2 + 2}
                variant="on-card"
                width={40}
                height={20}
                radius="round"
              />
            )}
            {accessory === 'select' && (
              <SkeletonBone
                delay={i * 2 + 2}
                variant="on-card"
                width={160}
                height={32}
                radius="control"
              />
            )}
          </div>
        </div>
      ))}
    </div>
  )
}

export function SettingsSectionSkeleton({
  section
}: {
  section: SettingsSectionKey
}): React.JSX.Element {
  switch (section) {
    case 'appearance':
      return <AppearanceSectionSkeleton />
    case 'application':
      return <GenericSettingsSectionSkeleton rows={6} accessory="switch" />
    case 'power':
      return <GenericSettingsSectionSkeleton rows={4} accessory="select" />
    case 'display':
      return <GenericSettingsSectionSkeleton rows={3} accessory="select" />
    case 'smartKeys':
      return <GenericSettingsSectionSkeleton rows={2} accessory="select" />
    case 'update':
      return <GenericSettingsSectionSkeleton rows={2} accessory="none" />
    case 'integrations':
      return <GenericSettingsSectionSkeleton rows={3} accessory="switch" />
    case 'osd':
      return <GenericSettingsSectionSkeleton rows={4} accessory="switch" />
    default:
      return <GenericSettingsSectionSkeleton rows={4} accessory="switch" />
  }
}
