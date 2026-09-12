import { useEffect, useState } from 'react'
import { Modal, Select, Spin, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { featuresApi } from '../../api/features'
import { settingsApi } from '../../api/settings'
import { useSettingsStore } from '../../stores/settingsStore'
import { SettingsLoadError } from './SettingsLoadError'

/**
 * Parity modal for Electron Windows/Settings/WindowsPowerModesWindow: choose the
 * Windows power mode (Best power efficiency / Balanced / Best performance)
 * applied for each device power mode (Quiet / Balance / Performance / Custom).
 * The God Mode card is only shown when the power-mode feature reports it.
 */

interface PowerModesModalProps {
  open: boolean
  onClose: () => void
}

const WINDOWS_POWER_MODES = ['BestPowerEfficiency', 'Balanced', 'BestPerformance'] as const

type WindowsPowerMode = (typeof WINDOWS_POWER_MODES)[number]

const DEVICE_MODE_STATES = ['Quiet', 'Balance', 'Performance', 'GodMode'] as const

type DeviceModeState = (typeof DEVICE_MODE_STATES)[number]

function powerModeLabelKey(state: string): string {
  return `powerModeState${state.charAt(0).toUpperCase()}${state.slice(1)}`
}

function windowsPowerModeLabelKey(mode: string): string {
  return `windowsPowerMode${mode}`
}

export default function PowerModesModal({ open, onClose }: PowerModesModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const [powerModes, setPowerModes] = useState<Record<string, string>>({})
  const [availableStates, setAvailableStates] = useState<string[]>([])
  const [modesReady, setModesReady] = useState(false)

  useEffect(() => {
    if (!open) return
    let cancelled = false
    setLoading(true)
    setLoadError(null)
    setModesReady(false)
    void (async () => {
      try {
        const [statesResult, settingsResult] = await Promise.all([
          featuresApi.getStates('powerMode'),
          settingsApi.get('application')
        ])
        if (cancelled) return

        const store = (settingsResult.value ?? {}) as Record<string, unknown>
        const storedModes = (store.PowerModes ?? {}) as Record<string, string>
        const states = (statesResult.states ?? [])
          .filter((state): state is string => typeof state === 'string')
        setAvailableStates(states)
        setPowerModes({
          Quiet: storedModes.Quiet ?? 'Balanced',
          Balance: storedModes.Balance ?? 'Balanced',
          Performance: storedModes.Performance ?? 'Balanced',
          GodMode: storedModes.GodMode ?? 'Balanced'
        })
        setModesReady(true)
      } catch (reason) {
        if (cancelled) return
        setModesReady(false)
        setLoadError(reason instanceof Error ? reason.message : String(reason))
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [open, reloadToken])

  const handleChange = async (state: DeviceModeState, value: WindowsPowerMode): Promise<void> => {
    if (!modesReady) return
    const next = { ...powerModes, [state]: value }
    setPowerModes(next)
    try {
      const result = await settingsApi.get('application')
      const current = (result.value ?? {}) as Record<string, unknown>
      const merged = {
        ...current,
        PowerModes: { ...((current.PowerModes as Record<string, string> | undefined) ?? {}), ...next }
      }
      useSettingsStore.getState().setScope('application', merged)
      await settingsApi.set('application', merged)
      await settingsApi.save(['application'])
    } catch (reason) {
      void message.error((reason as Error).message)
      setPowerModes(powerModes)
    }
  }

  const godModeAvailable = availableStates.includes('GodMode')

  return (
    <Modal
      centered
      open={open}
      title={t('wpf.windowsPowerModesWindowtitle')}
      width={600}
      footer={null}
      onCancel={onClose}
    >
      {loading ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : loadError != null || !modesReady ? (
        <SettingsLoadError
          message={loadError}
          onRetry={() => setReloadToken((value) => value + 1)}
        />
      ) : (
        <div>
          {DEVICE_MODE_STATES.filter(
            (state) => state !== 'GodMode' || godModeAvailable
          ).map((state) => (
            <div
              key={state}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: 16,
                padding: '12px 0',
                borderBottom: '1px solid rgba(128,128,128,0.15)'
              }}
            >
              <span style={{ fontWeight: 600 }}>{t(powerModeLabelKey(state))}</span>
              <Select<WindowsPowerMode>
                className="udt-settings-select"
                style={{ minWidth: 200 }}
                value={powerModes[state] as WindowsPowerMode | undefined}
                onChange={(value) => void handleChange(state, value)}
                options={WINDOWS_POWER_MODES.map((mode) => ({
                  value: mode,
                  label: t(windowsPowerModeLabelKey(mode))
                }))}
              />
            </div>
          ))}
        </div>
      )}
    </Modal>
  )
}
