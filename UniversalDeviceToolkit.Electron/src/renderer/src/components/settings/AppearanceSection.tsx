import { useEffect, useState } from 'react'
import { Checkbox, Select, message } from 'antd'
import { useTranslation } from 'react-i18next'
import ColorPicker from '../ColorPicker'
import { settingsApi } from '../../api/settings'
import { systemApi } from '../../api/system'
import { LANGUAGES, changeLanguage } from '../../i18n'
import { useSettingsStore } from '../../stores/settingsStore'
import {
  UI_SCALE_AUTO,
  UI_SCALE_OPTIONS,
  useThemeStore,
  type StylePreference,
  type UiScalePreference
} from '../../stores/themeStore'
import { storeAccentPreference } from '../../theme/useTheme'
import { FONT_PRESETS, applyAppFont, getStoredAppFont } from '../../utils/fonts'
import { SettingsCard } from './SettingsCard'

type AppSettings = Record<string, unknown>

type ThemePreference = 'System' | 'Light' | 'Dark'
type AccentColorSource = 'System' | 'Custom'

/**
 * Temperature unit preference for the sensor dashboard.
 *
 * The value is persisted both to the backend 'application' scope (kept for the
 * host-side consumers such as the OSD / status tray) and to localStorage
 * 'udt-temperature-unit' so the renderer can read it synchronously. Sensor
 * sections should use getTemperatureUnit() below to format values.
 */
export type TemperatureUnit = 'C' | 'F'

const TEMPERATURE_UNIT_STORAGE_KEY = 'udt-temperature-unit'

/** Returns the current temperature unit ('C' or 'F'). */
export function getTemperatureUnit(): TemperatureUnit {
  try {
    return localStorage.getItem(TEMPERATURE_UNIT_STORAGE_KEY) === 'F' ? 'F' : 'C'
  } catch {
    return 'C'
  }
}

interface AccentColorRGB {
  R: number
  G: number
  B: number
}

/** Fallback when Windows accent cannot be read (AccentColorPresets.Swatches[0]). */
const DEFAULT_SYSTEM_ACCENT_HEX = '#0078d4'

/** All selectable languages; the current one is sorted to the top. */
const LANGUAGE_OPTIONS = LANGUAGES.map((language) => ({
  value: language.code,
  label: language.name
}))

const THEME_OPTIONS: { value: ThemePreference; labelKey: string; previewClass: string }[] = [
  { value: 'Light', labelKey: 'settings.appearance.themeOptions.light', previewClass: 'udt-theme-option--light' },
  { value: 'Dark', labelKey: 'settings.appearance.themeOptions.dark', previewClass: 'udt-theme-option--dark' },
  { value: 'System', labelKey: 'settings.appearance.themeOptions.system', previewClass: 'udt-theme-option--system' }
]

const STYLE_OPTIONS: { value: StylePreference; labelKey: string; previewClass: string }[] = [
  { value: 'default', labelKey: 'settings.appearance.styleOptions.default', previewClass: 'udt-style-option--default' },
  { value: 'focus', labelKey: 'settings.appearance.styleOptions.focus', previewClass: 'udt-style-option--focus' },
  { value: 'neubrutalism', labelKey: 'settings.appearance.styleOptions.neubrutalism', previewClass: 'udt-style-option--neubrutalism' }
]

const TEMPERATURE_UNIT_OPTIONS: { value: TemperatureUnit; label: string }[] = [
  { value: 'C', label: '°C' },
  { value: 'F', label: '°F' }
]

function uiScaleOptions(autoLabel: string): { value: UiScalePreference; label: string }[] {
  return [
    { value: UI_SCALE_AUTO, label: autoLabel },
    ...UI_SCALE_OPTIONS.map((value) => ({
      value,
      label: `${Math.round(value * 100)}%`
    }))
  ]
}

/**
 * Solid accent presets from Lib Theme/AccentColorPresets.cs (system rainbow is separate).
 */
const ACCENT_PRESETS: { hex: string; key: string }[] = [
  { hex: '#0078d4', key: 'Blue' },
  { hex: '#b146c2', key: 'Purple' },
  { hex: '#e3008c', key: 'Pink' },
  { hex: '#e81123', key: 'Red' },
  { hex: '#f7630c', key: 'Orange' },
  { hex: '#ffb900', key: 'Amber' },
  { hex: '#107c10', key: 'Green' },
  { hex: '#808080', key: 'Gray' }
]

const SYSTEM_ACCENT_GRADIENT =
  'linear-gradient(135deg, #f13b50 0%, #742ac4 28%, #1a98f2 52%, #06d3a5 76%, #ffd62e 100%)'

function readString(app: AppSettings, key: string): string | undefined {
  const value = app[key]
  return typeof value === 'string' ? value : undefined
}

function readBool(app: AppSettings, key: string, fallback: boolean): boolean {
  const value = app[key]
  return typeof value === 'boolean' ? value : fallback
}

function readThemePreference(app: AppSettings): ThemePreference {
  const value = readString(app, 'Theme')
  return value === 'Light' || value === 'Dark' ? value : 'System'
}

function readAccentColorSource(app: AppSettings): AccentColorSource {
  return readString(app, 'AccentColorSource') === 'Custom' ? 'Custom' : 'System'
}

function readAccentColor(app: AppSettings): AccentColorRGB | undefined {
  const value = app['AccentColor']
  if (
    value != null &&
    typeof value === 'object' &&
    typeof (value as AccentColorRGB).R === 'number' &&
    typeof (value as AccentColorRGB).G === 'number' &&
    typeof (value as AccentColorRGB).B === 'number'
  ) {
    return value as AccentColorRGB
  }
  return undefined
}

function accentColorToHex(color: AccentColorRGB): string {
  const toHex = (value: number): string => value.toString(16).padStart(2, '0')
  return `#${toHex(color.R)}${toHex(color.G)}${toHex(color.B)}`
}

function hexToAccentColor(hex: string): AccentColorRGB {
  const normalized = hex.replace('#', '')
  const value = Number.parseInt(normalized, 16)
  return { R: (value >> 16) & 0xff, G: (value >> 8) & 0xff, B: value & 0xff }
}

function colorsEqual(a: AccentColorRGB | undefined, hex: string): boolean {
  if (a == null) return false
  return accentColorToHex(a).toLowerCase() === hex.toLowerCase()
}

function isPresetHex(hex: string): boolean {
  return ACCENT_PRESETS.some((preset) => preset.hex.toLowerCase() === hex.toLowerCase())
}

function EyedropperIcon(): React.JSX.Element {
  return (
    <svg
      className="udt-settings-swatch__eyedropper-icon"
      viewBox="0 0 24 24"
      width="18"
      height="18"
      aria-hidden="true"
      focusable="false"
    >
      <path
        fill="currentColor"
        d="M11.1 16.2 7.8 19.5a2.1 2.1 0 0 1-3-3l3.3-3.3 3 3Zm8.2-12.5a2.2 2.2 0 0 0-3.1 0l-1.5 1.5 3.1 3.1 1.5-1.5a2.2 2.2 0 0 0 0-3.1Zm-4.6 2.5L6.2 16.7l1.1 1.1 8.5-8.5-1.1-1.1Z"
      />
    </svg>
  )
}

function ThemePreviewWindowButtons(): React.JSX.Element {
  return (
    <span className="udt-theme-option__win" aria-hidden="true">
      <span className="udt-theme-option__win-btn udt-theme-option__win-btn--min" />
      <span className="udt-theme-option__win-btn udt-theme-option__win-btn--max" />
      <span className="udt-theme-option__win-btn udt-theme-option__win-btn--close" />
    </span>
  )
}

function ThemePreviewBar(): React.JSX.Element {
  return (
    <span className="udt-theme-option__bar" aria-hidden="true">
      <span className="udt-theme-option__app-badge">
        <span className="udt-theme-option__app-icon" />
        <span className="udt-theme-option__app-title" />
      </span>
      <ThemePreviewWindowButtons />
    </span>
  )
}

function ThemePreviewNavItem({ active = false }: { active?: boolean }): React.JSX.Element {
  return (
    <span
      className={`udt-theme-option__nav-item${active ? ' udt-theme-option__nav-item--active' : ''}`}
      aria-hidden="true"
    >
      {active ? <span className="udt-theme-option__nav-accent" /> : null}
      <span className="udt-theme-option__nav-icon" />
      <span className="udt-theme-option__nav-label" />
    </span>
  )
}

function ThemePreviewNav(): React.JSX.Element {
  return (
    <span className="udt-theme-option__sidebar" aria-hidden="true">
      <span className="udt-theme-option__nav-group">
        <ThemePreviewNavItem active />
        <ThemePreviewNavItem />
        <ThemePreviewNavItem />
      </span>
      <span className="udt-theme-option__nav-group udt-theme-option__nav-group--footer">
        <ThemePreviewNavItem />
      </span>
    </span>
  )
}

function ThemePreviewCardItem({
  controlType
}: {
  controlType: 'switch-on' | 'switch-off' | 'button'
}): React.JSX.Element {
  return (
    <span className="udt-theme-option__card-item" aria-hidden="true">
      <span className="udt-theme-option__card-icon" />
      <span className="udt-theme-option__card-text">
        <span className="udt-theme-option__line udt-theme-option__line--title" />
        <span className="udt-theme-option__line udt-theme-option__line--desc" />
      </span>
      <span
        className={`udt-theme-option__card-control udt-theme-option__card-control--${controlType}`}
      />
    </span>
  )
}

function ThemePreviewContent(): React.JSX.Element {
  return (
    <span className="udt-theme-option__content" aria-hidden="true">
      <span className="udt-theme-option__header-line" />
      <span className="udt-theme-option__card-list">
        <ThemePreviewCardItem controlType="switch-on" />
        <ThemePreviewCardItem controlType="switch-off" />
        <ThemePreviewCardItem controlType="button" />
      </span>
    </span>
  )
}

function ThemePreviewMockup({ mode }: { mode: 'light' | 'dark' }): React.JSX.Element {
  return (
    <span
      className={`udt-theme-option__mockup udt-theme-option__mockup--${mode}`}
      aria-hidden="true"
    >
      <ThemePreviewBar />
      <span className="udt-theme-option__body">
        <ThemePreviewNav />
        <ThemePreviewContent />
      </span>
    </span>
  )
}

function ThemePreviewCard({
  option,
  selected,
  label,
  disabled,
  onClick
}: {
  option: (typeof THEME_OPTIONS)[number]
  selected: boolean
  label: string
  disabled: boolean
  onClick: () => void
}): React.JSX.Element {
  const isSystem = option.value === 'System'
  const mode: 'light' | 'dark' = option.value === 'Light' ? 'light' : 'dark'

  return (
    <button
      type="button"
      className={`udt-theme-option ${option.previewClass}${selected ? ' udt-theme-option--selected' : ''}`}
      disabled={disabled}
      onClick={onClick}
      aria-pressed={selected}
    >
      <span className="udt-theme-option__preview">
        {isSystem ? (
          <span className="udt-theme-option__system-wrapper">
            <ThemePreviewMockup mode="light" />
            <span className="udt-theme-option__system-clip">
              <ThemePreviewMockup mode="dark" />
            </span>
            <span className="udt-theme-option__system-line" />
          </span>
        ) : (
          <ThemePreviewMockup mode={mode} />
        )}
      </span>
      <span className="udt-theme-option__label-container">
        <span className="udt-theme-option__label">{label}</span>
      </span>
    </button>
  )
}

function StylePreviewBar(): React.JSX.Element {
  return (
    <span className="udt-style-option__bar" aria-hidden="true">
      <span className="udt-style-option__dot" />
      <span className="udt-style-option__bar-line" />
    </span>
  )
}

function StylePreviewRow({ wide = false }: { wide?: boolean }): React.JSX.Element {
  return (
    <span className="udt-style-option__row" aria-hidden="true">
      <span className="udt-style-option__row-dot" />
      <span
        className={`udt-style-option__row-line${wide ? ' udt-style-option__row-line--wide' : ''}`}
      />
    </span>
  )
}

function StylePreviewContent(): React.JSX.Element {
  return (
    <span className="udt-style-option__content" aria-hidden="true">
      <StylePreviewRow wide />
      <StylePreviewRow />
      <StylePreviewRow />
    </span>
  )
}

/** Mini window mockup; the style-specific colors live entirely in the CSS classes. */
function StylePreviewMockup({ variant }: { variant: StylePreference }): React.JSX.Element {
  return (
    <span
      className={`udt-style-option__mockup udt-style-option__mockup--${variant}`}
      aria-hidden="true"
    >
      <StylePreviewBar />
      <StylePreviewContent />
    </span>
  )
}

function StylePreviewCard({
  option,
  selected,
  label,
  disabled,
  onClick
}: {
  option: (typeof STYLE_OPTIONS)[number]
  selected: boolean
  label: string
  disabled: boolean
  onClick: () => void
}): React.JSX.Element {
  return (
    <button
      type="button"
      className={`udt-style-option ${option.previewClass}${selected ? ' udt-style-option--selected' : ''}`}
      disabled={disabled}
      onClick={onClick}
      aria-pressed={selected}
    >
      <span className="udt-style-option__preview">
        <StylePreviewMockup variant={option.value} />
      </span>
      <span className="udt-style-option__label">{label}</span>
    </button>
  )
}

export default function AppearanceSection(): React.JSX.Element {
  const { t, i18n } = useTranslation()
  const setThemePreference = useThemeStore((s) => s.setThemePreference)
  const setAccent = useThemeStore((s) => s.setAccent)
  const setAccentTintsSurfaces = useThemeStore((s) => s.setAccentTintsSurfaces)
  const uiScalePreference = useThemeStore((s) => s.uiScalePreference)
  const setUiScalePreference = useThemeStore((s) => s.setUiScalePreference)
  const stylePreference = useThemeStore((s) => s.stylePreference)
  const setStylePreference = useThemeStore((s) => s.setStylePreference)
  const scopes = useSettingsStore((s) => s.scopes)
  const load = useSettingsStore((s) => s.load)
  const setScope = useSettingsStore((s) => s.setScope)
  const [systemAccentHex, setSystemAccentHex] = useState(DEFAULT_SYSTEM_ACCENT_HEX)

  const rawApp = scopes.application
  const editorsEnabled = typeof rawApp === 'object' && rawApp !== null
  const app: AppSettings = editorsEnabled ? (rawApp as AppSettings) : {}

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    let cancelled = false
    systemApi
      .getAccentColor()
      .then((color) => {
        if (!cancelled) {
          setSystemAccentHex(accentColorToHex({ R: color.r, G: color.g, B: color.b }))
        }
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [])

  // Keep localStorage 'udt-temperature-unit' in sync with the backend value so
  // getTemperatureUnit() reflects the persisted preference from the first run.
  useEffect(() => {
    const backendUnit = app['TemperatureUnit']
    if (backendUnit === 'C' || backendUnit === 'F') {
      localStorage.setItem(TEMPERATURE_UNIT_STORAGE_KEY, backendUnit)
    }
  }, [app['TemperatureUnit']])

  const accentSource = readAccentColorSource(app)
  const accentColor = readAccentColor(app)
  const accentHex = accentColor ? accentColorToHex(accentColor) : undefined
  const applyAccentToSystem = readBool(app, 'ApplyAccentColorToSystem', true)
  const applyAccentToTheme = readBool(app, 'ApplyAccentColorToTheme', true)
  const customPickerSelected =
    accentSource === 'Custom' && accentHex != null && !isPresetHex(accentHex)

  const currentLanguage = LANGUAGE_OPTIONS.some((option) => option.value === i18n.language)
    ? i18n.language
    : 'en'
  const languageOptions = [...LANGUAGE_OPTIONS].sort((a, b) => {
    if (a.value === currentLanguage) return -1
    if (b.value === currentLanguage) return 1
    return a.label.localeCompare(b.label, undefined, { sensitivity: 'base' })
  })

  const [selectedFont, setSelectedFont] = useState<string>(() => getStoredAppFont())

  useEffect(() => {
    const backendFont = app['FontFamily']
    if (typeof backendFont === 'string' && backendFont.trim() !== '') {
      setSelectedFont(backendFont)
      applyAppFont(backendFont)
    }
  }, [app['FontFamily']])

  const persistApplication = (next: AppSettings): void => {
    if (!editorsEnabled) return
    setScope('application', next)
    settingsApi
      .set('application', next)
      .then(() => settingsApi.save(['application']))
      .catch(() => message.error(t('settings.saveFailed')))
  }

  const handleFontChange = (fontValue: string): void => {
    setSelectedFont(fontValue)
    applyAppFont(fontValue)
    const next: AppSettings = { ...app, FontFamily: fontValue }
    persistApplication(next)
  }

  /**
   * Push the resolved accent into the theme store immediately.
   * Matches Electron ThemeManager.SetColor: the accent always applies to
   * --udt-accent / Ant Design colorPrimary. ApplyAccentColorToTheme only
   * controls the tinted surface palette (ThemeStylePreset), not the accent.
   */
  const applyAccentToUi = (next: AppSettings): void => {
    const source = readAccentColorSource(next)
    if (source === 'System') {
      setAccent(systemAccentHex)
      return
    }
    const custom = readAccentColor(next)
    setAccent(custom ? accentColorToHex(custom) : systemAccentHex)
  }

  /** When the theme-style checkbox is on, picking an accent resets to Default (Electron). */
  const themeStylePresetForAccentPick = (): string | undefined =>
    applyAccentToTheme ? 'Default' : undefined

  const applyAccentToWindowsIfEnabled = (rgb: AccentColorRGB, enabled: boolean): void => {
    if (!enabled) return
    void systemApi.setAccentColor({ r: rgb.R, g: rgb.G, b: rgb.B }).catch(() => undefined)
  }

  const handleLanguageChange = (value: string): void => {
    void changeLanguage(value)
  }

  const handleTemperatureUnitChange = (value: TemperatureUnit): void => {
    localStorage.setItem(TEMPERATURE_UNIT_STORAGE_KEY, value)
    persistApplication({ ...app, TemperatureUnit: value })
  }

  const handleThemeChange = (value: ThemePreference): void => {
    // Persist the renderer-side choice and update the store — useTheme watches
    // themePreference and re-runs, (re)attaching the OS light/dark listener
    // when "follow system" is selected so switching the OS theme updates the
    // app immediately.
    setThemePreference(value === 'System' ? 'system' : value === 'Dark' ? 'dark' : 'light')
    persistApplication({ ...app, Theme: value })
  }

  const handleApplyAccentToSystemChange = (checked: boolean): void => {
    const next: AppSettings = { ...app, ApplyAccentColorToSystem: checked }
    persistApplication(next)
    if (checked && accentSource === 'Custom' && accentColor != null) {
      applyAccentToWindowsIfEnabled(accentColor, true)
    }
  }

  const handleApplyAccentToThemeChange = (checked: boolean): void => {
    // Palette tint gate only - do not clear/reapply the accent itself (Electron parity).
    const next: AppSettings = { ...app, ApplyAccentColorToTheme: checked }
    if (checked && accentSource === 'Custom') {
      next.ThemeStylePreset = 'Default'
    }
    persistApplication(next)
    // The palette effect in useTheme retints surfaces for the current mode.
    setAccentTintsSurfaces(checked)
  }

  const handleSystemAccent = (): void => {
    const next: AppSettings = {
      ...app,
      AccentColorSource: 'System',
      ...(themeStylePresetForAccentPick() != null
        ? { ThemeStylePreset: themeStylePresetForAccentPick() }
        : {})
    }
    persistApplication(next)
    storeAccentPreference('System')
    applyAccentToUi(next)
  }

  const handleAccentPreset = (hex: string): void => {
    const rgb = hexToAccentColor(hex)
    const next: AppSettings = {
      ...app,
      AccentColorSource: 'Custom',
      AccentColor: rgb,
      ...(themeStylePresetForAccentPick() != null
        ? { ThemeStylePreset: themeStylePresetForAccentPick() }
        : {})
    }
    persistApplication(next)
    storeAccentPreference('Custom', hex)
    applyAccentToUi(next)
    applyAccentToWindowsIfEnabled(rgb, applyAccentToSystem)
  }

  const handleCustomAccent = (hex: string): void => {
    const rgb = hexToAccentColor(hex)
    const next: AppSettings = {
      ...app,
      AccentColorSource: 'Custom',
      AccentColor: rgb,
      ...(themeStylePresetForAccentPick() != null
        ? { ThemeStylePreset: themeStylePresetForAccentPick() }
        : {})
    }
    persistApplication(next)
    storeAccentPreference('Custom', hex)
    applyAccentToUi(next)
    applyAccentToWindowsIfEnabled(rgb, applyAccentToSystem)
  }

  const handleUiScaleChange = (value: UiScalePreference): void => {
    setUiScalePreference(value)
  }

  const selectedTheme = readThemePreference(app)
  const selectedTemperatureUnit: TemperatureUnit =
    app['TemperatureUnit'] === 'F' || app['TemperatureUnit'] === 'C'
      ? app['TemperatureUnit']
      : getTemperatureUnit()
  const customPickerValue = accentHex ?? systemAccentHex

  return (
    <div className="udt-settings-section udt-settings-section--appearance">
      <SettingsCard
        title={t('settings.appearance.language')}
        description={t('settings.appearance.languageDesc')}
        action={
          <Select<string>
            className="udt-settings-select udt-settings-select--language"
            value={currentLanguage}
            options={languageOptions}
            disabled={!editorsEnabled}
            onChange={handleLanguageChange}
          />
        }
      />
      <SettingsCard
        title={t('settings.appearance.temperature')}
        description={t('settings.appearance.temperatureDesc')}
        action={
          <Select<TemperatureUnit>
            className="udt-settings-select"
            value={selectedTemperatureUnit}
            options={TEMPERATURE_UNIT_OPTIONS}
            disabled={!editorsEnabled}
            onChange={handleTemperatureUnitChange}
          />
        }
      />
      <SettingsCard title={t('wpf.settingsPagethemeModetitle', { defaultValue: t('settings.appearance.theme') })}>
        <div className="udt-theme-options">
          {THEME_OPTIONS.map((option) => (
            <ThemePreviewCard
              key={option.value}
              option={option}
              selected={selectedTheme === option.value}
              label={t(option.labelKey)}
              disabled={!editorsEnabled}
              onClick={() => handleThemeChange(option.value)}
            />
          ))}
        </div>

        <div className="udt-theme-accent-options">
          <Checkbox
            checked={applyAccentToSystem}
            disabled={!editorsEnabled}
            onChange={(event) => handleApplyAccentToSystemChange(event.target.checked)}
          >
            {t('wpf.settingsPageapplyAccentColorToThemetitle')}
          </Checkbox>
          <Checkbox
            checked={applyAccentToTheme}
            disabled={!editorsEnabled}
            onChange={(event) => handleApplyAccentToThemeChange(event.target.checked)}
          >
            {t('wpf.settingsPageapplyAccentColorToThemeStyletitle')}
          </Checkbox>
        </div>

        <div className="udt-theme-accent-divider" role="separator" />

        <div className="udt-theme-accent-title">
          {t('wpf.settingsPageaccentColorPresetstitle', {
            defaultValue: t('settings.appearance.accentColor')
          })}
        </div>
        <div className="udt-settings-swatches">
          <button
            type="button"
            className={`udt-settings-swatch${accentSource === 'System' ? ' udt-settings-swatch--selected' : ''}`}
            style={{ background: SYSTEM_ACCENT_GRADIENT }}
            disabled={!editorsEnabled}
            aria-label={t('settings.appearance.accentColorSource.system', {
              defaultValue: 'System'
            })}
            title={t('settings.appearance.accentColorSource.system', { defaultValue: 'System' })}
            onClick={handleSystemAccent}
          />
          {ACCENT_PRESETS.map((preset) => {
            const selected =
              accentSource === 'Custom' && colorsEqual(accentColor, preset.hex)
            return (
              <button
                key={preset.key}
                type="button"
                className={`udt-settings-swatch${selected ? ' udt-settings-swatch--selected' : ''}`}
                style={{ background: preset.hex }}
                disabled={!editorsEnabled}
                aria-label={preset.key}
                title={preset.key}
                onClick={() => handleAccentPreset(preset.hex)}
              />
            )
          })}
          <div
            className={`udt-settings-swatch-picker${
              customPickerSelected ? ' udt-settings-swatch-picker--selected' : ''
            }`}
          >
            <ColorPicker
              value={customPickerValue}
              size={40}
              disabled={!editorsEnabled}
              tooltip={t('settings.appearance.accentColorSource.custom', {
                defaultValue: 'Custom'
              })}
              onChangeDelayed={handleCustomAccent}
            >
              <EyedropperIcon />
            </ColorPicker>
          </div>
        </div>
      </SettingsCard>
      <SettingsCard title={t('settings.appearance.style')} description={t('settings.appearance.styleDesc')}>
        <div className="udt-style-options">
          {STYLE_OPTIONS.map((option) => (
            <StylePreviewCard
              key={option.value}
              option={option}
              selected={stylePreference === option.value}
              label={t(option.labelKey)}
              disabled={!editorsEnabled}
              onClick={() => setStylePreference(option.value)}
            />
          ))}
        </div>
      </SettingsCard>
      <SettingsCard
        title={t('settings.appearance.appScale')}
        description={t('settings.appearance.appScaleDesc')}
        action={
          <Select<UiScalePreference>
            className="udt-settings-select udt-settings-select--scale"
            value={uiScalePreference}
            options={uiScaleOptions(
              t('settings.appearance.appScaleAuto', { defaultValue: 'Auto' })
            )}
            disabled={!editorsEnabled}
            onChange={handleUiScaleChange}
          />
        }
      />
      <SettingsCard
        title={t('settings.appearance.font', { defaultValue: 'Interface Font (界面字体)' })}
        description={t('settings.appearance.fontDesc', { defaultValue: 'Choose a font family for the interface typography' })}
        action={
          <Select<string>
            className="udt-settings-select udt-settings-select--font"
            value={selectedFont}
            options={FONT_PRESETS.map((preset) => ({
              value: preset.value,
              label: t(preset.labelKey, { defaultValue: preset.defaultLabel })
            }))}
            disabled={!editorsEnabled}
            onChange={handleFontChange}
          />
        }
      />
    </div>
  )
}
