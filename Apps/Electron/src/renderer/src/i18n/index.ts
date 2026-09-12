import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { syncCultureToHost } from '../api/localization'
import enUS from './locales/en-US'
import { dashboardParityEnUS, dashboardParityZhCN } from './locales/dashboard-parity'
export interface LanguageOption {
  code: string
  /** Native name shown in the language picker. */
  name: string
}

/**
 * Selectable languages (25) — mirrors the resx set. Locale files are loaded
 * ON DEMAND (dynamic import) so the main entry chunk stays small and the
 * renderer does not hold all 25 translations in memory; only the active
 * language plus the English fallback are ever bundled/loaded.
 */
export const LANGUAGES: LanguageOption[] = [
  { code: 'en', name: 'English' },
  { code: 'zh-CN', name: '简体中文' },
  { code: 'zh-Hant', name: '繁體中文' },
  { code: 'ja', name: '日本語' },
  { code: 'de', name: 'Deutsch' },
  { code: 'fr', name: 'Français' },
  { code: 'es', name: 'Español' },
  { code: 'it', name: 'Italiano' },
  { code: 'pt-BR', name: 'Português (Brasil)' },
  { code: 'pt', name: 'Português' },
  { code: 'ru', name: 'Русский' },
  { code: 'uk', name: 'Українська' },
  { code: 'pl', name: 'Polski' },
  { code: 'cs', name: 'Čeština' },
  { code: 'sk', name: 'Slovenčina' },
  { code: 'hu', name: 'Magyar' },
  { code: 'ro', name: 'Română' },
  { code: 'bg', name: 'Български' },
  { code: 'tr', name: 'Türkçe' },
  { code: 'el', name: 'Ελληνικά' },
  { code: 'ar', name: 'العربية' },
  { code: 'lv', name: 'Latviešu' },
  { code: 'nl-NL', name: 'Nederlands' },
  { code: 'vi', name: 'Tiếng Việt' },
  { code: 'uz-Latn-UZ', name: "O'zbek" }
]

export const supportedLanguages: string[] = LANGUAGES.map((language) => language.code)
export type SupportedLanguage = (typeof supportedLanguages)[number]
export type LanguageDirection = 'rtl' | 'ltr'

const DEFAULT_LANGUAGE: SupportedLanguage = 'zh-CN'
export const LANGUAGE_STORAGE_KEY = 'udt-language'
export const LEGACY_LANGUAGE_STORAGE_KEY = 'udt.lang'

/** Selectable RTL cultures. Keep in sync with LANGUAGES (currently Arabic only). */
const RTL_LANGUAGE_CODES = new Set(['ar'])

type LocaleBundle = { translation: Record<string, unknown> }

/**
 * Vite emits one async chunk per locale file. The glob MUST include the `.ts`
 * extension — `import(\`./locales/${lng}\`)` is dropped in production builds
 * (dynamic-import-vars requires a file extension), which left every non-English
 * UI on the English fallback while the settings dropdown still showed 简体中文.
 */
const localeModules = import.meta.glob<LocaleBundle>(
  ['./locales/*.ts', '!./locales/dashboard-parity.ts', '!./locales/en-US.ts'],
  {
    import: 'default'
  }
)

/**
 * Legacy language codes (persisted by older builds / the app) mapped onto the
 * current registry.
 */
const LEGACY_ALIASES: Record<string, string> = {
  'en-US': 'en',
  'nl-NL': 'nl-NL',
  pt: 'pt',
  ko: 'en',
  'ko-KR': 'en'
}

function normalizeLanguage(lng: string): string {
  const normalized = LEGACY_ALIASES[lng] ?? lng
  return supportedLanguages.includes(normalized as SupportedLanguage) ? normalized : 'en'
}

/**
 * True when `lng` (or its legacy alias) is a right-to-left UI language.
 * Exported so ConfigProvider / layout code can set direction without
 * duplicating the Arabic check. Does not write document.dir itself.
 */
export function isRtlLanguage(lng: string): boolean {
  const raw = (LEGACY_ALIASES[lng] ?? lng).toLowerCase()
  if (RTL_LANGUAGE_CODES.has(raw)) return true
  const dash = raw.indexOf('-')
  return dash > 0 && RTL_LANGUAGE_CODES.has(raw.slice(0, dash))
}

export function getLanguageDirection(lng: string): LanguageDirection {
  return isRtlLanguage(lng) ? 'rtl' : 'ltr'
}

function applyDocumentLanguage(lng: string): void {
  if (typeof document === 'undefined') return
  const normalized = normalizeLanguage(lng)
  document.documentElement.lang = normalized
  document.documentElement.dir = getLanguageDirection(normalized)
}

function persistLanguage(lng: string): void {
  try {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, lng)
    localStorage.setItem(LEGACY_LANGUAGE_STORAGE_KEY, lng)
  } catch {
    /* ignore quota / private mode */
  }
}

function resolveInitialLanguage(): string {
  let storedLanguage: string | null = null
  try {
    storedLanguage =
      localStorage.getItem(LANGUAGE_STORAGE_KEY) ?? localStorage.getItem(LEGACY_LANGUAGE_STORAGE_KEY)
  } catch {
    /* ignore quota / private mode */
  }

  if (storedLanguage != null) return normalizeLanguage(storedLanguage)

  // The installer is the first-run language picker. Persist its validated
  // choice immediately so a later launch is governed by Settings instead of
  // replaying setup state from the installed application directory.
  const installerLanguage = typeof window === 'undefined' ? undefined : window.bridge?.installerSelection?.language
  if (installerLanguage != null && supportedLanguages.includes(installerLanguage as SupportedLanguage)) {
    const normalized = normalizeLanguage(installerLanguage)
    persistLanguage(normalized)
    return normalized
  }

  return DEFAULT_LANGUAGE
}

function localeModulePath(lng: string): string {
  return `./locales/${lng}.ts`
}

/**
 * Load a locale module on demand and register it with i18next. Only English is
 * bundled statically (fallback); every other language is a separate chunk that
 * Vite code-splits out of the main entry.
 */
async function loadLocale(lng: string): Promise<void> {
  if (lng === 'en' || i18n.hasResourceBundle(lng, 'translation')) return
  const loader = localeModules[localeModulePath(lng)]
  if (loader == null) {
    console.warn(`[i18n] no locale module for '${lng}'`)
    return
  }
  try {
    const module = await loader()
    const bundle = { ...module.translation }
    if (lng === 'zh-CN') {
      Object.assign(bundle, dashboardParityZhCN)
    }
    i18n.addResourceBundle(lng, 'translation', bundle, true, true)
  } catch (error) {
    console.warn(`[i18n] failed to load locale '${lng}':`, error)
  }
}

void i18n.use(initReactI18next).init({
  resources: {
    // English is always bundled: canonical 'en' + legacy 'en-US' alias.
    'en-US': {
      translation: {
        ...enUS.translation,
        ...dashboardParityEnUS
      }
    },
    en: {
      translation: {
        ...enUS.translation,
        ...dashboardParityEnUS
      }
    }
  },
  lng: resolveInitialLanguage(),
  fallbackLng: 'en-US',
  interpolation: {
    escapeValue: false
  }
})

i18n.on('languageChanged', applyDocumentLanguage)
applyDocumentLanguage(resolveInitialLanguage())

// Load the initial language bundle before rendering (keeps first paint correct).
const initialLanguage = resolveInitialLanguage()
if (initialLanguage !== 'en') {
  // The bundle is registered after init — re-apply the language so i18next
  // re-resolves every key and React re-renders with the real translations
  // instead of the English fallback (which is what a bare init would show).
  void loadLocale(initialLanguage).then(() => {
    void i18n.changeLanguage(initialLanguage)
  })
}

export async function changeLanguage(lng: string): Promise<void> {
  const normalized = normalizeLanguage(lng)
  await loadLocale(normalized)
  await i18n.changeLanguage(normalized)
  persistLanguage(normalized)
  window.bridge?.setTrayLanguage?.(normalized)
  await syncCultureToHost(normalized)
}

// Keep the main-process tray menu in sync with the active UI language.
try {
  window.bridge?.setTrayLanguage?.(resolveInitialLanguage())
} catch {
  /* bridge may be unavailable during unit tests */
}

export default i18n
