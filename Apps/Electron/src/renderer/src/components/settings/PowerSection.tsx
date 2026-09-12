import { useEffect, useState } from 'react'
import { Select, Switch, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { settingsApi } from '../../api/settings'
import { wmiApi } from '../../api/wmi'
import { useSettingsStore } from '../../stores/settingsStore'
import { SettingsCard } from './SettingsCard'
import PowerModesModal from './PowerModesModal'
import PowerPlansModal from './PowerPlansModal'

type PowerModeMappingMode = 'Disabled' | 'WindowsPowerMode' | 'WindowsPowerPlan'

/** Windows-only entries (powercfg based) are hidden on other platforms. */
const PLATFORM: string = window.bridge?.platform ?? 'win32'

const POWER_MODE_MAPPING_OPTIONS: Array<{ value: PowerModeMappingMode; i18nKey: string }> = [
  { value: 'Disabled', i18nKey: 'settings.power.mappingModes.disabled' },
  { value: 'WindowsPowerMode', i18nKey: 'settings.power.mappingModes.windowsPowerMode' },
  { value: 'WindowsPowerPlan', i18nKey: 'settings.power.mappingModes.windowsPowerPlan' }
]

const SMART_FN_LOCK_MODIFIERS: Array<{ flag: number; i18nKey: string }> = [
  { flag: 1, i18nKey: 'settings.power.modifierKeys.shift' },
  { flag: 2, i18nKey: 'settings.power.modifierKeys.ctrl' },
  { flag: 4, i18nKey: 'settings.power.modifierKeys.alt' }
]

export function PowerSection(): React.JSX.Element {
  const { t } = useTranslation()
  const { scopes, load, setScope } = useSettingsStore()
  const [powerModesOpen, setPowerModesOpen] = useState(false)
  const [powerPlansOpen, setPowerPlansOpen] = useState(false)
  const [godModeFnQ, setGodModeFnQ] = useState<{ supported: boolean; enabled: boolean | null } | null>(null)
  const [godModeFnQLoading, setGodModeFnQLoading] = useState(false)

  useEffect(() => {
    void load()
  }, [load])

  // Electron parity (SettingsPowerControl.RefreshAsync): probe the capability and read
  // the current value; hide the card when unsupported or the read fails.
  useEffect(() => {
    void (async () => {
      try {
        const status = await wmiApi.getGodModeFnQ()
        setGodModeFnQ(status)
      } catch {
        setGodModeFnQ(null)
      }
    })()
  }, [])

  const persistGodModeFnQ = async (checked: boolean): Promise<void> => {
    setGodModeFnQLoading(true)
    try {
      await wmiApi.setGodModeFnQ(checked)
      setGodModeFnQ((previous) => (previous ? { ...previous, enabled: checked } : previous))
      message.success(t('settings.saved'))
    } catch (error) {
      message.error((error as Error).message)
    } finally {
      setGodModeFnQLoading(false)
    }
  }

  const editorsEnabled = typeof scopes.application === 'object' && scopes.application !== null
  const app = (editorsEnabled ? scopes.application : {}) as Record<string, unknown>
  const powerModeMappingMode =
    (app['PowerModeMappingMode'] as PowerModeMappingMode | undefined) ?? 'WindowsPowerMode'
  const synchronizeBrightness = (app['SynchronizeBrightnessToAllPowerPlans'] as boolean | undefined) ?? false
  const smartFnLockFlags = (app['SmartFnLockFlags'] as number | undefined) ?? 0
  const resetBatteryOnSince = (app['ResetBatteryOnSinceTimerOnReboot'] as boolean | undefined) ?? false

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

  const selectedModifierFlags = SMART_FN_LOCK_MODIFIERS.filter(
    (modifier) => (smartFnLockFlags & modifier.flag) !== 0
  ).map((modifier) => modifier.flag)

  return (
    <div className="udt-settings-section udt-settings-section--power">
      {godModeFnQ?.supported === true && godModeFnQ.enabled !== null && (
        <SettingsCard
          title={t('settings.power.godModeFnQ', { defaultValue: 'Switch to Custom Mode with Fn+Q' })}
          description={t('settings.power.godModeFnQDesc', {
            defaultValue: 'Allow quick switching to Custom Mode with Fn+Q.'
          })}
          action={
            <Switch
              className="udt-settings-switch"
              checked={godModeFnQ.enabled}
              loading={godModeFnQLoading}
              onChange={(checked) => void persistGodModeFnQ(checked)}
            />
          }
        />
      )}
      <SettingsCard
        title={t('settings.power.powerModeMapping')}
        description={t('settings.power.powerModeMappingDesc')}
        action={
          <Select<PowerModeMappingMode>
            className="udt-settings-select"
            value={powerModeMappingMode}
            disabled={!editorsEnabled}
            onChange={(value) => void persistApplication({ PowerModeMappingMode: value })}
            options={POWER_MODE_MAPPING_OPTIONS.map((option) => ({
              value: option.value,
              label: t(option.i18nKey)
            }))}
          />
        }
      />
      {PLATFORM === 'win32' && (
        <>
          <SettingsCard
            title={t('settings.power.windowsPowerModes')}
            description={t('settings.power.windowsPowerModesDesc')}
            onClick={() => setPowerModesOpen(true)}
          />
          <SettingsCard
            title={t('settings.power.windowsPowerPlans')}
            description={t('settings.power.windowsPowerPlansDesc')}
            onClick={() => setPowerPlansOpen(true)}
          />
        </>
      )}
      <SettingsCard
        title={t('settings.power.synchronizeBrightness')}
        description={t('settings.power.synchronizeBrightnessDesc')}
        action={
          <Switch
            className="udt-settings-switch"
            checked={synchronizeBrightness}
            disabled={!editorsEnabled}
            onChange={(checked) =>
              void persistApplication({ SynchronizeBrightnessToAllPowerPlans: checked })
            }
          />
        }
      />
      <SettingsCard
        title={t('settings.power.smartFnLock')}
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
        title={t('settings.power.resetBatteryOnSince')}
        description={t('settings.power.resetBatteryOnSinceDesc')}
        action={
          <Switch
            className="udt-settings-switch"
            checked={resetBatteryOnSince}
            disabled={!editorsEnabled}
            onChange={(checked) => void persistApplication({ ResetBatteryOnSinceTimerOnReboot: checked })}
          />
        }
      />
      <PowerModesModal open={powerModesOpen} onClose={() => setPowerModesOpen(false)} />
      <PowerPlansModal open={powerPlansOpen} onClose={() => setPowerPlansOpen(false)} />
    </div>
  )
}
