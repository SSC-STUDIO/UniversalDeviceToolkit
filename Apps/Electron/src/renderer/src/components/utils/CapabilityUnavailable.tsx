import { Alert } from 'antd'
import { useTranslation } from 'react-i18next'

interface CapabilityUnavailableProps {
  title: string
}

export default function CapabilityUnavailable({ title }: CapabilityUnavailableProps): React.JSX.Element {
  const { t } = useTranslation()
  return (
    <div style={{ padding: 24 }}>
      <Alert type="warning" showIcon title={title} description={t('common.notSupportedOnPlatform')} />
    </div>
  )
}
