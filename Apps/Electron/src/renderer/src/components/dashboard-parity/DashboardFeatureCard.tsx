import { memo, useEffect, useMemo, useState } from 'react'
import { Select, Switch, Tooltip, message } from 'antd'
import { Settings24Regular } from '../icons/fluent'
import type { TFunction } from 'i18next'
import {
  BatteryCharge24Regular,
  DesktopPulse24Regular,
  Gauge24Regular,
  Hdr24Regular,
  Keyboard24Regular,
  LeafOne24Regular,
  LightbulbCircle24Regular,
  Mic24Regular,
  PlugDisconnected24Regular,
  Power24Regular,
  ScaleFill24Regular,
  Tablet24Regular,
  TextFontSize24Regular,
  TopSpeed24Regular,
  UsbPlug24Regular,
  UsbStick24Regular,
  WeatherMoon24Regular
} from '@fluentui/react-icons'
import { useTranslation } from 'react-i18next'
import type { FeatureKey } from '../../api/features'
import { featuresApi } from '../../api/features'
import { systemApi } from '../../api/system'
import { useFeature } from '../../hooks/useFeature'
import { powerModeColor } from '../../utils/powerMode'
import BalanceModeSettingsModal from './BalanceModeSettingsModal'
import GodModeSettingsModal from './GodModeSettingsModal'

const TOGGLE_FEATURES = new Set<FeatureKey>([
  'batteryNightCharge',
  'flipToStart',
  'fnLock',
  'gSync',
  'hdr',
  'microphone',
  'overDrive',
  'panelLogo',
  'portsBacklight',
  'touchpadLock',
  'winKey',
  'oneLevelWhiteKeyboard'
])

const HIDE_SINGLE_OPTION_FEATURES = new Set<FeatureKey>(['resolution', 'refreshRate', 'dpiScale'])

function FeatureIcon({ feature, color }: { feature: FeatureKey; color?: string }): React.JSX.Element {
  const icon = (() => {
    switch (feature) {
      case 'powerMode':
      case 'itsMode':
        return <Gauge24Regular />
      case 'battery':
        return <BatteryCharge24Regular />
      case 'batteryNightCharge':
        return <WeatherMoon24Regular />
      case 'alwaysOnUsb':
        return <UsbStick24Regular />
      case 'instantBoot':
        return <PlugDisconnected24Regular />
      case 'flipToStart':
        return <Power24Regular />
      case 'hybridMode':
      case 'igpuMode':
        return <LeafOne24Regular />
      case 'resolution':
        return <ScaleFill24Regular />
      case 'refreshRate':
        return <DesktopPulse24Regular />
      case 'dpiScale':
        return <TextFontSize24Regular />
      case 'hdr':
        return <Hdr24Regular />
      case 'overDrive':
        return <TopSpeed24Regular />
      case 'panelLogo':
        return <LightbulbCircle24Regular />
      case 'portsBacklight':
        return <UsbPlug24Regular />
      case 'microphone':
        return <Mic24Regular />
      case 'touchpadLock':
        return <Tablet24Regular />
      case 'fnLock':
      case 'whiteKeyboard':
      case 'oneLevelWhiteKeyboard':
      case 'winKey':
        return <Keyboard24Regular />
      default:
        return <Gauge24Regular />
    }
  })()

  return (
    <span
      className="udt-parity-feature-card__icon"
      aria-hidden="true"
      style={color !== undefined ? { color } : undefined}
    >
      {icon}
    </span>
  )
}

function stateKey(value: string): string {
  return value.charAt(0).toLowerCase() + value.slice(1)
}

function labelForStringState(feature: FeatureKey, value: string, t: TFunction): string {
  if (feature === 'powerMode') {
    return t(`feature.powerModeOptions.${stateKey(value)}`, { defaultValue: value })
  }
  if (feature === 'battery') {
    return t(`feature.batteryModes.${stateKey(value)}`, { defaultValue: value })
  }
  return t(`dashboardFeatureState.${stateKey(value)}`, { defaultValue: value })
}

function wireKey(value: unknown): string {
  return typeof value === 'string' ? value : JSON.stringify(value)
}

function labelForState(feature: FeatureKey, value: unknown, t: TFunction): string {
  if (typeof value === 'string') return labelForStringState(feature, value, t)
  if (value == null || typeof value !== 'object') return String(value ?? '')

  const record = value as Record<string, unknown>
  if (typeof record.Width === 'number' && typeof record.Height === 'number') {
    return `${record.Width} x ${record.Height}`
  }
  if (typeof record.Frequency === 'number') return `${record.Frequency} Hz`
  if (typeof record.Scale === 'number') return `${record.Scale}%`
  return JSON.stringify(value)
}

function isOnState(value: unknown): boolean {
  return typeof value === 'string' && value.toLowerCase() === 'on'
}

function DashboardFeatureCard({ feature }: { feature: FeatureKey }): React.JSX.Element | null {
  const { t } = useTranslation()
  const { supported, state, states, loading, error, setState, refresh } = useFeature(feature)
  const [settingsModal, setSettingsModal] = useState<'balance' | 'godMode' | null>(null)
  const [hdrBlocked, setHdrBlocked] = useState(false)
  const [hdrStatusUnknown, setHdrStatusUnknown] = useState(false)
  const [powerAdapterDisconnected, setPowerAdapterDisconnected] = useState(false)
  const [adapterStatusUnknown, setAdapterStatusUnknown] = useState(false)

  // HDRControl.OnRefreshAsync: while Windows settings block HDR the toggle is
  // disabled and the card shows the HDRControl_Warning.
  useEffect(() => {
    if (!supported || feature !== 'hdr') return
    let cancelled = false
    featuresApi
      .isHdrBlocked()
      .then((result) => {
        if (!cancelled) {
          setHdrBlocked(result.blocked === true)
          setHdrStatusUnknown(false)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setHdrStatusUnknown(true)
          setHdrBlocked(true)
        }
      })
    return () => {
      cancelled = true
    }
  }, [supported, feature])

  // PowerModeControl.OnRefreshAsync: warn when the selected Performance/GodMode
  // state cannot work without a connected AC adapter.
  useEffect(() => {
    if (!supported || feature !== 'powerMode') return
    if (state !== 'Performance' && state !== 'GodMode') {
      setPowerAdapterDisconnected(false)
      setAdapterStatusUnknown(false)
      return
    }
    let cancelled = false
    systemApi
      .powerAdapterStatus()
      .then((result) => {
        if (!cancelled) {
          setPowerAdapterDisconnected(result.status === 'Disconnected')
          setAdapterStatusUnknown(false)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setAdapterStatusUnknown(true)
          setPowerAdapterDisconnected(false)
        }
      })
    return () => {
      cancelled = true
    }
  }, [supported, feature, state])

  const selectOptions = useMemo(
    () =>
      states.map((value) => ({
        value: wireKey(value),
        label: labelForState(feature, value, t),
        wireValue: value
      })),
    [feature, states, t]
  )

  if (!supported) return null
  if (HIDE_SINGLE_OPTION_FEATURES.has(feature) && states.length < 2) return null

  const title = t(`feature.${feature}`, { defaultValue: feature })
  const description = t(`feature.${feature}.desc`, { defaultValue: '' })
  const isToggle = TOGGLE_FEATURES.has(feature)
  const notSupportedReason = t('dashboard.card.notSupported', {
    defaultValue: 'Not supported on this device'
  })

  const applyState = (next: unknown): void => {
    void setState(next).then((ok) => {
      if (!ok) {
        void message.error(t('dashboard.card.changeFailed', {
          defaultValue: 'Failed to change setting.'
        }))
      }
    })
  }

  // Electron HDRControl_Warning / PowerModeControl_Warning.
  const warning =
    feature === 'hdr' && hdrStatusUnknown
      ? t('feature.hdr.statusUnknown', {
          defaultValue: 'Could not verify HDR status. The toggle is disabled until status is known.'
        })
      : feature === 'hdr' && hdrBlocked
        ? t('feature.hdr.warning', { defaultValue: 'HDR usage is blocked by Windows settings.' })
        : feature === 'powerMode' && adapterStatusUnknown
          ? t('feature.powerMode.adapterStatusUnknown', {
              defaultValue: 'Could not verify power adapter status.'
            })
          : feature === 'powerMode' && powerAdapterDisconnected
            ? t('feature.powerMode.warning')
            : ''

  // Electron PowerModeControl.ConfigButton: Balance → AI engine settings;
  // Performance/GodMode → Custom Mode settings (when the machine supports God Mode).
  const powerState = feature === 'powerMode' && typeof state === 'string' ? state : undefined
  const showConfigButton =
    supported &&
    feature === 'powerMode' &&
    (powerState === 'Balance' ||
      ((powerState === 'Performance' || powerState === 'GodMode') && states.includes('GodMode')))

  const refreshPowerMode = (): void => {
    void refresh()
  }

  return (
    <>
      <article className={`udt-parity-feature-card${supported ? '' : ' udt-parity-feature-card--disabled'}`}>
        <div className="udt-parity-feature-card__body">
          <FeatureIcon
            feature={feature}
            color={feature === 'powerMode' && typeof state === 'string' ? powerModeColor(state) : undefined}
          />
          <div className="udt-parity-feature-card__copy">
            <div className="udt-parity-feature-card__title" title={title}>{title}</div>
            {description !== '' && (
              <div className="udt-parity-feature-card__description" title={description}>{description}</div>
            )}
            {!supported && (
              <div className="udt-parity-feature-card__warning" title={notSupportedReason}>
                {notSupportedReason}
              </div>
            )}
            {error != null && <div className="udt-parity-feature-card__warning" title={error}>{error}</div>}
            {warning !== '' && <div className="udt-parity-feature-card__warning" title={warning}>{warning}</div>}
          </div>
          <div className="udt-parity-feature-card__accessory">
            {isToggle ? (
              <Switch
                aria-label={title}
                checked={isOnState(state)}
                disabled={!supported || loading || error != null || (feature === 'hdr' && hdrBlocked)}
                loading={loading}
                onChange={(checked) => applyState(checked ? 'On' : 'Off')}
              />
            ) : (
              <Select
                aria-label={title}
                className="udt-parity-feature-card__select"
                disabled={!supported || loading || error != null || states.length === 0}
                loading={loading}
                value={state == null ? undefined : wireKey(state)}
                options={selectOptions}
                optionRender={(option) => option.label}
                onChange={(key) => {
                  const next = states.find((candidate) => wireKey(candidate) === key)
                  if (next !== undefined) applyState(next)
                }}
              />
            )}
            {showConfigButton && (
              <Tooltip title={t('dashboard.card.config')}>
                <button
                  type="button"
                  className="udt-parity-feature-card__config-btn"
                  aria-label={t('dashboard.card.config')}
                  onClick={() =>
                    setSettingsModal(powerState === 'Balance' ? 'balance' : 'godMode')
                  }
                >
                  <Settings24Regular />
                </button>
              </Tooltip>
            )}
          </div>
        </div>
      </article>
      {settingsModal === 'balance' && (
        <BalanceModeSettingsModal
          open
          onClose={() => setSettingsModal(null)}
          onSaved={refreshPowerMode}
        />
      )}
      {settingsModal === 'godMode' && (
        <GodModeSettingsModal
          open
          onClose={() => setSettingsModal(null)}
          onSaved={refreshPowerMode}
        />
      )}
    </>
  )
}

export default memo(DashboardFeatureCard)
