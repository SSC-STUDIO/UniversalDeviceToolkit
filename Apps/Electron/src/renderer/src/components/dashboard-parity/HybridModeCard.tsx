import { useEffect, useMemo, useRef, useState } from 'react'
import { Button, Modal, Select, Switch, message } from 'antd'
import { Info24Regular } from '../icons/fluent'
import { LeafOne24Regular } from '@fluentui/react-icons'
import { useTranslation } from 'react-i18next'
import { powerApi } from '../../api/power'
import { useFeature } from '../../hooks/useFeature'
import { useFeaturesStore } from '../../stores/featuresStore'

/**
 * Parity card for the Electron HybridModeControlFactory:
 * - IGPU-capable machines get a ComboBox (On / OnIGPUOnly / OnAuto / Off)
 *   plus an info dialog; others get a plain On/Off toggle.
 * - Switching to/from Off asks whether to restart now (Electron asks before the
 *   state change; the toggle variant asks after applying).
 * - Changes not involving Off keep the control disabled for 5 seconds.
 */
type HybridModeValue = 'On' | 'OnIGPUOnly' | 'OnAuto' | 'Off'

interface RestartPrompt {
  mode: HybridModeValue
  resolve: (restart: boolean) => void
}

interface ModeInfoEntry {
  state: HybridModeValue
  titleKey: string
  messageKey: string
  disclaimerKey?: string
}

const MODE_INFO: readonly ModeInfoEntry[] = [
  {
    state: 'On',
    titleKey: 'feature.hybridMode.info.hybrid.title',
    messageKey: 'feature.hybridMode.info.hybrid.message'
  },
  {
    state: 'OnIGPUOnly',
    titleKey: 'feature.hybridMode.info.hybridIgpu.title',
    messageKey: 'feature.hybridMode.info.hybridIgpu.message',
    disclaimerKey: 'feature.hybridMode.info.hybridIgpu.disclaimer'
  },
  {
    state: 'OnAuto',
    titleKey: 'feature.hybridMode.info.hybridAuto.title',
    messageKey: 'feature.hybridMode.info.hybridAuto.message'
  },
  {
    state: 'Off',
    titleKey: 'feature.hybridMode.info.dgpu.title',
    messageKey: 'feature.hybridMode.info.dgpu.message',
    disclaimerKey: 'feature.hybridMode.info.dgpu.disclaimer'
  }
]

const STATE_LABEL_KEYS: Record<HybridModeValue, string> = {
  On: 'feature.hybridMode.states.hybrid',
  OnIGPUOnly: 'feature.hybridMode.states.hybridIGPUOnly',
  OnAuto: 'feature.hybridMode.states.hybridAuto',
  Off: 'feature.hybridMode.states.off'
}

/** Electron AbstractComboBoxFeatureCardControl.AdditionalStateChangeDelay. */
const NON_OFF_CHANGE_DELAY_MS = 5000

function isHybridModeValue(value: unknown): value is HybridModeValue {
  return value === 'On' || value === 'OnIGPUOnly' || value === 'OnAuto' || value === 'Off'
}

export default function HybridModeCard(): React.JSX.Element | null {
  const { t } = useTranslation()
  const { supported, state, states, loading, error, setState, refresh } = useFeature('hybridMode')
  const [changing, setChanging] = useState(false)
  const [restartPrompt, setRestartPrompt] = useState<RestartPrompt | null>(null)
  const [infoOpen, setInfoOpen] = useState(false)
  const delayTimerRef = useRef<number | null>(null)

  useEffect(() => {
    return () => {
      if (delayTimerRef.current !== null) window.clearTimeout(delayTimerRef.current)
    }
  }, [])

  const comboOptions = useMemo(
    () =>
      states.filter(isHybridModeValue).map((value) => ({
        value,
        label: t(STATE_LABEL_KEYS[value], { defaultValue: value })
      })),
    [states, t]
  )

  if (!supported) {
    return null
  }

  // Electron HybridModeControlFactory picks the ComboBox when the machine supports
  // IGPU mode; such machines expose OnIGPUOnly/OnAuto among their states.
  const isComboBox = states.some((value) => value === 'OnIGPUOnly' || value === 'OnAuto')
  const current = isHybridModeValue(state) ? state : undefined

  const title = t('feature.hybridMode', { defaultValue: 'hybridMode' })
  const description = t('feature.hybridMode.desc', { defaultValue: '' })

  function stateLabel(value: HybridModeValue): string {
    return t(STATE_LABEL_KEYS[value], { defaultValue: value })
  }

  function confirmRestart(mode: HybridModeValue): Promise<boolean> {
    return new Promise((resolve) => {
      setRestartPrompt({ mode, resolve })
    })
  }

  function readHybridState(): HybridModeValue | undefined {
    const latest = useFeaturesStore.getState().states.hybridMode
    if (isHybridModeValue(latest)) return latest
    return isHybridModeValue(state) ? state : undefined
  }

  async function attemptRestart(): Promise<void> {
    try {
      const result = await powerApi.restart()
      if (result.ok !== true) {
        message.warning(t('feature.hybridMode.restartFailed'))
      }
    } catch {
      message.warning(t('feature.hybridMode.restartFailed'))
    }
  }

  function showChangeFailure(): void {
    const failure = useFeaturesStore.getState().error ?? ''
    if (!failure.includes('IGPUModeChangeException')) return
    message.open({
      type: 'info',
      content: (
        <div className="udt-parity-hybrid-snackbar">
          <div className="udt-parity-hybrid-snackbar__title">
            {t('feature.hybridMode.changeFailed.title')}
          </div>
          <div className="udt-parity-hybrid-snackbar__message">
            {t('feature.hybridMode.changeFailed.message')}
          </div>
        </div>
      )
    })
  }

  async function handleComboChange(value: unknown): Promise<void> {
    const from = readHybridState()
    if (!isHybridModeValue(value) || from === undefined || value === from) return

    const involvesOff = from === 'Off' || value === 'Off'
    const restart = involvesOff ? await confirmRestart(value) : false

    setChanging(true)
    try {
      const ok = await setState(value)
      if (!ok) {
        showChangeFailure()
        return
      }
      await refresh()
      if (restart) await attemptRestart()
    } finally {
      if (!involvesOff && delayTimerRef.current === null) {
        delayTimerRef.current = window.setTimeout(() => {
          delayTimerRef.current = null
          setChanging(false)
        }, NON_OFF_CHANGE_DELAY_MS)
      } else {
        setChanging(false)
      }
    }
  }

  async function handleToggleChange(checked: boolean): Promise<void> {
    const next: HybridModeValue = checked ? 'On' : 'Off'
    const from = readHybridState()
    if (from === undefined || next === from) return

    setChanging(true)
    try {
      const ok = await setState(next)
      if (!ok) {
        showChangeFailure()
        return
      }
      await refresh()
      const restart = await confirmRestart(next)
      if (restart) await attemptRestart()
    } finally {
      setChanging(false)
    }
  }

  return (
    <article className="udt-parity-feature-card">
      <div className="udt-parity-feature-card__body">
        <span className="udt-parity-feature-card__icon" aria-hidden="true"><LeafOne24Regular /></span>
        <div className="udt-parity-feature-card__copy">
          <div className="udt-parity-feature-card__title" title={title}>{title}</div>
          {description !== '' && (
            <div className="udt-parity-feature-card__description" title={description}>{description}</div>
          )}
          {error != null && <div className="udt-parity-feature-card__warning" title={error}>{error}</div>}
        </div>
        <div className="udt-parity-feature-card__accessory">
          {isComboBox ? (
            <>
              <Select
                aria-label={title}
                className="udt-parity-feature-card__select"
                disabled={changing || loading || error != null || states.length === 0 || restartPrompt != null}
                loading={loading}
                value={current}
                options={comboOptions}
                onChange={(value) => void handleComboChange(value)}
              />
              <Button
                type="text"
                size="small"
                className="udt-parity-hybrid-info-btn"
                aria-label={t('feature.hybridMode.info.title')}
                icon={<Info24Regular />}
                onClick={() => setInfoOpen(true)}
              />
            </>
          ) : (
            <Switch
              aria-label={title}
              checked={current === 'On'}
              disabled={changing || loading || error != null}
              loading={loading}
              onChange={(checked) => void handleToggleChange(checked)}
            />
          )}
        </div>
      </div>

      {restartPrompt != null && (
        <Modal
          open
          title={t('feature.hybridMode.restartRequired.title')}
          okText={t('feature.hybridMode.restartRequired.now')}
          cancelText={t('feature.hybridMode.restartRequired.later')}
          closable={false}
          maskClosable={false}
          keyboard={false}
          centered
          onOk={() => {
            const prompt = restartPrompt
            setRestartPrompt(null)
            prompt?.resolve(true)
          }}
          onCancel={() => {
            const prompt = restartPrompt
            setRestartPrompt(null)
            prompt?.resolve(false)
          }}
        >
          {t('feature.hybridMode.restartRequired.message', { mode: stateLabel(restartPrompt.mode) })}
        </Modal>
      )}

      {infoOpen && (
        <Modal
          open
          title={t('feature.hybridMode.info.title')}
          width={550}
          centered
          footer={[
            <Button key="close" type="primary" onClick={() => setInfoOpen(false)}>
              {t('common.close')}
            </Button>
          ]}
          onCancel={() => setInfoOpen(false)}
        >
          <div className="udt-parity-hybrid-info">
            {MODE_INFO.filter((entry) => states.includes(entry.state)).map((entry) => (
              <div key={entry.state} className="udt-parity-hybrid-info__section">
                <div className="udt-parity-hybrid-info__title">{t(entry.titleKey)}</div>
                <div className="udt-parity-hybrid-info__message">{t(entry.messageKey)}</div>
                {entry.disclaimerKey != null && (
                  <div className="udt-parity-hybrid-info__disclaimer">{t(entry.disclaimerKey)}</div>
                )}
              </div>
            ))}
          </div>
        </Modal>
      )}
    </article>
  )
}
