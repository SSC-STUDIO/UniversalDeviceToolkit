import { lazy, Suspense, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ArrowSync24Regular, FluentIcon, Keyboard24Regular, Play24Regular } from '../../shared/ui/icons/fluent'
import { isNavigationFeatureVisible } from '../../../../shared/navigation-visibility'
import { useSettingsStore } from '../../shared/settings/settingsStore'
import { useHostCapabilitiesStore } from '../../shared/state/hostCapabilitiesStore'
import CapabilityUnavailable from '../../shared/ui/dialogs/CapabilityUnavailable'
import '../../shared/styles/pages.css'

const AutomationPage = lazy(() => import('./automation/AutomationPage'))
const MacroPage = lazy(() => import('./macro/MacroPage'))

type ActionView = 'automation' | 'macro'

function ActionFallback(): React.JSX.Element {
  const { t } = useTranslation()
  return (
    <div className="udt-actions-page__fallback" aria-label={t('common.loading')}>
      <FluentIcon size={28} spin color="var(--udt-accent-secondary)">
        <ArrowSync24Regular />
      </FluentIcon>
    </div>
  )
}

export default function ActionsPage(): React.JSX.Element {
  const { t } = useTranslation()
  const [searchParams, setSearchParams] = useSearchParams()
  const capabilities = useHostCapabilitiesStore((state) => state.capabilities)
  const applicationScope = useSettingsStore((state) => state.scopes.application)
  const navigationVisibility = useMemo(() => {
    const application = (applicationScope ?? {}) as Record<string, unknown>
    return (application.NavigationItemsVisibility as Record<string, boolean> | undefined) ?? {}
  }, [applicationScope])
  const installerFeatures = window.bridge?.installerSelection?.features
  const view: ActionView = searchParams.get('view') === 'macro' ? 'macro' : 'automation'
  const canUseAutomation = isNavigationFeatureVisible('automation', installerFeatures, navigationVisibility, capabilities?.capabilities)
  const canUseMacro = isNavigationFeatureVisible('macro', installerFeatures, navigationVisibility, capabilities?.capabilities)
  const availableView = useMemo<ActionView | null>(() => {
    if (view === 'automation' && canUseAutomation) return 'automation'
    if (view === 'macro' && canUseMacro) return 'macro'
    if (canUseAutomation) return 'automation'
    if (canUseMacro) return 'macro'
    return null
  }, [canUseAutomation, canUseMacro, view])

  if (availableView == null) return <CapabilityUnavailable title={t('nav.actions')} />

  const selectView = (next: ActionView): void => {
    setSearchParams({ view: next })
  }

  const sections: Array<{ view: ActionView; labelKey: string; icon: React.JSX.Element }> = []
  if (canUseAutomation) {
    sections.push({ view: 'automation', labelKey: 'nav.automation', icon: <Play24Regular /> })
  }
  if (canUseMacro) {
    sections.push({ view: 'macro', labelKey: 'nav.macro', icon: <Keyboard24Regular /> })
  }

  return (
    <div className="udt-actions-page">
      <div className="udt-actions-page__surface">
        <nav className="udt-actions-page__nav" aria-label={t('nav.actions')}>
          <ul className="udt-actions-page__nav-list">
            {sections.map((section) => {
              const isActive = section.view === availableView
              return (
                <li key={section.view}>
                  <button
                    type="button"
                    className={`udt-actions-nav-item${isActive ? ' udt-actions-nav-item--active' : ''}`}
                    aria-current={isActive ? 'true' : undefined}
                    onClick={() => selectView(section.view)}
                  >
                    <span className="udt-actions-nav-item__accent" />
                    <span className="udt-actions-nav-item__icon">{section.icon}</span>
                    <span className="udt-actions-nav-item__label">{t(section.labelKey)}</span>
                  </button>
                </li>
              )
            })}
          </ul>
        </nav>
        <div className="udt-actions-page__content">
          <Suspense fallback={<ActionFallback />}>
            {availableView === 'automation' ? <AutomationPage /> : <MacroPage />}
          </Suspense>
        </div>
      </div>
    </div>
  )
}
