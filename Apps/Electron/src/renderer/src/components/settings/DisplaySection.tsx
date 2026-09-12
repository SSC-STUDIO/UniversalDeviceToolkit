import { useEffect, useState } from 'react'
import { Select, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { bootLogoApi } from '../../api/bootLogo'
import { settingsApi } from '../../api/settings'
import { useSettingsStore } from '../../stores/settingsStore'
import { SettingsCard } from './SettingsCard'
import WindowBackdropSetting from './WindowBackdropSetting'
import BootLogoModal from './BootLogoModal'
import ExcludeRefreshRatesModal from './ExcludeRefreshRatesModal'
import NavigationItemsSetting from './NavigationItemsSetting'
import NotificationsModal from './NotificationsModal'
import {
  buildNotificationDurationOptions,
  buildNotificationPositionOptions,
  sanitizeNotificationPosition
} from './notificationSettingsOptions'

export function DisplaySection(): React.JSX.Element {
  const { t } = useTranslation()
  const { scopes, load, setScope } = useSettingsStore()
  const [notificationsOpen, setNotificationsOpen] = useState(false)
  const [excludeRefreshRatesOpen, setExcludeRefreshRatesOpen] = useState(false)
  const [bootLogoOpen, setBootLogoOpen] = useState(false)
  const [bootLogoSupported, setBootLogoSupported] = useState(false)

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    let cancelled = false
    bootLogoApi
      .getStatus()
      .then(() => {
        if (!cancelled) setBootLogoSupported(true)
      })
      .catch(() => {
        // Hidden when unsupported, mirroring Electron BootLogo.IsSupportedAsync gate.
      })
    return () => {
      cancelled = true
    }
  }, [])

  const editorsEnabled = typeof scopes.application === 'object' && scopes.application !== null
  const app = (editorsEnabled ? scopes.application : {}) as Record<string, unknown>
  const notificationPosition = sanitizeNotificationPosition(app['NotificationPosition'])
  const notificationDuration = (app['NotificationDuration'] as string | undefined) ?? 'Normal'
  const excludedRefreshRates = (app['ExcludedRefreshRates'] ?? []) as Array<{ Frequency: number }>

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
    <div className="udt-settings-section udt-settings-section--display">
      <NavigationItemsSetting />

      <div className="udt-settings-group-title">{t('settings.display.groupNotifications')}</div>
      <SettingsCard
        title={t('settings.display.notifications')}
        description={t('settings.display.notificationsDesc')}
      >
        <div className="udt-settings-card__fields">
          <div className="udt-settings-row">
            <span className="udt-settings-row__label">{t('settings.display.notificationPosition')}</span>
            <Select<string>
              className="udt-settings-row__select udt-settings-select"
              value={notificationPosition}
              disabled={!editorsEnabled}
              onChange={(value) => void persistApplication({ NotificationPosition: value })}
              options={buildNotificationPositionOptions(t)}
            />
          </div>
          <div className="udt-settings-row">
            <span className="udt-settings-row__label">{t('settings.display.notificationDuration')}</span>
            <Select<string>
              className="udt-settings-row__select udt-settings-select"
              value={notificationDuration}
              disabled={!editorsEnabled}
              onChange={(value) => void persistApplication({ NotificationDuration: value })}
              options={buildNotificationDurationOptions(t)}
            />
          </div>
        </div>
        <button
          type="button"
          className="udt-settings-card__link-row"
          onClick={() => setNotificationsOpen(true)}
        >
          <span>{t('settings.display.notificationCategories')}</span>
        </button>
      </SettingsCard>

      <div className="udt-settings-group-title">{t('settings.display.groupWindow')}</div>
      <WindowBackdropSetting
        application={app}
        disabled={!editorsEnabled}
        persist={(patch) => void persistApplication(patch)}
      />
      {bootLogoSupported && (
        <SettingsCard
          title={t('settings.display.bootLogo')}
          description={t('settings.display.bootLogoDesc')}
          onClick={() => setBootLogoOpen(true)}
        />
      )}
      <SettingsCard
        title={t('settings.display.excludedRefreshRates')}
        description={t('settings.display.excludedRefreshRatesDesc')}
        onClick={() => setExcludeRefreshRatesOpen(true)}
      >
        {excludedRefreshRates.length === 0 ? (
          <div className="udt-settings-card__empty">
            {t('settings.display.excludedRefreshRatesEmpty')}
          </div>
        ) : (
          <div className="udt-settings-tag-list">
            {excludedRefreshRates.map((rate) => (
              <span key={rate.Frequency} className="udt-settings-tag-list__tag">
                {rate.Frequency} Hz
              </span>
            ))}
          </div>
        )}
      </SettingsCard>

      <NotificationsModal open={notificationsOpen} onClose={() => setNotificationsOpen(false)} />
      <ExcludeRefreshRatesModal
        open={excludeRefreshRatesOpen}
        onClose={() => setExcludeRefreshRatesOpen(false)}
        onSaved={() => void load()}
      />
      <BootLogoModal open={bootLogoOpen} onClose={() => setBootLogoOpen(false)} />
    </div>
  )
}
