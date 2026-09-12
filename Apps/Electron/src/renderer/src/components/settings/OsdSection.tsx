import { useEffect, useRef, useState } from 'react'
import { Button, Checkbox, ColorPicker, InputNumber, Select, Slider, Switch, Tabs } from 'antd'
import { useTranslation } from 'react-i18next'
import type { OsdItemName } from '../../api/osd'
import { sensorsApi } from '../../api/sensors'
import { useOsdSettingsStore } from '../../stores/osdSettingsStore'
import { SettingsCard } from './SettingsCard'

/**
 * OSD settings — port of the Electron OsdSettingsWindow (General / Appearance /
 * Thresholds / Sensors tabs). Values are persisted to the "osd" settings
 * scope; the main-process OSD window applies them on settings.changed.
 */

interface ItemGroup {
  key: string
  items: OsdItemName[]
}

const ITEM_GROUPS: ItemGroup[] = [
  { key: 'game', items: ['Fps', 'LowFps', 'FrameTime'] },
  {
    key: 'cpu',
    items: ['CpuUtilization', 'CpuFrequency', 'CpuTemperature', 'CpuPower', 'CpuFan']
  },
  {
    key: 'gpu',
    items: [
      'GpuUtilization',
      'GpuFrequency',
      'GpuTemperature',
      'GpuVramUtilization',
      'GpuVramTemperature',
      'GpuPower',
      'GpuFan'
    ]
  },
  {
    key: 'pch',
    items: [
      'MemoryUtilization',
      'MemoryTemperature',
      'Disk1Temperature',
      'Disk2Temperature',
      'PchTemperature',
      'PchFan'
    ]
  }
]

const HYBRID_CPU_ITEMS: OsdItemName[] = [
  'CpuUtilization',
  'CpuPCoreFrequency',
  'CpuECoreFrequency',
  'CpuTemperature',
  'CpuPower',
  'CpuFan'
]

function useDebouncedUpdate(delayMs = 200): {
  debounced: (patch: () => Partial<Record<string, unknown>>) => void
} {
  const { update } = useOsdSettingsStore()
  const timerRef = useRef<number | null>(null)
  const patchRef = useRef<Partial<Record<string, unknown>> | null>(null)

  useEffect(() => {
    return () => {
      if (timerRef.current !== null) {
        window.clearTimeout(timerRef.current)
      }
    }
  }, [])

  const debounced = (patch: () => Partial<Record<string, unknown>>): void => {
    patchRef.current = { ...patchRef.current, ...patch() }
    if (timerRef.current !== null) window.clearTimeout(timerRef.current)
    timerRef.current = window.setTimeout(() => {
      timerRef.current = null
      if (patchRef.current) {
        void update(patchRef.current)
        patchRef.current = null
      }
    }, delayMs)
  }

  return { debounced }
}

function hexToRgbString(hex: string): string {
  const value = hex.replace(/^#/, '')
  const full =
    value.length === 3
      ? value
          .split('')
          .map((c) => c + c)
          .join('')
      : value
  const parsed = parseInt(full, 16)
  if (Number.isNaN(parsed)) return '30,30,30'
  return `${(parsed >> 16) & 0xff},${(parsed >> 8) & 0xff},${parsed & 0xff}`
}

/** Live preview of the OSD background/rounded box. */
function OsdPreview(): React.JSX.Element {
  const { settings } = useOsdSettingsStore()
  const alpha = Math.min(1, Math.max(0, settings.backgroundOpacity))
  const radius =
    settings.selectedStyleIndex === 1 || settings.selectedStyleIndex === 2
      ? settings.cornerRadiusTop
      : `${settings.cornerRadiusTop}px ${settings.cornerRadiusTop}px ${settings.cornerRadiusBottom}px ${settings.cornerRadiusBottom}px`
  return (
    <div className="udt-osd-preview">
      <div className="udt-osd-preview__bar" style={{ borderRadius: radius }}>
        <div
          className="udt-osd-preview__box"
          style={{
            background: `rgba(${hexToRgbString(settings.backgroundColor)},${alpha.toFixed(3)})`,
            borderRadius: radius
          }}
        />
      </div>
    </div>
  )
}

export function OsdSection(): React.JSX.Element {
  const { t } = useTranslation()
  const { settings, loading, load, update } = useOsdSettingsStore()
  const { debounced } = useDebouncedUpdate()
  const [isHybrid, setIsHybrid] = useState(false)

  useEffect(() => {
    void load()
    sensorsApi
      .getStatus()
      .then((status) => setIsHybrid(status.isHybrid === true))
      .catch(() => undefined)
  }, [load])

  const groups = ITEM_GROUPS.map((group) => ({
    ...group,
    items: group.key === 'cpu' && isHybrid ? HYBRID_CPU_ITEMS : group.items
  }))

  const persist = (patch: Partial<Record<string, unknown>>): void => {
    void update(patch)
  }

  const colorField = (
    label: string,
    value: string,
    key: 'backgroundColor' | 'categoryColor' | 'labelColor' | 'valueColor' | 'warningColor' | 'criticalColor' | 'separatorColor'
  ): React.JSX.Element => (
    <div className={`udt-settings-row udt-settings-row--color${!settings.showOsd ? ' udt-settings-row--disabled' : ''}`}>
      <span className="udt-settings-row__label">{label}</span>
      <ColorPicker
        value={value}
        disabled={loading || !settings.showOsd}
        onChange={(color) => debounced(() => ({ [key]: color.toHexString() }))}
      />
    </div>
  )

  const sliderRow = (
    label: string,
    value: number,
    hint: string,
    min: number,
    max: number,
    step: number,
    key: 'backgroundOpacity' | 'cornerRadiusTop' | 'cornerRadiusBottom'
  ): React.JSX.Element => (
    <div className={`udt-settings-row udt-settings-row--slider${!settings.showOsd ? ' udt-settings-row--disabled' : ''}`}>
      <div className="udt-settings-row__copy">
        <span className="udt-settings-row__label">{label}</span>
        <span className="udt-settings-row__hint">{hint}</span>
      </div>
      <Slider
        className="udt-settings-row__slider"
        min={min}
        max={max}
        step={step}
        value={value}
        disabled={loading || !settings.showOsd}
        onChange={(next) => debounced(() => ({ [key]: next }))}
      />
    </div>
  )

  const numberField = (
    label: string,
    value: number,
    min: number,
    max: number,
    step: number,
    key: 'osdRefreshInterval' | 'snapThreshold' | 'fontSize' | 'tempThresholdWarning' | 'tempThresholdCritical' | 'usageThresholdWarning' | 'usageThresholdCritical' | 'fpsThresholdCritical' | 'lowFpsDeltaThreshold'
  ): React.JSX.Element => (
    <div className={`udt-settings-row${!settings.showOsd ? ' udt-settings-row--disabled' : ''}`}>
      <span className="udt-settings-row__label">{label}</span>
      <InputNumber
        className="udt-settings-row__number"
        min={min}
        max={max}
        step={step}
        precision={step < 1 ? 1 : 0}
        value={value}
        disabled={loading || !settings.showOsd}
        onChange={(next) => {
          if (next === null || next === undefined) return
          void update({ [key]: next })
        }}
      />
    </div>
  )

  const thresholdGroup = (
    title: string,
    warning: React.JSX.Element,
    critical: React.JSX.Element
  ): React.JSX.Element => (
    <div className="udt-settings-group">
      <div className="udt-settings-group__title">{title}</div>
      {warning}
      {critical}
    </div>
  )

  return (
    <div className="udt-settings-section udt-settings-section--osd">
      <SettingsCard>
        <div className="udt-osd-settings-layout">
          <div className="udt-osd-settings-layout__main">
            <Tabs
              className="udt-settings-tabs udt-osd-settings-tabs"
              items={[
            {
              key: 'general',
              label: t('settings.osd.tabs.general'),
              children: (
                <div className="udt-settings-fields udt-osd-settings-fields udt-osd-settings-fields--form">
                  <div className="udt-settings-row">
                    <div className="udt-settings-row__copy">
                      <span className="udt-settings-row__label">{t('settings.osd.showOsd')}</span>
                      <span className="udt-settings-row__hint">{t('settings.osd.showOsdDesc')}</span>
                    </div>
                    <Switch
                      className="udt-settings-switch"
                      checked={settings.showOsd}
                      disabled={loading}
                      onChange={(checked) => persist({ showOsd: checked })}
                    />
                  </div>
                  <div className={`udt-settings-row${!settings.showOsd ? ' udt-settings-row--disabled' : ''}`}>
                    <span className="udt-settings-row__label">{t('settings.osd.style')}</span>
                    <Select<number>
                      className="udt-settings-row__select"
                      value={settings.selectedStyleIndex}
                      disabled={loading || !settings.showOsd}
                      options={[
                        { value: 0, label: t('settings.osd.styles.panel') },
                        { value: 1, label: t('settings.osd.styles.bar') },
                        { value: 2, label: t('settings.osd.styles.mini') }
                      ]}
                      onChange={(value) => persist({ selectedStyleIndex: value })}
                    />
                  </div>
                  {numberField(
                    t('settings.osd.refreshInterval'),
                    settings.osdRefreshInterval,
                    0.1,
                    10,
                    0.1,
                    'osdRefreshInterval'
                  )}
                  {numberField(
                    t('settings.osd.snapThreshold'),
                    settings.snapThreshold,
                    0,
                    100,
                    1,
                    'snapThreshold'
                  )}
                  <div className={`udt-settings-row${!settings.showOsd ? ' udt-settings-row--disabled' : ''}`}>
                    <span className="udt-settings-row__label">{t('settings.osd.lockPosition')}</span>
                    <span className="udt-settings-row__actions">
                      <Button
                        size="small"
                        disabled={loading || !settings.showOsd}
                        onClick={() =>
                          void update({
                            panelPositionX: null,
                            panelPositionY: null,
                            barPositionX: null,
                            barPositionY: null
                          })
                        }
                      >
                        {t('settings.osd.resetPosition')}
                      </Button>
                      <Switch
                        className="udt-settings-switch"
                        checked={settings.isLocked}
                        disabled={loading || !settings.showOsd}
                        onChange={(checked) => persist({ isLocked: checked })}
                      />
                    </span>
                  </div>
                </div>
              )
            },
            {
              key: 'appearance',
              label: t('settings.osd.tabs.appearance'),
              children: (
                <div className="udt-settings-fields udt-osd-settings-fields udt-osd-settings-fields--form">
                  {sliderRow(
                    t('settings.osd.opacity'),
                    settings.backgroundOpacity,
                    `${Math.round(settings.backgroundOpacity * 100)}%`,
                    0,
                    1,
                    0.1,
                    'backgroundOpacity'
                  )}
                  {sliderRow(
                    t('settings.osd.cornerRadius'),
                    settings.cornerRadiusTop,
                    `${settings.cornerRadiusTop} / ${settings.cornerRadiusBottom}`,
                    0,
                    50,
                    1,
                    'cornerRadiusTop'
                  )}
                  {sliderRow(
                    `${t('settings.osd.cornerRadius')} (${t('settings.osd.cornerRadiusBottom')})`,
                    settings.cornerRadiusBottom,
                    `${settings.cornerRadiusTop} / ${settings.cornerRadiusBottom}`,
                    0,
                    50,
                    1,
                    'cornerRadiusBottom'
                  )}
                  {numberField(t('settings.osd.fontSize'), settings.fontSize, 8, 24, 1, 'fontSize')}
                  {colorField(t('settings.osd.background'), settings.backgroundColor, 'backgroundColor')}
                  {colorField(t('settings.osd.category'), settings.categoryColor, 'categoryColor')}
                  {colorField(t('settings.osd.label'), settings.labelColor, 'labelColor')}
                  {colorField(t('settings.osd.value'), settings.valueColor, 'valueColor')}
                  {colorField(t('settings.osd.warning'), settings.warningColor, 'warningColor')}
                  {colorField(t('settings.osd.critical'), settings.criticalColor, 'criticalColor')}
                  {colorField(t('settings.osd.separator'), settings.separatorColor, 'separatorColor')}
                </div>
              )
            },
            {
              key: 'thresholds',
              label: t('settings.osd.tabs.thresholds'),
              children: (
                <div className="udt-settings-fields udt-osd-threshold-groups">
                  {thresholdGroup(
                    t('settings.osd.thresholds.performance'),
                    numberField(
                      t('settings.osd.thresholds.fpsRedline'),
                      settings.fpsThresholdCritical,
                      0,
                      1000,
                      1,
                      'fpsThresholdCritical'
                    ),
                    numberField(
                      t('settings.osd.thresholds.lowFpsDelta'),
                      settings.lowFpsDeltaThreshold,
                      0,
                      1000,
                      1,
                      'lowFpsDeltaThreshold'
                    )
                  )}
                  {thresholdGroup(
                    t('settings.osd.thresholds.temperature'),
                    numberField(
                      t('settings.osd.thresholds.warning'),
                      settings.tempThresholdWarning,
                      0,
                      110,
                      1,
                      'tempThresholdWarning'
                    ),
                    numberField(
                      t('settings.osd.thresholds.critical'),
                      settings.tempThresholdCritical,
                      0,
                      110,
                      1,
                      'tempThresholdCritical'
                    )
                  )}
                  {thresholdGroup(
                    t('settings.osd.thresholds.usage'),
                    numberField(
                      t('settings.osd.thresholds.warning'),
                      settings.usageThresholdWarning,
                      0,
                      100,
                      1,
                      'usageThresholdWarning'
                    ),
                    numberField(
                      t('settings.osd.thresholds.critical'),
                      settings.usageThresholdCritical,
                      0,
                      100,
                      1,
                      'usageThresholdCritical'
                    )
                  )}
                </div>
              )
            },
            {
              key: 'sensors',
              label: t('settings.osd.tabs.sensors'),
              children: (
                <div className="udt-settings-fields udt-osd-settings-fields">
                  {groups.map((group) => (
                    <div key={group.key} className="udt-settings-group">
                      <div className="udt-settings-group__title">
                        {t(`settings.osd.items.groups.${group.key}`)}
                      </div>
                      <div className="udt-settings-checkbox-list">
                        {group.items.map((item) => (
                          <Checkbox
                            key={item}
                            checked={settings.items.includes(item)}
                            disabled={loading || !settings.showOsd}
                            onChange={(e) => {
                              const next = e.target.checked
                                ? [...settings.items, item]
                                : settings.items.filter((existing) => existing !== item)
                              void update({ items: next })
                            }}
                          >
                            {t(`settings.osd.items.names.${item}`)}
                          </Checkbox>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              )
            }
          ]}
            />
          </div>
          <aside className="udt-osd-settings-layout__aside" aria-label={t('settings.osd.previewHint')}>
            <div className="udt-osd-preview__heading">{t('settings.osd.previewHint')}</div>
            <OsdPreview />
          </aside>
        </div>
      </SettingsCard>
    </div>
  )
}
