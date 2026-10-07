import { useTranslation } from 'react-i18next'
import { useHostCapabilitiesStore } from '../../shared/state/hostCapabilitiesStore'
import InfoBar from '../../shared/ui/InfoBar'

export default function DiagnosticModeBanner(): React.JSX.Element | null {
  const { t } = useTranslation()
  const executionMode = useHostCapabilitiesStore((state) => state.capabilities?.executionMode)
  if (executionMode !== 'diagnostic') return null

  return (
    <div className="udt-diagnostic-mode-banner" role="status">
      <InfoBar
        severity="warning"
        title={t('app.diagnosticModeTitle')}
        message={t('app.diagnosticModeDescription')}
      />
    </div>
  )
}
