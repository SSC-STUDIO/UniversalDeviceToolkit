import { useEffect, useState } from 'react'
import { Checkbox, Empty, Modal, Spin, Switch, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { automationApi } from '../../api/automation'
import type { AutomationPipeline } from '../../api/automation'
import { settingsApi } from '../../api/settings'
import { useSettingsStore } from '../../stores/settingsStore'
import { SettingsLoadError } from './SettingsLoadError'

/**
 * Parity modal for Electron Windows/Settings/SelectSmartKeyPipelinesWindow:
 * binds one or more manual (trigger-less) automation pipelines to the
 * smart key (Fn+F9) single or double press. "Show this app" leaves the
 * smart key action unset so pressing the key just brings the app forward.
 */

interface SmartKeyPipelinesModalProps {
  open: boolean
  isDoublePress?: boolean
  onClose: () => void
  onSaved?: () => void
}

export default function SmartKeyPipelinesModal({
  open,
  isDoublePress = false,
  onClose,
  onSaved
}: SmartKeyPipelinesModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const [pipelines, setPipelines] = useState<AutomationPipeline[]>([])
  const [checkedIds, setCheckedIds] = useState<Set<string>>(new Set())
  const [showThisApp, setShowThisApp] = useState(false)
  const [pipelinesReady, setPipelinesReady] = useState(false)

  useEffect(() => {
    if (!open) return
    let cancelled = false
    setLoading(true)
    setLoadError(null)
    setPipelinesReady(false)
    void (async () => {
      try {
        const [state, settingsResult] = await Promise.all([
          automationApi.getState(),
          settingsApi.get('application')
        ])
        if (cancelled) return

        const store = (settingsResult.value ?? {}) as Record<string, unknown>
        const actionId = isDoublePress
          ? store.SmartKeyDoublePressActionId
          : store.SmartKeySinglePressActionId
        const actionList = isDoublePress
          ? store.SmartKeyDoublePressActionList
          : store.SmartKeySinglePressActionList
        const list = Array.isArray(actionList)
          ? actionList.filter((id): id is string => typeof id === 'string')
          : []

        const manual = (state.pipelines ?? [])
          .filter((pipeline) => pipeline.trigger == null)
          .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))

        setPipelines(manual)
        setShowThisApp(actionId == null)
        setCheckedIds(
          new Set(
            manual
              .map((pipeline) => pipeline.id)
              .filter((id): id is string => id != null && (list.includes(id) || id === actionId))
          )
        )
        setPipelinesReady(true)
      } catch (reason) {
        if (cancelled) return
        setPipelinesReady(false)
        setLoadError(reason instanceof Error ? reason.message : String(reason))
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [open, isDoublePress, reloadToken])

  const togglePipeline = (id: string, checked: boolean): void => {
    setCheckedIds((current) => {
      const next = new Set(current)
      if (checked) next.add(id)
      else next.delete(id)
      return next
    })
  }

  const handleSave = async (): Promise<void> => {
    if (!pipelinesReady) return
    setSaving(true)
    try {
      const result = await settingsApi.get('application')
      const current = (result.value ?? {}) as Record<string, unknown>
      const selected = pipelines
        .map((pipeline) => pipeline.id)
        .filter((id): id is string => id != null && checkedIds.has(id))
      const actionId = showThisApp ? null : (selected[0] ?? null)
      const merged = {
        ...current,
        ...(isDoublePress
          ? {
              SmartKeyDoublePressActionList: selected,
              SmartKeyDoublePressActionId: actionId
            }
          : {
              SmartKeySinglePressActionList: selected,
              SmartKeySinglePressActionId: actionId
            })
      }
      useSettingsStore.getState().setScope('application', merged)
      await settingsApi.set('application', merged)
      await settingsApi.save(['application'])
      onSaved?.()
      onClose()
    } catch (reason) {
      void message.error((reason as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const titleKey = isDoublePress
    ? 'settingsPagesmartKeyDoublePressActiontitle'
    : 'settingsPagesmartKeySinglePressActiontitle'

  return (
    <Modal
      centered
      open={open}
      title={t(titleKey)}
      width={400}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      confirmLoading={saving}
      okButtonProps={{ disabled: loading || loadError != null || !pipelinesReady }}
      onOk={() => {
        if (!pipelinesReady) return
        void handleSave()
      }}
      onCancel={onClose}
    >
      {loading ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : loadError != null || !pipelinesReady ? (
        <SettingsLoadError
          message={loadError}
          onRetry={() => setReloadToken((value) => value + 1)}
        />
      ) : (
        <div>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 16,
              padding: '12px 0'
            }}
          >
            <span style={{ fontWeight: 600 }}>{t('wpf.selectSmartKeyPipelinesWindowshowThisApp')}</span>
            <Switch
              className="udt-settings-switch"
              checked={showThisApp}
              onChange={(checked) => {
                setShowThisApp(checked)
                if (checked) setCheckedIds(new Set())
              }}
            />
          </div>
          {!showThisApp && (
            <>
              <p style={{ opacity: 0.75 }}>{t('wpf.selectSmartKeyPipelinesWindowlistdescription')}</p>
              {pipelines.length === 0 ? (
                <Empty description={t('wpf.selectSmartKeyPipelinesWindowlistempty')} />
              ) : (
                <div
                  style={{
                    maxHeight: 260,
                    overflowY: 'auto',
                    border: '1px solid rgba(128,128,128,0.25)',
                    borderRadius: 6,
                    padding: '4px 12px'
                  }}
                >
                  {pipelines.map((pipeline) => {
                    const id = pipeline.id
                    if (id == null) return null
                    return (
                      <Checkbox
                        key={id}
                        checked={checkedIds.has(id)}
                        onChange={(event) => togglePipeline(id, event.target.checked)}
                      >
                        {pipeline.name ?? id}
                      </Checkbox>
                    )
                  })}
                </div>
              )}
            </>
          )}
        </div>
      )}
    </Modal>
  )
}
