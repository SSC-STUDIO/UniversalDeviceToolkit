import { useTranslation } from 'react-i18next'

interface SettingsLoadErrorProps {
  message?: string | null
  onRetry: () => void
}

export function SettingsLoadError({ message, onRetry }: SettingsLoadErrorProps): React.JSX.Element {
  const { t } = useTranslation()
  return (
    <div className="udt-settings-load-error" role="alert">
      <h3 className="udt-settings-load-error__title">{t('common.error')}</h3>
      {message != null && message.length > 0 ? (
        <p className="udt-settings-load-error__message">{message}</p>
      ) : null}
      <button type="button" className="udt-btn udt-btn--secondary" onClick={onRetry}>
        {t('common.retry', { defaultValue: 'Retry' })}
      </button>
    </div>
  )
}
