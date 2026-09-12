import './bridge/initWebBridge'
import React, { useEffect, useState } from 'react'
import ReactDOM from 'react-dom/client'
import { HashRouter } from 'react-router-dom'
import { ConfigProvider, message, theme } from 'antd'
import i18n from './i18n'
import { getAntDesignLocale, loadAntDesignLocale } from './i18n/antdLocale'
import logger from './utils/logger'
import { initNotifications } from './notifications'
import { initCrashReportListener } from './notifications/crashListener'
import {
  onCultureSynchronized,
  registerHostCultureRetry,
  syncCultureToHost
} from './api/localization'
import { initHostCapabilitiesSync } from './stores/hostCapabilitiesStore'
import { initSettingsSync, useSettingsStore } from './stores/settingsStore'
import { useOptimizationStore } from './stores/optimizationStore'
import { useThemeStore } from './stores/themeStore'
import { useTheme } from './theme/useTheme'
import { bootstrapThemeDocument } from './theme/bootstrapTheme'
import App from './App'
import './styles/global.css'
import './styles/skeleton.css'

bootstrapThemeDocument()

// Platform adaptation: [data-platform] on <html> drives platform-specific CSS
// (font stack, scrollbars). bridge.platform is process.platform from the main
// process ('darwin' on macOS). Browser `dev:web` sets platform 'web' via the
// shim; if no bridge is present, do not pretend we are Windows.
const RUNTIME_PLATFORM = window.bridge?.platform ?? 'web'
document.documentElement.dataset.platform = RUNTIME_PLATFORM

logger.info('renderer starting', { platform: window.bridge?.platform ?? 'unknown' })
void initNotifications()
void initCrashReportListener()

// Electron SnackbarHelper parity: transient action feedback lives at the bottom
// edge (see .ant-message override in global.css), distinct from the corner
// notification stack (AppNotificationHost).
message.config({ maxCount: 3 })

function getUiDirection(language: string): 'ltr' | 'rtl' {
  const normalized = language.toLowerCase()
  return normalized === 'ar' || normalized.startsWith('ar-') ? 'rtl' : 'ltr'
}

function applyDocumentDirection(language: string): void {
  document.documentElement.dir = getUiDirection(language)
}

applyDocumentDirection(i18n.language)

/** Apply the saved AnimationsEnabled flag to <html> (drives window/dialog CSS). */
function applyAnimationsSetting(application?: unknown): void {
  const enabled =
    typeof application === 'object' &&
    application !== null &&
    (application as Record<string, unknown>)['AnimationsEnabled'] !== false
  document.documentElement.classList.toggle('udt-animations-off', !enabled)
}

function Root(): React.JSX.Element {
  // Keep theme mode + accent synced for the whole app (not only Settings).
  useTheme()
  const themeMode = useThemeStore((s) => s.themeMode)
  const themePreference = useThemeStore((s) => s.themePreference)
  const stylePreference = useThemeStore((s) => s.stylePreference)
  const colorPrimary = useThemeStore((s) => s.colorPrimary)
  const [locale, setLocale] = useState(() => getAntDesignLocale(i18n.language))
  const [direction, setDirection] = useState(() => getUiDirection(i18n.language))

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', themeMode)
    document.documentElement.style.colorScheme = themeMode
    // Keep DWM backdrop materials (mica/acrylic) matching the in-app theme —
    // otherwise a dark app on a light system shows a washed-out white backdrop.
    // Only pin nativeTheme while the user forces Light/Dark: a pinned
    // themeSource makes prefers-color-scheme report the override instead of
    // the real OS theme, so "follow system" must reset it to 'system' for
    // matchMedia (bootstrapTheme / useTheme) to resolve the actual OS mode
    // and for the OS change listener to keep firing.
    window.bridge?.setThemeSource?.(themePreference === 'system' ? 'system' : themeMode)
  }, [themeMode, themePreference])

  // 保持 <html data-style> 与风格偏好同步（default 也显式写出，供 CSS 按 [data-style] 分支）。
  useEffect(() => {
    document.documentElement.setAttribute('data-style', stylePreference)
  }, [stylePreference])

  useEffect(() => {
    const root = document.documentElement
    if (colorPrimary) {
      root.style.setProperty('--udt-accent', colorPrimary)
      // Electron --udt-accent-secondary is the accent at 90% opacity.
      root.style.setProperty('--udt-accent-secondary', `color-mix(in srgb, ${colorPrimary} 90%, transparent)`)
    } else {
      root.style.removeProperty('--udt-accent')
      root.style.removeProperty('--udt-accent-secondary')
    }
  }, [colorPrimary])

  useEffect(() => {
    let cancelled = false
    const applyLanguage = (lng: string): void => {
      setDirection(getUiDirection(lng))
      applyDocumentDirection(lng)
      void loadAntDesignLocale(lng).then((next) => {
        if (!cancelled) setLocale(next)
      })
    }
    applyLanguage(i18n.language)
    const handleLanguageChanged = (lng: string): void => {
      applyLanguage(lng)
    }
    i18n.on('languageChanged', handleLanguageChanged)
    return () => {
      cancelled = true
      i18n.off('languageChanged', handleLanguageChanged)
    }
  }, [])

  useEffect(() => initSettingsSync(), [])

  useEffect(() => initHostCapabilitiesSync(), [])

  useEffect(() => {
    const offHostReady = registerHostCultureRetry(() => i18n.resolvedLanguage ?? i18n.language)
    const offCultureSynchronized = onCultureSynchronized(() => {
      const optimizationState = useOptimizationStore.getState()
      if (optimizationState.categories.length > 0 || optimizationState.loading)
        void optimizationState.refresh()
    })

    void syncCultureToHost(i18n.resolvedLanguage ?? i18n.language)

    return () => {
      offHostReady()
      offCultureSynchronized()
    }
  }, [])

  useEffect(() => {
    // Apply saved AnimationsEnabled on startup and keep it in sync when the
    // store updates (settings.changed via initSettingsSync, or a Settings toggle).
    let disposed = false
    const applyFromScopes = (): void => {
      if (!disposed) applyAnimationsSetting(useSettingsStore.getState().scopes.application)
    }
    const unsubscribe = useSettingsStore.subscribe(applyFromScopes)
    void useSettingsStore
      .getState()
      .load(['application'])
      .then(applyFromScopes)
      .catch((error: unknown) => {
        if (!disposed) logger.warn('failed to refresh animation settings', error)
      })
    return () => {
      disposed = true
      unsubscribe()
    }
  }, [])

  return (
    <ConfigProvider
      locale={locale}
      direction={direction}
      theme={{
        algorithm: themeMode === 'dark' ? theme.darkAlgorithm : theme.defaultAlgorithm,
        cssVar: { key: 'udt' },
        token: {
          borderRadius: 8,
          borderRadiusLG: 18,
          borderRadiusSM: 8,
          controlHeight: 32,
          fontSize: 13,
          ...(themeMode === 'dark'
            ? {
                colorBgLayout: '#202020',
                colorBgContainer: '#303030',
                colorBorderSecondary: 'rgba(255,255,255,0.08)'
              }
            : {}),
          ...(colorPrimary ? { colorPrimary } : {})
        },
        components: {
          Select: {
            borderRadiusLG: 12
          },
          Cascader: {
            borderRadiusLG: 12
          },
          DatePicker: {
            borderRadiusLG: 12
          },
          Dropdown: {
            borderRadiusLG: 12,
            borderRadiusSM: 8
          },
          Mentions: {
            borderRadiusLG: 12
          }
        }
      }}
    >
      <HashRouter>
        <App />
      </HashRouter>
    </ConfigProvider>
  )
}

ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
  <React.StrictMode>
    <Root />
  </React.StrictMode>
)
