import { lazy, Suspense, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ArrowSync24Regular, FluentIcon } from '../components/icons/fluent'
import { isInstallerOptionalFeatureEnabled } from '../../../shared/installer-selection'
import { useHostCapabilitiesStore } from '../stores/hostCapabilitiesStore'
import CapabilityUnavailable from '../components/utils/CapabilityUnavailable'
import './pages.css'

const AutomationPage = lazy(() => import('./AutomationPage'))
const MacroPage = lazy(() => import('./MacroPage'))

type ActionView = 'automation' | 'macro'

function ActionFallback(): React.JSX.Element {
  return (
    <div className="udt-actions-page__fallback" aria-label="Loading">
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
  const installerFeatures = window.bridge?.installerSelection?.features
  const view: ActionView = searchParams.get('view') === 'macro' ? 'macro' : 'automation'
  const hasAutomation = isInstallerOptionalFeatureEnabled(installerFeatures, 'automation')
  const hasMacro = isInstallerOptionalFeatureEnabled(installerFeatures, 'macro')
  const canUseAutomation = hasAutomation && capabilities?.capabilities.automation !== false
  const canUseMacro = hasMacro && capabilities?.capabilities.macro !== false
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

  return (
    <div className="udt-actions-page">
      <header className="udt-actions-page__header">
        <div className="udt-actions-page__tabs" role="tablist" aria-label={t('nav.actions')}>
          {canUseAutomation && (
            <button
              type="button"
              role="tab"
              aria-selected={availableView === 'automation'}
              className={`udt-actions-page__tab${availableView === 'automation' ? ' udt-actions-page__tab--active' : ''}`}
              onClick={() => selectView('automation')}
            >
              {t('nav.automation')}
            </button>
          )}
          {canUseMacro && (
            <button
              type="button"
              role="tab"
              aria-selected={availableView === 'macro'}
              className={`udt-actions-page__tab${availableView === 'macro' ? ' udt-actions-page__tab--active' : ''}`}
              onClick={() => selectView('macro')}
            >
              {t('nav.macro')}
            </button>
          )}
        </div>
      </header>
      <Suspense fallback={<ActionFallback />}>
        {availableView === 'automation' ? <AutomationPage /> : <MacroPage />}
      </Suspense>
    </div>
  )
}
