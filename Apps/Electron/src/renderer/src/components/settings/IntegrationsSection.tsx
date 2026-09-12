import { useEffect } from 'react'
import { Switch, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { settingsApi } from '../../api/settings'
import { useSettingsStore } from '../../stores/settingsStore'
import { SettingsCard } from './SettingsCard'

export function IntegrationsSection(): React.JSX.Element {
  const { t } = useTranslation()
  const { scopes, load, setScope } = useSettingsStore()

  useEffect(() => {
    void load()
  }, [load])

  const editorsEnabled = typeof scopes.integrations === 'object' && scopes.integrations !== null
  const integrations = (editorsEnabled ? scopes.integrations : {}) as Record<string, unknown>
  const hwinfoEnabled = (integrations['HWiNFO'] as boolean | undefined) ?? false
  const cliEnabled = (integrations['CLI'] as boolean | undefined) ?? false

  const persistIntegrations = async (patch: Record<string, unknown>): Promise<void> => {
    if (!editorsEnabled) return
    const current = (scopes.integrations ?? {}) as Record<string, unknown>
    const next = { ...current, ...patch }
    setScope('integrations', next)
    try {
      await settingsApi.set('integrations', next)
      await settingsApi.save(['integrations'])
      message.success(t('settings.saved'))
    } catch {
      message.error(t('settings.saveFailed'))
    }
  }

  return (
    <div className="udt-settings-section udt-settings-section--integrations">
      <SettingsCard
        title={t('settings.integrations.hwinfo')}
        description={t('settings.integrations.hwinfoDesc')}
        action={
          <Switch
            className="udt-settings-switch"
            checked={hwinfoEnabled}
            disabled={!editorsEnabled}
            onChange={(checked) => void persistIntegrations({ HWiNFO: checked })}
          />
        }
      />
      <SettingsCard
        title={t('settings.integrations.cli')}
        description={t('settings.integrations.cliDesc')}
        action={
          <Switch
            className="udt-settings-switch"
            checked={cliEnabled}
            disabled={!editorsEnabled}
            onChange={(checked) => void persistIntegrations({ CLI: checked })}
          />
        }
      />
    </div>
  )
}
