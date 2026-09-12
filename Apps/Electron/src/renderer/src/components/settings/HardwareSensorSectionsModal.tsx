import { useEffect, useState } from 'react'
import { Button, List, Modal, Spin, Switch, message } from 'antd'
import { useTranslation } from 'react-i18next'
import { settingsApi } from '../../api/settings'
import { useSettingsStore } from '../../stores/settingsStore'
import { SettingsLoadError } from './SettingsLoadError'

/**
 * Parity modal for Electron Windows/Settings/HardwareSensorSectionsWindow:
 * choose which sensor sections (CPU / Battery / GPU) are visible on the
 * dashboard and in which order. Persisted to the hardwareSensors scope
 * (VisibleSections / SectionOrder).
 */

interface HardwareSensorSectionsModalProps {
  open: boolean
  onClose: () => void
  onSaved?: () => void
}

const ALL_SECTIONS = ['CPU', 'Battery', 'GPU'] as const

type SensorSection = (typeof ALL_SECTIONS)[number]

function getSectionLabel(
  section: SensorSection,
  t: (key: string, options?: Record<string, unknown>) => string
): string {
  if (section === 'CPU') return t('wpf.sensorSectionCpu', { defaultValue: 'CPU' })
  if (section === 'Battery') return t('wpf.sensorSectionBattery', { defaultValue: '电池' })
  if (section === 'GPU') return t('wpf.sensorSectionGpu', { defaultValue: 'GPU' })
  return section
}

export default function HardwareSensorSectionsModal({
  open,
  onClose,
  onSaved
}: HardwareSensorSectionsModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const [visibility, setVisibility] = useState<Record<string, boolean>>({})
  const [order, setOrder] = useState<SensorSection[]>([])
  const [selected, setSelected] = useState<number | null>(null)

  useEffect(() => {
    if (!open) return
    let cancelled = false
    setLoading(true)
    setLoadError(null)
    settingsApi
      .get('hardwareSensors')
      .then((result) => {
        if (cancelled) return
        const store = (result.value ?? {}) as Record<string, unknown>
        const sectionOrderRaw = store.SectionOrder ?? store.sectionOrder
        const visibleSectionsRaw = store.VisibleSections ?? store.visibleSections
        const sectionOrder = Array.isArray(sectionOrderRaw)
          ? (sectionOrderRaw as unknown[]).filter((section): section is string => typeof section === 'string')
          : []
        const visibleSections = Array.isArray(visibleSectionsRaw)
          ? (visibleSectionsRaw as unknown[]).filter((section): section is string => typeof section === 'string')
          : ALL_SECTIONS

        const matchKnown = (value: string): SensorSection | undefined =>
          ALL_SECTIONS.find((section) => section.toUpperCase() === value.toUpperCase())
        const seen = new Set<SensorSection>()
        const normalizedOrder: SensorSection[] = []
        for (const entry of sectionOrder) {
          const known = matchKnown(entry)
          if (known == null || seen.has(known)) continue
          seen.add(known)
          normalizedOrder.push(known)
        }
        for (const section of ALL_SECTIONS) {
          if (seen.has(section)) continue
          normalizedOrder.push(section)
        }

        const visible = new Set(visibleSections.map((section) => section.toUpperCase()))
        setVisibility(
          Object.fromEntries(
            ALL_SECTIONS.map((section) => [section, visible.has(section.toUpperCase())])
          )
        )
        setOrder(normalizedOrder)
        setSelected(normalizedOrder.length > 0 ? 0 : null)
      })
      .catch((reason: unknown) => {
        if (cancelled) return
        setLoadError(reason instanceof Error ? reason.message : String(reason))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [open, reloadToken])

  const move = (delta: number): void => {
    if (selected == null) return
    const target = selected + delta
    if (target < 0 || target >= order.length) return
    setOrder((current) => {
      const next = [...current]
      const item = next[selected]
      if (item == null) return current
      next.splice(selected, 1)
      next.splice(target, 0, item)
      return next
    })
    setSelected(target)
  }

  const handleSave = async (): Promise<void> => {
    if (loadError != null) return
    setSaving(true)
    try {
      const result = await settingsApi.get('hardwareSensors')
      const current = (result.value ?? {}) as Record<string, unknown>
      const visibleSections = ALL_SECTIONS.filter((section) => visibility[section] === true)
      const next = {
        ...current,
        VisibleSections: visibleSections.length > 0 ? visibleSections : [...ALL_SECTIONS],
        SectionOrder: order
      }
      useSettingsStore.getState().setScope('hardwareSensors', next)
      await settingsApi.set('hardwareSensors', next)
      await settingsApi.save(['hardwareSensors'])
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
      title={t('wpf.hardwareSensorSectionsWindowtitle', { defaultValue: '传感器分区' })}
      width={420}
      okText={t('saveButton', { defaultValue: '保存' })}
      cancelText={t('common.cancel', { defaultValue: '取消' })}
      confirmLoading={saving}
      okButtonProps={{ disabled: loading || loadError != null }}
      onOk={() => {
        if (loadError != null) return
        void handleSave()
      }}
      onCancel={onClose}
    >
      {loading ? (
        <div style={{ textAlign: 'center', padding: 24 }}>
          <Spin />
        </div>
      ) : loadError != null ? (
        <SettingsLoadError
          message={loadError}
          onRetry={() => setReloadToken((value) => value + 1)}
        />
      ) : (
        <div>
          <div style={{ fontWeight: 600, marginBottom: 8 }}>
            {t('wpf.sensorSectionsvisibletitle', { defaultValue: '可见分区' })}
          </div>
          {ALL_SECTIONS.map((section) => (
            <div
              key={section}
              style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}
            >
              <span>{getSectionLabel(section, t)}</span>
              <Switch
                className="udt-settings-switch"
                checked={visibility[section] === true}
                onChange={(checked) =>
                  setVisibility((current) => ({ ...current, [section]: checked }))
                }
              />
            </div>
          ))}
          <div style={{ fontWeight: 600, margin: '16px 0 8px' }}>
            {t('wpf.sensorSectionsordertitle', { defaultValue: '分区排序' })}
          </div>
          <List
            size="small"
            bordered
            style={{ maxHeight: 140, overflowY: 'auto' }}
            dataSource={order}
            renderItem={(section, index) => (
              <List.Item
                onClick={() => setSelected(index)}
                style={{
                  cursor: 'pointer',
                  background: selected === index ? 'rgba(22,119,255,0.12)' : undefined,
                  paddingLeft: 12
                }}
              >
                {getSectionLabel(section, t)}
              </List.Item>
            )}
          />
          <div style={{ marginTop: 8 }}>
            <Button size="small" disabled={selected == null || selected <= 0} onClick={() => move(-1)}>
              {t('wpf.sensorSectionsmoveUp', { defaultValue: '上移' })}
            </Button>
            <Button
              size="small"
              style={{ marginLeft: 8 }}
              disabled={selected == null || selected >= order.length - 1}
              onClick={() => move(1)}
            >
              {t('wpf.sensorSectionsmoveDown', { defaultValue: '下移' })}
            </Button>
          </div>
        </div>
      )}
    </Modal>
  )
}
