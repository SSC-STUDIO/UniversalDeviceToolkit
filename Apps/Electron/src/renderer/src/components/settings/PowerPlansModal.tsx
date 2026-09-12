import { useEffect, useState } from 'react'
import { Alert, Modal, Select, Spin, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { featuresApi } from '../../api/features'
import { settingsApi } from '../../api/settings'
import { DEFAULT_POWER_PLAN_GUID, powerPlansApi, type WindowsPowerPlan } from '../../api/powerSettings'
import { useSettingsStore } from '../../stores/settingsStore'
import { SettingsLoadError } from './SettingsLoadError'

/**
 * Parity modal for Electron Windows/Settings/WindowsPowerPlansWindow: choose the
 * Windows power plan applied for each device power mode. The first entry is
 * always the "Default" plan (empty GUID), followed by the plans reported by
 * the host, sorted by name. The God Mode card is only shown when the
 * power-mode feature reports it.
 */

interface PowerPlansModalProps {
  open: boolean
  onClose: () => void
}

const DEVICE_MODE_STATES = ['Quiet', 'Balance', 'Performance', 'GodMode'] as const

type DeviceModeState = (typeof DEVICE_MODE_STATES)[number]

function powerModeLabelKey(state: string): string {
  return `powerModeState${state.charAt(0).toUpperCase()}${state.slice(1)}`
}

export default function PowerPlansModal({ open, onClose }: PowerPlansModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const [plans, setPlans] = useState<WindowsPowerPlan[]>([])
  const [powerPlans, setPowerPlans] = useState<Record<string, string>>({})
  const [availableStates, setAvailableStates] = useState<string[]>([])
  const [plansUnavailable, setPlansUnavailable] = useState(false)
  const [plansReady, setPlansReady] = useState(false)

  useEffect(() => {
    if (!open) return
    let cancelled = false
    setLoading(true)
    setLoadError(null)
    setPlansReady(false)
    setPlansUnavailable(false)
    void (async () => {
      try {
        const [statesResult, settingsResult] = await Promise.all([
          featuresApi.getStates('powerMode'),
          settingsApi.get('application')
        ])
        if (cancelled) return

        const store = (settingsResult.value ?? {}) as Record<string, unknown>
        const storedPlans = (store.PowerPlans ?? {}) as Record<string, string>
        const states = (statesResult.states ?? []).filter(
          (state): state is string => typeof state === 'string'
        )

        const defaultPlan: WindowsPowerPlan = {
          guid: DEFAULT_POWER_PLAN_GUID,
          name: t('wpf.windowsPowerPlansWindowdefaultPowerPlan'),
          isActive: false
        }

        let availablePlans: WindowsPowerPlan[] = []
        try {
          const result = await powerPlansApi.getList()
          availablePlans = result.plans ?? []
        } catch {
          setPlansUnavailable(true)
        }
        availablePlans.sort((a, b) => a.name.localeCompare(b.name))

        setAvailableStates(states)
        setPlans([defaultPlan, ...availablePlans])
        setPowerPlans({
          Quiet: storedPlans.Quiet ?? DEFAULT_POWER_PLAN_GUID,
          Balance: storedPlans.Balance ?? DEFAULT_POWER_PLAN_GUID,
          Performance: storedPlans.Performance ?? DEFAULT_POWER_PLAN_GUID,
          GodMode: storedPlans.GodMode ?? DEFAULT_POWER_PLAN_GUID
        })
        setPlansReady(true)
      } catch (reason) {
        if (cancelled) return
        setPlansReady(false)
        setLoadError(reason instanceof Error ? reason.message : String(reason))
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [open, reloadToken, t])

  const handleChange = async (state: DeviceModeState, guid: string): Promise<void> => {
    if (!plansReady) return
    const next = { ...powerPlans, [state]: guid }
    setPowerPlans(next)
    try {
      const result = await settingsApi.get('application')
      const current = (result.value ?? {}) as Record<string, unknown>
      const merged = {
        ...current,
        PowerPlans: { ...((current.PowerPlans as Record<string, string> | undefined) ?? {}), ...next }
      }
      useSettingsStore.getState().setScope('application', merged)
      await settingsApi.set('application', merged)
      await settingsApi.save(['application'])

      // Electron WindowsPowerPlansWindow: activating the plan for the *current*
      // power mode applies it right away (EnsureCorrectWindowsPowerSettings).
      if (guid !== DEFAULT_POWER_PLAN_GUID) {
        try {
          const powerState = await featuresApi.getState('powerMode')
          if (String(powerState.state) === state) {
            await powerPlansApi.setActive(guid)
          }
        } catch {
          // activation is best-effort; the mapping is already persisted
        }
      }
    } catch (reason) {
      void message.error((reason as Error).message)
      setPowerPlans(powerPlans)
    }
  }

  const godModeAvailable = availableStates.includes('GodMode')

  return (
    <Modal
      centered
      open={open}
      title={t('wpf.windowsPowerPlansWindowtitle')}
      width={600}
      footer={null}
      onCancel={onClose}
    >
      {loading ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : loadError != null || !plansReady ? (
        <SettingsLoadError
          message={loadError}
          onRetry={() => setReloadToken((value) => value + 1)}
        />
      ) : (
        <div>
          {plansUnavailable && (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              message={t('wpf.powerPlansWindowloadError')}
            />
          )}
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
              <Select<string>
                className="udt-settings-select"
                style={{ minWidth: 240 }}
                value={powerPlans[state]}
                onChange={(value) => void handleChange(state, value)}
                options={plans.map((plan) => ({
                  value: plan.guid,
                  label: plan.name
                }))}
              />
            </div>
          ))}
        </div>
      )}
    </Modal>
  )
}
