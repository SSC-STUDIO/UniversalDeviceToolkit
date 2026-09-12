import { useEffect, useState } from 'react'
import { Checkbox, Modal, Spin, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { aiApi } from '../../api/ai'
import { settingsApi } from '../../api/settings'
import { useFeaturesStore } from '../../stores/featuresStore'

/**
 * Parity modal for Electron Windows/Dashboard/BalanceModeSettingsWindow:
 * a single "Enable AI Engine" toggle persisted through ai.setEnabled
 * (AIController) and balancemode.json, then switching power mode to Balance.
 */
interface BalanceModeSettingsModalProps {
  open: boolean
  onClose: () => void
  onSaved?: () => void
}

interface BalanceModeStore {
  aiModeEnabled: boolean
}

function parseStore(value: unknown): BalanceModeStore {
  const record = (value ?? {}) as Record<string, unknown>
  return { aiModeEnabled: record.AIModeEnabled === true || record.aiModeEnabled === true }
}

export default function BalanceModeSettingsModal({
  open,
  onClose,
  onSaved
}: BalanceModeSettingsModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [aiModeEnabled, setAiModeEnabled] = useState(false)
  const [aiSupported, setAiSupported] = useState(true)
  const [openKey, setOpenKey] = useState(open)
  if (open !== openKey) {
    setOpenKey(open)
    if (open) setLoading(true)
  }

  useEffect(() => {
    if (!open) return
    let cancelled = false
    Promise.all([
      settingsApi.get('balanceMode'),
      aiApi.getStatus().catch(() => null)
    ])
      .then(([result, ai]) => {
        if (cancelled) return
        if (ai != null) {
          setAiModeEnabled(ai.enabled)
          setAiSupported(ai.supported)
        } else {
          setAiModeEnabled(parseStore(result.value).aiModeEnabled)
        }
      })
      .catch((reason: unknown) => {
        if (!cancelled) void message.error((reason as Error).message)
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [open])

  const handleSave = async (): Promise<void> => {
    setSaving(true)
    try {
      const enabled = await aiApi.setEnabled(aiModeEnabled)
      if (enabled.ok !== true) {
        throw new Error(t('balanceMode.saveFailed', {
          defaultValue: 'Failed to update AI Engine.'
        }))
      }
      await settingsApi.set('balanceMode', { AIModeEnabled: aiModeEnabled })
      const saved = await settingsApi.save(['balanceMode'])
      if (!saved.saved.includes('balanceMode')) {
        throw new Error(t('balanceMode.saveFailed', {
          defaultValue: 'Failed to save Balance Mode settings.'
        }))
      }
      const switched = await useFeaturesStore.getState().setState('powerMode', 'Balance')
      if (!switched) {
        throw new Error(t('feature.powerMode.changeFailed', {
          defaultValue: 'Failed to switch to Balance mode.'
        }))
      }
      await useFeaturesStore.getState().refresh('powerMode')
      onSaved?.()
      onClose()
    } catch (reason) {
      void message.error((reason as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      centered
      open={open}
      title={t('balanceMode.title')}
      width={400}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      confirmLoading={saving}
      onOk={() => void handleSave()}
      onCancel={onClose}
    >
      {loading ? (
        <div className="udt-dashboard-edit__loading">
          <Spin size="small" />
        </div>
      ) : (
        <div>
          <Checkbox
            checked={aiModeEnabled}
            disabled={!aiSupported}
            onChange={(event) => setAiModeEnabled(event.target.checked)}
          >
            {t('balanceMode.aiEngine')}
          </Checkbox>
          <div className="udt-balance-mode__description">{t('balanceMode.aiEngineDesc')}</div>
        </div>
      )}
    </Modal>
  )
}
