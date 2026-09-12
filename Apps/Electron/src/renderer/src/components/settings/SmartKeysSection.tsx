import { useEffect, useState } from 'react'
import { Select, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { settingsApi } from '../../api/settings'
import { useSettingsStore } from '../../stores/settingsStore'
import { SettingsCard } from './SettingsCard'
import SmartKeyPipelinesModal from './SmartKeyPipelinesModal'

const SMART_FN_LOCK_MODIFIERS: Array<{ flag: number; i18nKey: string }> = [
  { flag: 1, i18nKey: 'settings.power.modifierKeys.shift' },
  { flag: 2, i18nKey: 'settings.power.modifierKeys.ctrl' },
  { flag: 4, i18nKey: 'settings.power.modifierKeys.alt' }
]

export function SmartKeysSection(): React.JSX.Element {
  const { t } = useTranslation()
  const { scopes, load, setScope } = useSettingsStore()
  const [singlePressOpen, setSinglePressOpen] = useState(false)
  const [doublePressOpen, setDoublePressOpen] = useState(false)

  useEffect(() => {
    void load()
  }, [load])

  const editorsEnabled = typeof scopes.application === 'object' && scopes.application !== null
  const app = (editorsEnabled ? scopes.application : {}) as Record<string, unknown>
  const smartFnLockFlags = (app['SmartFnLockFlags'] as number | undefined) ?? 0
  const selectedModifierFlags = SMART_FN_LOCK_MODIFIERS.filter(
    (modifier) => (smartFnLockFlags & modifier.flag) !== 0
  ).map((modifier) => modifier.flag)

  const persistApplication = async (patch: Record<string, unknown>): Promise<void> => {
    if (!editorsEnabled) return
    const current = (scopes.application ?? {}) as Record<string, unknown>
    const next = { ...current, ...patch }
    setScope('application', next)
    try {
      await settingsApi.set('application', next)
      await settingsApi.save(['application'])
      message.success(t('settings.saved'))
    } catch {
      message.error(t('settings.saveFailed'))
    }
  }

  return (
    <div className="udt-settings-section udt-settings-section--smart-keys">
      <SettingsCard
        title={t('settings.smartKeys.smartFnLock')}
        description={t('settings.smartKeys.smartFnLockDesc')}
        action={
          <Select<number[]>
            className="udt-settings-select"
            mode="multiple"
            allowClear
            maxTagCount="responsive"
            placeholder={t('settings.smartKeys.off')}
            value={selectedModifierFlags.length > 0 ? selectedModifierFlags : undefined}
            disabled={!editorsEnabled}
            onChange={(values) => {
              const flags = (values ?? []).reduce((acc, flag) => acc | flag, 0)
              void persistApplication({ SmartFnLockFlags: flags })
            }}
            options={SMART_FN_LOCK_MODIFIERS.map((modifier) => ({
              value: modifier.flag,
              label: t(modifier.i18nKey)
            }))}
          />
        }
      />
      <SettingsCard
        title={t('wpf.settingsPagesmartKeySinglePressActiontitle')}
        description={t('settings.smartKeys.singlePressActionDesc')}
        onClick={() => setSinglePressOpen(true)}
      />
      <SettingsCard
        title={t('wpf.settingsPagesmartKeyDoublePressActiontitle')}
        description={t('settings.smartKeys.doublePressActionDesc')}
        onClick={() => setDoublePressOpen(true)}
      />
      <SmartKeyPipelinesModal
        open={singlePressOpen}
        onClose={() => setSinglePressOpen(false)}
        onSaved={() => void load()}
      />
      <SmartKeyPipelinesModal
        open={doublePressOpen}
        isDoublePress
        onClose={() => setDoublePressOpen(false)}
        onSaved={() => void load()}
      />
    </div>
  )
}
