import type { TFunction } from 'i18next'
import { useTranslation } from 'react-i18next'
import type { DashboardGroup, DashboardItem } from '../../api/dashboard'
import { useDashboardHardware } from '../../hooks/useDashboardHardware'
import { useFeaturesStore } from '../../stores/featuresStore'
import DashboardFeatureCard from './DashboardFeatureCard'
import DashboardSpecialCard, { type SpecialDashboardItem } from './DashboardSpecialCard'
import HybridModeCard from './HybridModeCard'
import { isSpecialItemSupported } from './dashboardHardwareSupport'
import { isSpecialDashboardItem, resolveDashboardFeature } from './dashboardItems'
import './dashboardHardware.css'

function groupTitle(group: DashboardGroup, t: TFunction): string {
  if (group.type === 'Custom' && group.customName) return group.customName
  return t(`dashboard.group.${group.type.toLowerCase()}`, { defaultValue: group.type })
}

export default function DashboardFeatureGroupsHardware({
  groups
}: {
  groups: DashboardGroup[]
}): React.JSX.Element {
  const { t } = useTranslation()
  const infos = useFeaturesStore((state) => state.infos)
  const loaded = useFeaturesStore((state) => state.loaded)
  const hardware = useDashboardHardware()

  function itemIsVisible(item: DashboardItem): boolean {
    if (isSpecialDashboardItem(item)) {
      if (hardware.state != null) {
        return isSpecialItemSupported(item as SpecialDashboardItem, hardware.state)
      }
      return hardware.error == null
    }
    const feature = resolveDashboardFeature(item, infos)
    if (feature == null) return false
    if (loaded && infos[feature]?.supported !== true) return false
    return true
  }

  function renderItem(item: DashboardItem): React.JSX.Element | null {
    if (isSpecialDashboardItem(item)) {
      if (hardware.state != null && isSpecialItemSupported(item as SpecialDashboardItem, hardware.state)) {
        return (
          <DashboardSpecialCard
            key={item}
            item={item as SpecialDashboardItem}
            hardware={hardware.state}
            error={hardware.error}
            onChanged={hardware.refresh}
          />
        )
      }
      return null
    }

    const feature = resolveDashboardFeature(item, infos)
    if (feature == null) return null
    if (feature === 'hybridMode') return <HybridModeCard key={item} />
    return <DashboardFeatureCard key={item} feature={feature} />
  }

  return (
    <div className="udt-parity-feature-groups">
      {groups.map((group, index) => {
        const visibleItems = group.items.filter(itemIsVisible)
        if (visibleItems.length === 0) return null

        return (
          <section key={`${group.type}-${index}`} className="udt-parity-feature-group">
            <h2>{groupTitle(group, t)}</h2>
            <div className="udt-parity-feature-group__items">
              {visibleItems.map(renderItem)}
            </div>
          </section>
        )
      })}
    </div>
  )
}
