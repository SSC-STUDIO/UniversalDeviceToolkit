import { useEffect, useState } from 'react'
import { Alert, Button, Input, Select, Switch, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { updateApi } from '../../api/update'
import { settingsApi } from '../../api/settings'
import { useSettingsStore } from '../../stores/settingsStore'
import { openUpdateModal } from '../utils/UpdateModal'
import { SettingsCard } from './SettingsCard'

const UPDATE_CHECK_FREQUENCIES: Array<{ value: string; i18nKey: string }> = [
  { value: 'PerHour', i18nKey: 'settings.update.frequencies.perHour' },
  { value: 'PerThreeHours', i18nKey: 'settings.update.frequencies.perThreeHours' },
  { value: 'PerTwelveHours', i18nKey: 'settings.update.frequencies.perTwelveHours' },
  { value: 'PerDay', i18nKey: 'settings.update.frequencies.perDay' },
  { value: 'PerWeek', i18nKey: 'settings.update.frequencies.perWeek' },
  { value: 'PerMonth', i18nKey: 'settings.update.frequencies.perMonth' }
]

export function UpdateSection(): React.JSX.Element {
  const { t } = useTranslation()
  const { scopes, load, setScope } = useSettingsStore()
  const [checking, setChecking] = useState(false)
  const [checkResult, setCheckResult] = useState<{
    available: boolean
    version?: string | null
    error?: string | null
  } | null>(null)
  const [repositoryOwner, setRepositoryOwner] = useState('')
  const [repositoryName, setRepositoryName] = useState('')

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    const store = scopes.updateCheck as Record<string, unknown> | undefined
    setRepositoryOwner((store?.['UpdateRepositoryOwner'] as string | null | undefined) ?? '')
    setRepositoryName((store?.['UpdateRepositoryName'] as string | null | undefined) ?? '')
  }, [scopes.updateCheck])

  const updateCheck = (scopes.updateCheck ?? {}) as Record<string, unknown>
  const updateCheckFrequency = (updateCheck['UpdateCheckFrequency'] as string | undefined) ?? 'PerDay'
  const includePrereleaseUpdates = (updateCheck['IncludePrereleaseUpdates'] as boolean | undefined) ?? false

  const persistUpdateCheck = async (patch: Record<string, unknown>): Promise<void> => {
    const current = (scopes.updateCheck ?? {}) as Record<string, unknown>
    const next = { ...current, ...patch }
    setScope('updateCheck', next)
    try {
      await settingsApi.set('updateCheck', next)
      await settingsApi.save(['updateCheck'])
      message.success(t('settings.saved'))
    } catch {
      message.error(t('settings.saveFailed'))
    }
  }

  // Electron parity (SettingsUpdateControl): trim; empty text persists as null (use default).
  const persistRepository = async (): Promise<void> => {
    const owner = repositoryOwner.trim()
    const name = repositoryName.trim()
    await persistUpdateCheck({
      UpdateRepositoryOwner: owner === '' ? null : owner,
      UpdateRepositoryName: name === '' ? null : name
    })
  }

  const handleCheckForUpdates = async (): Promise<void> => {
    setChecking(true)
    setCheckResult(null)
    try {
      const result = await updateApi.check(true)
      if (result.error) {
        setCheckResult(result)
        message.error(result.error)
        return
      }
      if (!result.available) {
        setCheckResult(result)
        message.info(t('update.checkResult.latest'))
        return
      }
      const { release } = await updateApi.getRelease()
      if (release == null) {
        const error = t('update.checkResult.installerUnavailable', {
          defaultValue: 'An update is available but no compatible installer was found for this installation.'
        })
        setCheckResult({ available: false, version: result.version, error })
        message.error(error)
        return
      }
      setCheckResult({ available: true, version: release.version })
    } catch (error) {
      message.error((error as Error).message)
    } finally {
      setChecking(false)
    }
  }

  return (
    <div className="udt-settings-section udt-settings-section--update">
      <SettingsCard
        title={t('settings.update.frequency')}
        action={
          <Select<string>
            className="udt-settings-select"
            value={updateCheckFrequency}
            onChange={(value) => void persistUpdateCheck({ UpdateCheckFrequency: value })}
            options={UPDATE_CHECK_FREQUENCIES.map((option) => ({
              value: option.value,
              label: t(option.i18nKey)
            }))}
          />
        }
      />
      <SettingsCard
        title={t('settings.update.includePrerelease')}
        description={t('settings.update.includePrereleaseDesc')}
        action={
          <Switch
            className="udt-settings-switch"
            checked={includePrereleaseUpdates}
            onChange={(checked) => void persistUpdateCheck({ IncludePrereleaseUpdates: checked })}
          />
        }
      />
      <SettingsCard
        title={t('settings.update.repository', { defaultValue: 'Update repository' })}
        description={t('settings.update.repositoryDesc', {
          defaultValue: 'Configure the GitHub repository to check for updates. Leave empty to use default.'
        })}
      >
        <div className="udt-settings-card__fields">
          <div className="udt-settings-row">
            <span className="udt-settings-row__label">
              {t('settings.update.repositoryOwner', { defaultValue: 'Repository Owner' })}
            </span>
            <Input
              className="udt-settings-select"
              value={repositoryOwner}
              placeholder={t('settings.update.repositoryOwnerPlaceholder', { defaultValue: 'e.g., SSC-STUDIO' })}
              onChange={(event) => setRepositoryOwner(event.target.value)}
              onBlur={() => void persistRepository()}
            />
          </div>
          <div className="udt-settings-row">
            <span className="udt-settings-row__label">
              {t('settings.update.repositoryName', { defaultValue: 'Repository Name' })}
            </span>
            <Input
              className="udt-settings-select"
              value={repositoryName}
              placeholder={t('settings.update.repositoryNamePlaceholder', { defaultValue: 'e.g., UniversalDeviceToolkit' })}
              onChange={(event) => setRepositoryName(event.target.value)}
              onBlur={() => void persistRepository()}
            />
          </div>
        </div>
      </SettingsCard>
      <SettingsCard
        title={t('settings.update.check')}
        action={
          <Button
            type="primary"
            className="udt-settings-check-button"
            onClick={() => void handleCheckForUpdates()}
            loading={checking}
          >
            {t('settings.update.check')}
          </Button>
        }
      >
        {checkResult?.available ? (
          <Alert
            className="udt-settings-card__alert"
            type="success"
            showIcon
            message={t('update.checkResult.available', { version: checkResult.version ?? '' })}
            action={
              <Button size="small" type="primary" onClick={() => void openUpdateModal({ version: checkResult.version ?? null })}>
                {t('wpf.update')}
              </Button>
            }
          />
        ) : null}
      </SettingsCard>
    </div>
  )
}
