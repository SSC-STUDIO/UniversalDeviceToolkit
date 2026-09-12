import { Select } from 'antd'
import type { TFunction } from 'i18next'
import { useEffect,useState } from 'react'
import { useTranslation } from 'react-i18next'
import { localizeHostError } from '../../../../shared/bridge/bridge'
import { subscribeUiVisibility } from '../../../../shared/format/uiVisibility'
import { notify } from '../../../../shared/notifications'
import { SkeletonCard } from '../../../../shared/ui/Skeleton'
import {
PlayCircle24Regular,
Stop24Regular
} from '../../../../shared/ui/icons/fluent'
import { type NetworkAccelerationConfig,type NetworkAccelerationMode } from '../../network/api/network'
import { NetworkPanels } from '../../network/components/NetworkPanels'
import { useNetworkStore } from '../../network/stores/networkStore'
import '../../system/components/optimization.css'
import {
NETWORK_ACCELERATION_MODES,
getNetworkSelectedTargetCount,
presentActionNotification,
resolveActionError,
runExclusivePoll
} from '../../system/optimizationPresentation'

function reportStoreError(t: TFunction, fallbackKey: string, error: string | null | undefined): void {
  const fallback = t(fallbackKey)
  const localized = localizeHostError(resolveActionError(error, fallback), t)
  const notif = presentActionNotification(localized, fallback)
  notify({
    title: notif.title,
    message: notif.message ?? '',
    severity: 'Error'
  })
}

const NETWORK_MODE_I18N_KEYS: Record<NetworkAccelerationMode, string> = {
  Off: 'optimization.network.modes.off',
  SystemProxy: 'optimization.network.modes.systemProxy',
  Hosts: 'optimization.network.modes.hosts',
  DiagnosticsOnly: 'optimization.network.modes.diagnosticsOnly'
}

export default function NetworkTab(): React.JSX.Element {
  const { t } = useTranslation()
  const networkStatus = useNetworkStore((s) => s.networkStatus)
  const networkError = useNetworkStore((s) => s.error)
  const saveNetworkConfig = useNetworkStore((s) => s.saveNetworkConfig)
  const startNetwork = useNetworkStore((s) => s.startNetwork)
  const stopNetwork = useNetworkStore((s) => s.stopNetwork)
  const loadTraffic = useNetworkStore((s) => s.loadTraffic)
  const loadRuntime = useNetworkStore((s) => s.loadRuntime)
  const [config, setConfig] = useState<NetworkAccelerationConfig | null>(null)
  const [saving, setSaving] = useState(false)
  const [starting, setStarting] = useState(false)
  const [stopping, setStopping] = useState(false)

  const isRunning = networkStatus?.isRunning === true

  useEffect(() => {
    if (!isRunning) return
    const trafficInFlight = { current: false }
    const runtimeInFlight = { current: false }
    let trafficTimer: ReturnType<typeof setInterval> | undefined
    let runtimeTimer: ReturnType<typeof setInterval> | undefined
    const startPolls = (): void => {
      if (trafficTimer != null) return
      trafficTimer = setInterval(() => {
        void runExclusivePoll(trafficInFlight, loadTraffic)
      }, 1000)
      runtimeTimer = setInterval(() => {
        void runExclusivePoll(runtimeInFlight, loadRuntime)
      }, 2000)
    }
    const stopPolls = (): void => {
      if (trafficTimer != null) clearInterval(trafficTimer)
      if (runtimeTimer != null) clearInterval(runtimeTimer)
      trafficTimer = undefined
      runtimeTimer = undefined
    }
    if (!document.hidden) startPolls()
    const unsubscribeVisibility = subscribeUiVisibility((active) => {
      if (active) startPolls()
      else stopPolls()
    })
    return () => {
      unsubscribeVisibility()
      stopPolls()
    }
  }, [isRunning, loadTraffic, loadRuntime])

  const editableConfig = config ?? networkStatus?.config ?? null

  const ensureConfig = (): NetworkAccelerationConfig | null => {
    if (config) return config
    if (!networkStatus) return null
    const next: NetworkAccelerationConfig = {
      ...networkStatus.config,
      domainGroups: [...networkStatus.config.domainGroups]
    }
    setConfig(next)
    return next
  }

  const handleSave = async (): Promise<void> => {
    const current = ensureConfig()
    if (!current) return
    setSaving(true)
    try {
      const ok = await saveNetworkConfig(current)
      if (!ok) {
        reportStoreError(t, 'optimization.network.saveFailed', useNetworkStore.getState().error)
      }
    } finally {
      setSaving(false)
    }
  }

  const handleStart = async (): Promise<void> => {
    setStarting(true)
    try {
      const current = ensureConfig()
      if (current) {
        const saved = await saveNetworkConfig(current)
        if (!saved) {
          reportStoreError(t, 'optimization.network.saveFailed', useNetworkStore.getState().error)
          return
        }
      }
      const ok = await startNetwork()
      if (!ok) {
        reportStoreError(t, 'optimization.network.startFailed', useNetworkStore.getState().error)
      }
    } finally {
      setStarting(false)
    }
  }

  const handleStop = async (): Promise<void> => {
    setStopping(true)
    try {
      const ok = await stopNetwork()
      if (!ok) {
        reportStoreError(t, 'optimization.network.stopFailed', useNetworkStore.getState().error)
      }
    } finally {
      setStopping(false)
    }
  }

  if (!networkStatus || !editableConfig) {
    if (networkError) return <></>
    return <SkeletonCard lines={3} withIcon />
  }

  const updateConfig = (patch: Partial<NetworkAccelerationConfig>): void => {
    const current = ensureConfig()
    if (!current) return
    setConfig({ ...current, ...patch })
  }

  const selectedTargets = getNetworkSelectedTargetCount(editableConfig)

  return (
    <div className="udt-network-layout">
      <div className="udt-card udt-card--row">
        <span
          className={`udt-status-dot${networkStatus.isRunning ? ' udt-status-dot--on' : ''}`}
        />
        <div className="udt-card__copy">
          <div className="udt-card__title">
            {networkStatus.isRunning ? t('optimization.network.running') : t('optimization.network.stopped')}
          </div>
          <div className="udt-card__desc">{networkStatus.statusText}</div>
        </div>
        <div className="udt-card__desc">
          {networkStatus.isBackendReady
            ? t('optimization.network.backendReady')
            : t('optimization.network.backendNotReady')}
        </div>
        <div className="udt-card__desc">
          {t('optimization.network.targetsLabel')}: {selectedTargets}
        </div>
        <div className="udt-card__desc">
          {t('optimization.network.portLabel')}: {editableConfig.listenPort}
        </div>
      </div>

      <div className="udt-card udt-network-config">
        <div className="udt-card__title">{t('optimization.network.config')}</div>
        <div className="udt-network-config__fields">
          <div className="udt-network-field udt-network-field--switch">
            <span className="udt-network-field__label">{t('optimization.network.accelerationEnabled')}</span>
            <label className="udt-switch">
              <input
                type="checkbox"
                checked={editableConfig.accelerationEnabled}
                onChange={(e) => updateConfig({ accelerationEnabled: e.target.checked })}
              />
              <span className="udt-switch__track" />
            </label>
          </div>
          <div className="udt-network-field">
            <span className="udt-network-field__label">{t('optimization.network.mode')}</span>
            <Select<NetworkAccelerationMode>
              aria-label={t('optimization.network.mode')}
              className="udt-network-select"
              popupMatchSelectWidth={false}
              value={editableConfig.mode}
              onChange={(mode) => updateConfig({ mode })}
              options={NETWORK_ACCELERATION_MODES.map((mode) => ({
                value: mode,
                label: t(NETWORK_MODE_I18N_KEYS[mode])
              }))}
            />
          </div>
        </div>
        <div className="udt-network-config__actions">
          <button type="button" className="udt-btn udt-btn--primary" disabled={saving} onClick={() => void handleSave()}>
            {t('optimization.network.save')}
          </button>
          <button
            type="button"
            className="udt-btn udt-btn--secondary"
            disabled={
              !networkStatus.isBackendReady ||
              editableConfig.mode === 'Hosts' ||
              starting
            }
            onClick={() => void handleStart()}
          >
            <PlayCircle24Regular /> {t('optimization.network.start')}
          </button>
          <button
            type="button"
            className="udt-btn udt-btn--danger"
            disabled={stopping}
            onClick={() => void handleStop()}
          >
            <Stop24Regular /> {t('optimization.network.stop')}
          </button>
        </div>
      </div>

      <NetworkPanels />
    </div>
  )
}

