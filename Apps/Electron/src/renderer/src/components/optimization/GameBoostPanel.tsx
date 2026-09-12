import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Dismiss24Regular,
  Flash24Filled,
  PlayCircle24Regular,
  Stop24Regular
} from '../icons/fluent'
import {
  gameBoostApi,
  type GameBoostConfig,
  type GameBoostStatus,
  DEFAULT_GAME_BOOST_CONFIG
} from '../../api/gameBoost'
import { notify } from '../../notifications'

export default function GameBoostPanel(): React.JSX.Element {
  const { t } = useTranslation()
  const [config, setConfig] = useState<GameBoostConfig>(DEFAULT_GAME_BOOST_CONFIG)
  const [status, setStatus] = useState<GameBoostStatus>({
    isBoosting: false,
    activeGameProcess: null,
    activeGameProcessId: null,
    boostedProcesses: [],
    suppressedProcessesCount: 0
  })
  const [saving, setSaving] = useState(false)
  const [acting, setActing] = useState(false)
  const [newGameProcess, setNewGameProcess] = useState('')
  const [newWhitelistApp, setNewWhitelistApp] = useState('')

  useEffect(() => {
    let unmounted = false

    gameBoostApi
      .getConfig()
      .then((cfg) => {
        if (!unmounted && cfg) setConfig(cfg)
      })
      .catch(() => undefined)

    gameBoostApi
      .getStatus()
      .then((st) => {
        if (!unmounted && st) setStatus(st)
      })
      .catch(() => undefined)

    const unsubscribe = gameBoostApi.onStatusChanged((st) => {
      if (!unmounted) setStatus(st)
    })

    return () => {
      unmounted = true
      unsubscribe()
    }
  }, [])

  const handleSave = async (updated: GameBoostConfig): Promise<void> => {
    setConfig(updated)
    setSaving(true)
    try {
      await gameBoostApi.saveConfig(updated)
    } catch (ex) {
      notify({
        title: t('optimization.gameBoost.saveFailed', { defaultValue: 'Failed to save Game Boost config' }),
        message: ex instanceof Error ? ex.message : String(ex),
        severity: 'Error'
      })
    } finally {
      setSaving(false)
    }
  }

  const handleBoostNow = async (): Promise<void> => {
    setActing(true)
    try {
      const res = await gameBoostApi.boostNow()
      setStatus(res.status)
      if (res.success) {
        notify({
          title: t('optimization.gameBoost.boostSuccess', { defaultValue: 'Game Boost applied' }),
          message: '',
          severity: 'Success'
        })
      } else {
        notify({
          title: t('optimization.gameBoost.noGameDetected', {
            defaultValue: 'No running game process found in foreground'
          }),
          message: '',
          severity: 'Warning'
        })
      }
    } catch (ex) {
      notify({
        title: t('optimization.gameBoost.boostFailed', { defaultValue: 'Failed to apply boost' }),
        message: ex instanceof Error ? ex.message : String(ex),
        severity: 'Error'
      })
    } finally {
      setActing(false)
    }
  }

  const handleRevertNow = async (): Promise<void> => {
    setActing(true)
    try {
      const res = await gameBoostApi.revertNow()
      setStatus(res.status)
      notify({
        title: t('optimization.gameBoost.revertSuccess', {
          defaultValue: 'Game Boost optimizations reverted'
        }),
        message: '',
        severity: 'Info'
      })
    } catch (ex) {
      notify({
        title: t('optimization.gameBoost.revertFailed', { defaultValue: 'Failed to revert boost' }),
        message: ex instanceof Error ? ex.message : String(ex),
        severity: 'Error'
      })
    } finally {
      setActing(false)
    }
  }

  const addCustomGame = (): void => {
    const trimmed = newGameProcess.trim().toLowerCase()
    if (!trimmed || config.customGameProcesses.includes(trimmed)) return
    const updated: GameBoostConfig = {
      ...config,
      customGameProcesses: [...config.customGameProcesses, trimmed]
    }
    setNewGameProcess('')
    void handleSave(updated)
  }

  const removeCustomGame = (name: string): void => {
    const updated: GameBoostConfig = {
      ...config,
      customGameProcesses: config.customGameProcesses.filter((item) => item !== name)
    }
    void handleSave(updated)
  }

  const addWhitelistApp = (): void => {
    const trimmed = newWhitelistApp.trim().toLowerCase()
    if (!trimmed || config.backgroundWhitelist.includes(trimmed)) return
    const updated: GameBoostConfig = {
      ...config,
      backgroundWhitelist: [...config.backgroundWhitelist, trimmed]
    }
    setNewWhitelistApp('')
    void handleSave(updated)
  }

  const removeWhitelistApp = (name: string): void => {
    const updated: GameBoostConfig = {
      ...config,
      backgroundWhitelist: config.backgroundWhitelist.filter((item) => item !== name)
    }
    void handleSave(updated)
  }

  return (
    <div className="udt-network-layout">
      {/* Live Status Card */}
      <div className="udt-card udt-card--row" style={{ alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <span
            className={`udt-status-dot${status.isBoosting ? ' udt-status-dot--on' : ''}`}
            style={{ width: '12px', height: '12px' }}
          />
          <div className="udt-card__copy">
            <div className="udt-card__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Flash24Filled style={{ color: status.isBoosting ? '#4CAF50' : '#888' }} />
              {status.isBoosting
                ? t('optimization.gameBoost.statusActive', { defaultValue: 'Game Boost Active' })
                : t('optimization.gameBoost.statusStandby', { defaultValue: 'Game Boost Standby' })}
            </div>
            <div className="udt-card__desc">
              {status.isBoosting && status.activeGameProcess
                ? `${t('optimization.gameBoost.activeGame', { defaultValue: 'Target Game' })}: ${status.activeGameProcess} (PID: ${status.activeGameProcessId})`
                : t('optimization.gameBoost.waitingForGame', {
                    defaultValue: 'Waiting for game process to enter foreground'
                  })}
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
          {status.isBoosting ? (
            <div className="udt-card__desc" style={{ color: '#4CAF50', fontWeight: 600 }}>
              {t('optimization.gameBoost.suppressedCount', {
                defaultValue: '{0} background apps throttled',
                count: status.suppressedProcessesCount
              }).replace('{0}', String(status.suppressedProcessesCount))}
            </div>
          ) : null}

          <button
            type="button"
            className="udt-btn udt-btn--primary"
            disabled={acting || saving}
            onClick={() => void handleBoostNow()}
          >
            <PlayCircle24Regular />
            {t('optimization.gameBoost.boostNow', { defaultValue: 'Boost Now' })}
          </button>
          <button
            type="button"
            className="udt-btn udt-btn--secondary"
            disabled={acting || saving || !status.isBoosting}
            onClick={() => void handleRevertNow()}
          >
            <Stop24Regular />
            {t('optimization.gameBoost.revertNow', { defaultValue: 'Revert' })}
          </button>
        </div>
      </div>

      {/* Boost Policy Settings */}
      <div className="udt-card udt-network-config">
        <div className="udt-card__title">
          {t('optimization.gameBoost.policyTitle', { defaultValue: 'Optimization Policies' })}
        </div>
        <div className="udt-network-config__fields" style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          <div className="udt-network-field udt-network-field--switch" style={{ width: '100%' }}>
            <div>
              <div className="udt-network-field__label">
                {t('optimization.gameBoost.autoGameBoost', {
                  defaultValue: 'Automatic Game Detection & Boost'
                })}
              </div>
              <div className="udt-card__desc">
                {t('optimization.gameBoost.autoGameBoostDesc', {
                  defaultValue:
                    'Automatically detect foreground game launches/focus and immediately apply scheduling optimizations.'
                })}
              </div>
            </div>
            <label className="udt-switch">
              <input
                type="checkbox"
                checked={config.autoGameBoost}
                onChange={(e) => void handleSave({ ...config, autoGameBoost: e.target.checked })}
              />
              <span className="udt-switch__track" />
            </label>
          </div>

          <div className="udt-network-field udt-network-field--switch" style={{ width: '100%' }}>
            <div>
              <div className="udt-network-field__label">
                {t('optimization.gameBoost.boostGamePriority', {
                  defaultValue: 'Elevate Game Process Priority (High)'
                })}
              </div>
              <div className="udt-card__desc">
                {t('optimization.gameBoost.boostGamePriorityDesc', {
                  defaultValue:
                    'Grants the foreground game thread priority over background desktop applications for smoother frame delivery.'
                })}
              </div>
            </div>
            <label className="udt-switch">
              <input
                type="checkbox"
                checked={config.boostGamePriority}
                onChange={(e) => void handleSave({ ...config, boostGamePriority: e.target.checked })}
              />
              <span className="udt-switch__track" />
            </label>
          </div>

          <div className="udt-network-field udt-network-field--switch" style={{ width: '100%' }}>
            <div>
              <div className="udt-network-field__label">
                {t('optimization.gameBoost.optimizeCpuAffinity', {
                  defaultValue: 'P-Core Affinity Optimization'
                })}
              </div>
              <div className="udt-card__desc">
                {t('optimization.gameBoost.optimizeCpuAffinityDesc', {
                  defaultValue:
                    'On hybrid CPU architectures (Intel 12th+ / AMD Zen 4+), prioritizes performance cores for maximum 1% low FPS.'
                })}
              </div>
            </div>
            <label className="udt-switch">
              <input
                type="checkbox"
                checked={config.optimizeCpuAffinity}
                onChange={(e) => void handleSave({ ...config, optimizeCpuAffinity: e.target.checked })}
              />
              <span className="udt-switch__track" />
            </label>
          </div>

          <div className="udt-network-field udt-network-field--switch" style={{ width: '100%' }}>
            <div>
              <div className="udt-network-field__label">
                {t('optimization.gameBoost.suppressBackgroundProcesses', {
                  defaultValue: 'EcoQoS Background Process Throttling'
                })}
              </div>
              <div className="udt-card__desc">
                {t('optimization.gameBoost.suppressBackgroundProcessesDesc', {
                  defaultValue:
                    'Applies Windows EcoQoS efficiency mode to background non-game processes to prevent CPU/memory bandwidth contention.'
                })}
              </div>
            </div>
            <label className="udt-switch">
              <input
                type="checkbox"
                checked={config.suppressBackgroundProcesses}
                onChange={(e) =>
                  void handleSave({ ...config, suppressBackgroundProcesses: e.target.checked })
                }
              />
              <span className="udt-switch__track" />
            </label>
          </div>

          <div className="udt-network-field udt-network-field--switch" style={{ width: '100%' }}>
            <div>
              <div className="udt-network-field__label">
                {t('optimization.gameBoost.muteNotifications', {
                  defaultValue: 'Mute Windows Notifications During Gaming'
                })}
              </div>
              <div className="udt-card__desc">
                {t('optimization.gameBoost.muteNotificationsDesc', {
                  defaultValue:
                    'Suppresses intrusive pop-ups and toast alerts while playing full-screen or windowed games.'
                })}
              </div>
            </div>
            <label className="udt-switch">
              <input
                type="checkbox"
                checked={config.muteNotifications}
                onChange={(e) => void handleSave({ ...config, muteNotifications: e.target.checked })}
              />
              <span className="udt-switch__track" />
            </label>
          </div>
        </div>
      </div>

      {/* Whitelist & Custom Games Management */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
        {/* Custom Games Card */}
        <div className="udt-card">
          <div className="udt-card__title">
            {t('optimization.gameBoost.customGamesTitle', { defaultValue: 'Custom Game Processes' })}
          </div>
          <div className="udt-card__desc" style={{ marginBottom: '12px' }}>
            {t('optimization.gameBoost.customGamesDesc', {
              defaultValue: 'Add custom executable names (e.g. game.exe) to recognize as games.'
            })}
          </div>

          <div style={{ display: 'flex', gap: '8px', marginBottom: '12px' }}>
            <input
              type="text"
              placeholder="e.g. game.exe"
              value={newGameProcess}
              onChange={(e) => setNewGameProcess(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && addCustomGame()}
              style={{
                flex: 1,
                padding: '6px 10px',
                borderRadius: '6px',
                border: '1px solid rgba(255,255,255,0.15)',
                background: 'rgba(0,0,0,0.2)',
                color: 'inherit'
              }}
            />
            <button type="button" className="udt-btn udt-btn--primary" onClick={addCustomGame}>
              {t('optimization.gameBoost.add', { defaultValue: 'Add' })}
            </button>
          </div>

          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px', maxHeight: '160px', overflowY: 'auto' }}>
            {config.customGameProcesses.length === 0 ? (
              <div className="udt-card__desc" style={{ fontStyle: 'italic' }}>
                {t('optimization.gameBoost.noCustomGames', { defaultValue: 'No custom games added' })}
              </div>
            ) : (
              config.customGameProcesses.map((game) => (
                <span
                  key={game}
                  className="udt-badge"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    padding: '3px 8px',
                    borderRadius: '4px',
                    background: 'rgba(33, 150, 243, 0.15)',
                    color: '#64B5F6'
                  }}
                >
                  {game}
                  <button
                    type="button"
                    onClick={() => removeCustomGame(game)}
                    style={{
                      background: 'transparent',
                      border: 'none',
                      color: 'inherit',
                      cursor: 'pointer',
                      padding: 0,
                      display: 'flex',
                      alignItems: 'center'
                    }}
                  >
                    <Dismiss24Regular style={{ width: '14px', height: '14px' }} />
                  </button>
                </span>
              ))
            )}
          </div>
        </div>

        {/* Background Whitelist Card */}
        <div className="udt-card">
          <div className="udt-card__title">
            {t('optimization.gameBoost.whitelistTitle', { defaultValue: 'Background App Whitelist' })}
          </div>
          <div className="udt-card__desc" style={{ marginBottom: '12px' }}>
            {t('optimization.gameBoost.whitelistDesc', {
              defaultValue: 'Applications excluded from background EcoQoS throttling (e.g. OBS, Discord).'
            })}
          </div>

          <div style={{ display: 'flex', gap: '8px', marginBottom: '12px' }}>
            <input
              type="text"
              placeholder="e.g. obs64"
              value={newWhitelistApp}
              onChange={(e) => setNewWhitelistApp(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && addWhitelistApp()}
              style={{
                flex: 1,
                padding: '6px 10px',
                borderRadius: '6px',
                border: '1px solid rgba(255,255,255,0.15)',
                background: 'rgba(0,0,0,0.2)',
                color: 'inherit'
              }}
            />
            <button type="button" className="udt-btn udt-btn--primary" onClick={addWhitelistApp}>
              {t('optimization.gameBoost.add', { defaultValue: 'Add' })}
            </button>
          </div>

          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px', maxHeight: '160px', overflowY: 'auto' }}>
            {config.backgroundWhitelist.map((app) => (
              <span
                key={app}
                className="udt-badge"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '4px',
                  padding: '3px 8px',
                  borderRadius: '4px',
                  background: 'rgba(76, 175, 80, 0.15)',
                  color: '#81C784'
                }}
              >
                {app}
                <button
                  type="button"
                  onClick={() => removeWhitelistApp(app)}
                  style={{
                    background: 'transparent',
                    border: 'none',
                    color: 'inherit',
                    cursor: 'pointer',
                    padding: 0,
                    display: 'flex',
                    alignItems: 'center'
                  }}
                >
                  <Dismiss24Regular style={{ width: '14px', height: '14px' }} />
                </button>
              </span>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
