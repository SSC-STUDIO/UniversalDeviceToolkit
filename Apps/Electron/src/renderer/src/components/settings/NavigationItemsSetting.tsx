import { useEffect, useMemo, useState } from 'react'
import { Spin, Switch, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { featuresApi } from '../../api/features'
import { keyboardApi } from '../../api/keyboard'
import { settingsApi } from '../../api/settings'
import { isInstallerOptionalFeatureEnabled } from '../../../../shared/installer-selection'
import { useSettingsStore } from '../../stores/settingsStore'
import { SettingsCard } from './SettingsCard'
import { SettingsLoadError } from './SettingsLoadError'

/**
 * Inline navigation-item toggles — port of Electron NavigationItemsSettingsWindow.
 * Dashboard and Settings are always visible. Keyboard is shown only when supported.
 */

const NAVIGATION_ITEMS: Array<{ key: string; labelKey: string }> = [
  { key: 'keyboard', labelKey: 'settings.display.navigationKeys.keyboard' },
  { key: 'automation', labelKey: 'settings.display.navigationKeys.automation' },
  { key: 'macro', labelKey: 'settings.display.navigationKeys.macro' },
  { key: 'windowsOptimization', labelKey: 'settings.display.navigationKeys.windowsOptimization' },
  { key: 'about', labelKey: 'settings.display.navigationKeys.about' }
]

export default function NavigationItemsSetting(): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const [visibility, setVisibility] = useState<Record<string, boolean>>({})
  const [keyboardSupported, setKeyboardSupported] = useState(false)
  const [itemsReady, setItemsReady] = useState(false)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setLoadError(null)
    setItemsReady(false)
    void (async () => {
      try {
        const [settingsResult, keyboardMode, infos] = await Promise.all([
          settingsApi.get('application'),
          keyboardApi.detect().catch(() => null),
          featuresApi.list().catch(() => [])
        ])
        if (cancelled) return

        const store = (settingsResult.value ?? {}) as Record<string, unknown>
        const stored =
          (store.NavigationItemsVisibility as Record<string, boolean> | undefined) ?? {}
        const supported = keyboardMode?.mode != null && keyboardMode.mode !== 'none'
        const whiteKeyboardSupported = infos.some(
          (info) =>
            info.supported &&
            (info.key === 'whiteKeyboard' || info.key === 'oneLevelWhiteKeyboard')
        )
        setKeyboardSupported(supported || whiteKeyboardSupported)
        setVisibility(
          Object.fromEntries(
            NAVIGATION_ITEMS.map((item) => [item.key, stored[item.key] !== false])
          )
        )
        setItemsReady(true)
      } catch (reason) {
        if (cancelled) return
        setItemsReady(false)
        setLoadError(reason instanceof Error ? reason.message : String(reason))
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [reloadToken])

  const installerFeatures = window.bridge?.installerSelection?.features
  const installerOmitted = NAVIGATION_ITEMS.some(
    (item) => !isInstallerOptionalFeatureEnabled(installerFeatures, item.key)
  )
  const visibleItems = useMemo(
    () =>
      NAVIGATION_ITEMS.filter(
        (item) =>
          isInstallerOptionalFeatureEnabled(installerFeatures, item.key) &&
          (item.key !== 'keyboard' || keyboardSupported)
      ),
    [installerFeatures, keyboardSupported]
  )

  const handleToggle = async (key: string, checked: boolean): Promise<void> => {
    if (!itemsReady) return
    const previous = visibility
    const next = { ...visibility, [key]: checked }
    setVisibility(next)
    try {
      const result = await settingsApi.get('application')
      const current = (result.value ?? {}) as Record<string, unknown>
      const merged = {
        ...current,
        NavigationItemsVisibility: {
          ...((current.NavigationItemsVisibility as Record<string, boolean> | undefined) ?? {}),
          ...next
        }
      }
      useSettingsStore.getState().setScope('application', merged)
      await settingsApi.set('application', merged)
      await settingsApi.save(['application'])
    } catch (reason) {
      void message.error((reason as Error).message)
      setVisibility(previous)
    }
  }

  return (
    <SettingsCard
      title={t('settings.display.navigationItems')}
      description={t('wpf.navigationItemsSettingsWindowdescription')}
    >
      {loading ? (
        <div className="udt-settings-inline-loading">
          <Spin size="small" />
        </div>
      ) : loadError != null || !itemsReady ? (
        <SettingsLoadError
          message={loadError}
          onRetry={() => setReloadToken((value) => value + 1)}
        />
      ) : (
        <>
          {installerOmitted ? (
            <p className="udt-settings-section__intro">{t('installer.features.omittedHint')}</p>
          ) : null}
          <div className="udt-settings-toggle-grid" role="list">
            {visibleItems.map((item) => (
              <div key={item.key} className="udt-settings-toggle-grid__item" role="listitem">
                <span className="udt-settings-toggle-grid__label">{t(item.labelKey)}</span>
                <Switch
                  className="udt-settings-switch"
                  checked={visibility[item.key] === true}
                  onChange={(checked) => void handleToggle(item.key, checked)}
                />
              </div>
            ))}
          </div>
        </>
      )}
    </SettingsCard>
  )
}
