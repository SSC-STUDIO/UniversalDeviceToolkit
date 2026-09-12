import { useEffect, useState } from 'react'
import { Button, Checkbox, Empty, Modal, Spin, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { featuresApi } from '../../api/features'
import { settingsApi } from '../../api/settings'
import { useSettingsStore } from '../../stores/settingsStore'

/**
 * Parity modal for Electron Windows/Settings/ExcludeRefreshRatesWindow:
 * a checkbox list of all refresh rates offered by the built-in display.
 * Checked rates stay in the Fn+R rotation; unchecked rates are excluded
 * (persisted to ApplicationSettings.ExcludedRefreshRates).
 */

interface ExcludeRefreshRatesModalProps {
  open: boolean
  onClose: () => void
  onSaved?: () => void
}

interface RefreshRateItem {
  frequency: number
  displayName: string
  excluded: boolean
}

function readFrequency(value: unknown): number | null {
  if (typeof value !== 'object' || value === null) return null
  const record = value as Record<string, unknown>
  const frequency = record.Frequency ?? record.frequency
  return typeof frequency === 'number' && Number.isFinite(frequency) ? frequency : null
}

function readDisplayName(value: unknown): string | undefined {
  if (typeof value !== 'object' || value === null) return undefined
  const record = value as Record<string, unknown>
  const name = record.DisplayName ?? record.displayName
  return typeof name === 'string' ? name : undefined
}

export default function ExcludeRefreshRatesModal({
  open,
  onClose,
  onSaved
}: ExcludeRefreshRatesModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [items, setItems] = useState<RefreshRateItem[] | null>(null)
  const [loadFailed, setLoadFailed] = useState(false)

  const refresh = async (): Promise<void> => {
    setLoading(true)
    setLoadFailed(false)
    setItems(null)
    try {
      const [statesResult, settingsResult] = await Promise.all([
        featuresApi.getStates('refreshRate'),
        settingsApi.get('application')
      ])

      const rates = (statesResult.states ?? [])
        .map((state) => ({
          frequency: readFrequency(state),
          displayName: readDisplayName(state)
        }))
        .filter(
          (rate): rate is { frequency: number; displayName: string | undefined } =>
            rate.frequency !== null
        )

      const current = (settingsResult.value ?? {}) as Record<string, unknown>
      const excluded =
        (current.ExcludedRefreshRates as Array<Record<string, unknown>> | undefined) ?? []
      const excludedFrequencies = new Set(
        excluded.map((rate) => readFrequency(rate)).filter((frequency): frequency is number => frequency !== null)
      )

      const frequencies = new Set<number>()
      const merged: RefreshRateItem[] = []
      for (const rate of [
        ...rates,
        ...excluded.map((rate) => ({
          frequency: readFrequency(rate),
          displayName: readDisplayName(rate)
        }))
      ]) {
        if (rate.frequency === null || frequencies.has(rate.frequency)) continue
        frequencies.add(rate.frequency)
        merged.push({
          frequency: rate.frequency,
          displayName: rate.displayName ?? `${rate.frequency}Hz`,
          excluded: excludedFrequencies.has(rate.frequency)
        })
      }
      merged.sort((a, b) => a.frequency - b.frequency)
      setItems(merged)
    } catch {
      setLoadFailed(true)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (!open) return
    void refresh()
  }, [open])

  const handleSave = async (): Promise<void> => {
    if (items == null) return
    setSaving(true)
    try {
      const settingsResult = await settingsApi.get('application')
      const current = (settingsResult.value ?? {}) as Record<string, unknown>
      const excluded = items.filter((item) => item.excluded).map((item) => ({ Frequency: item.frequency }))
      const next = { ...current, ExcludedRefreshRates: excluded }
      useSettingsStore.getState().setScope('application', next)
      await settingsApi.set('application', next)
      await settingsApi.save(['application'])
      onSaved?.()
      onClose()
    } catch (reason) {
      void message.error((reason as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const footer = [
    <Button key="cancel" onClick={onClose}>
      {t('common.cancel')}
    </Button>,
    <Button key="save" type="primary" loading={saving} onClick={() => void handleSave()}>
      {t('common.save')}
    </Button>
  ]

  return (
    <Modal
      centered
      open={open}
      title={t('wpf.excludeRefreshRatesWindowtitle')}
      width={400}
      footer={loading || items == null ? undefined : footer}
      onCancel={onClose}
    >
      {loading ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : loadFailed ? (
        <Empty description={t('common.error')}>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'center' }}>
            <Button type="primary" onClick={() => void refresh()}>
              {t('common.retry')}
            </Button>
            <Button onClick={onClose}>{t('common.cancel')}</Button>
          </div>
        </Empty>
      ) : items == null || items.length === 0 ? (
        <div>
          <p>{t('wpf.excludeRefreshRatesWindownoRefreshRatesFoundmessage')}</p>
          <Button type="primary" onClick={() => void refresh()}>
            {t('tryAgain')}
          </Button>
        </div>
      ) : (
        <div>
          <p>{t('wpf.excludeRefreshRatesWindowdescription')}</p>
          <div
            style={{
              maxHeight: 300,
              overflowY: 'auto',
              border: '1px solid rgba(128,128,128,0.25)',
              borderRadius: 6,
              padding: '4px 12px'
            }}
          >
            {items.map((item) => (
              <Checkbox
                key={item.frequency}
                checked={!item.excluded}
                onChange={(event) => {
                  setItems((current) =>
                    (current ?? []).map((entry) =>
                      entry.frequency === item.frequency
                        ? { ...entry, excluded: !event.target.checked }
                        : entry
                    )
                  )
                }}
              >
                {item.displayName}
              </Checkbox>
            ))}
          </div>
        </div>
      )}
    </Modal>
  )
}
