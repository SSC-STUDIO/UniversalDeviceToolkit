import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  Apps24Regular,
  Broom24Regular,
  PaintBrush24Regular,
  Desktop24Regular
} from '../components/icons/fluent'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { isHostUnavailableError, sanitizeBridgeError } from '../api/bridge'
import { featuresApi, type FeatureKey } from '../api/features'
import { useLoadingStore } from '../stores/loadingStore'
import { useSettingsStore } from '../stores/settingsStore'
import AppearanceSection from '../components/settings/AppearanceSection'
import ApplicationSection from '../components/settings/ApplicationSection'
import { PowerSection } from '../components/settings/PowerSection'
import { DisplaySection } from '../components/settings/DisplaySection'
import { SmartKeysSection } from '../components/settings/SmartKeysSection'
import { UpdateSection } from '../components/settings/UpdateSection'
import { IntegrationsSection } from '../components/settings/IntegrationsSection'
import { OsdSection } from '../components/settings/OsdSection'
import { SettingsLoadError } from '../components/settings/SettingsLoadError'
import { SettingsSectionSkeleton, type SettingsSectionKey } from '../components/settings/SettingsSkeleton'
import '../components/settings/settings.css'

type SettingsGroupKey = 'appearance' | 'application' | 'device' | 'maintenance'

interface SettingsGroupDefinition {
  key: SettingsGroupKey
  labelKey: string
  icon: React.JSX.Element
  skeleton: SettingsSectionKey
}

const LENOVO_FEATURE_KEYS: readonly FeatureKey[] = [
  'alwaysOnUsb', 'battery', 'batteryNightCharge', 'dpiScale', 'flipToStart', 'fnLock', 'hdr',
  'hybridMode', 'igpuMode', 'instantBoot', 'itsMode', 'microphone', 'oneLevelWhiteKeyboard',
  'overDrive', 'panelLogo', 'portsBacklight', 'powerMode', 'refreshRate', 'resolution',
  'touchpadLock', 'whiteKeyboard', 'winKey'
]

const GROUPS: readonly SettingsGroupDefinition[] = [
  { key: 'appearance', labelKey: 'settings.nav.appearance', icon: <PaintBrush24Regular />, skeleton: 'appearance' },
  { key: 'application', labelKey: 'settings.nav.application', icon: <Apps24Regular />, skeleton: 'application' },
  { key: 'device', labelKey: 'settings.nav.device', icon: <Desktop24Regular />, skeleton: 'display' },
  { key: 'maintenance', labelKey: 'settings.nav.maintenance', icon: <Broom24Regular />, skeleton: 'update' }
]

function renderGroup(group: SettingsGroupKey, supportsLenovoHardware: boolean, t: TFunction): React.JSX.Element {
  switch (group) {
    case 'appearance':
      return <AppearanceSection />
    case 'application':
      return <><ApplicationSection /><OsdSection /></>
    case 'device':
      return supportsLenovoHardware
        ? <><DisplaySection /><PowerSection /><SmartKeysSection /></>
        : <div className="udt-settings-group-empty">{t('settings.notSupportedOnPlatform')}</div>
    case 'maintenance':
      return <><UpdateSection /><IntegrationsSection /></>
  }
}

export default function SettingsPage(): React.JSX.Element {
  const { t } = useTranslation()
  const [active, setActive] = useState<SettingsGroupKey>('appearance')
  const [supportsLenovoHardware, setSupportsLenovoHardware] = useState(true)
  const [pageLoading, setPageLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [scopesReady, setScopesReady] = useState(false)
  const [reloadToken, setReloadToken] = useState(0)
  const contentRef = useRef<HTMLElement | null>(null)

  useEffect(() => {
    contentRef.current?.scrollTo({ top: 0 })
  }, [active])

  const retry = useCallback(() => {
    setLoadError(null)
    setScopesReady(false)
    setPageLoading(true)
    setReloadToken((value) => value + 1)
  }, [])

  useEffect(() => {
    let cancelled = false
    const loadingId = useLoadingStore.getState().start(
      t('loading.settings', { defaultValue: 'Loading settings…' }),
      { canCancel: false, silent: true }
    )

    const loadPage = async (): Promise<void> => {
      const featuresPromise = featuresApi.list()
      await useSettingsStore.getState().load()
      if (cancelled) return

      let nextSupportsLenovoHardware = true
      try {
        const infos = await featuresPromise
        if (cancelled) return
        nextSupportsLenovoHardware = infos.some(
          (info) => info.supported && LENOVO_FEATURE_KEYS.includes(info.key)
        )
      } catch {
        // Keep the default (all groups visible) when the probe fails.
      }
      if (cancelled) return

      setSupportsLenovoHardware(nextSupportsLenovoHardware)
      setScopesReady(true)
      setLoadError(null)
      setPageLoading(false)
    }

    loadPage()
      .catch((reason: unknown) => {
        if (cancelled) return
        const raw = sanitizeBridgeError(reason)
        setScopesReady(false)
        setLoadError(
          isHostUnavailableError(raw)
            ? t('home.hostUnavailable', {
                defaultValue:
                  'The backend host is not running. Wait a moment and retry, or restart the app.'
              })
            : raw || t('settings.loadFailed', { defaultValue: 'Failed to load settings' })
        )
        setPageLoading(false)
      })
      .finally(() => {
        useLoadingStore.getState().finish(loadingId)
      })
    return () => {
      cancelled = true
      useLoadingStore.getState().finish(loadingId)
    }
  }, [reloadToken, t])

  const visibleGroups = useMemo(
    () => supportsLenovoHardware ? GROUPS : GROUPS.filter((group) => group.key !== 'device'),
    [supportsLenovoHardware]
  )

  useEffect(() => {
    if (!visibleGroups.some((group) => group.key === active)) setActive('appearance')
  }, [active, visibleGroups])

  const activeGroup = visibleGroups.find((group) => group.key === active) ?? visibleGroups[0]
  const editorsReady = !pageLoading && loadError == null && scopesReady

  return (
    <div className="udt-settings-page">
      <header className="udt-settings-page__header">
        <h1 className="udt-settings-page__title">{t('settings.title')}</h1>
        <p className="udt-settings-page__description">{t('settings.description')}</p>
      </header>
      <div className="udt-settings-page__surface udt-settings-page__surface--groups">
        <nav className="udt-settings-page__nav" aria-label={t('settings.title')}>
          <ul className="udt-settings-page__nav-list">
            {visibleGroups.map((group) => {
              const isActive = group.key === active
              return (
                <li key={group.key}>
                  <button
                    type="button"
                    className={`udt-settings-nav-item${isActive ? ' udt-settings-nav-item--active' : ''}`}
                    aria-current={isActive ? 'true' : undefined}
                    onClick={() => setActive(group.key)}
                  >
                    <span className="udt-settings-nav-item__accent" />
                    <span className="udt-settings-nav-item__icon">{group.icon}</span>
                    <span className="udt-settings-nav-item__label">{t(group.labelKey)}</span>
                  </button>
                </li>
              )
            })}
          </ul>
        </nav>
        <section
          ref={contentRef}
          key={editorsReady ? active : 'settings-pending'}
          className="udt-settings-page__content udt-settings-page__content-anim"
          aria-label={t(activeGroup.labelKey)}
        >
          <header className="udt-settings-page__section-header">
            <h2 className="udt-settings-page__section-title">{t(activeGroup.labelKey)}</h2>
          </header>
          {pageLoading ? (
            <SettingsSectionSkeleton section={activeGroup.skeleton} />
          ) : loadError != null || !scopesReady ? (
            <SettingsLoadError message={loadError} onRetry={retry} />
          ) : (
            renderGroup(active, supportsLenovoHardware, t)
          )}
        </section>
      </div>
    </div>
  )
}
