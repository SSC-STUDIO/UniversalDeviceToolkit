import { useState } from 'react'
import { message } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  ArrowDownload24Regular,
  ArrowExport24Regular,
  ArrowImport24Regular,
  Copy24Regular,
  Flash24Regular,
  Rocket24Regular,
  BatteryCharge24Regular,
  WeatherMoon24Regular
} from '../icons/fluent'
import type { AutomationPipeline } from '../../api/automation'
import AutomationModal from './AutomationModal'

export interface AutomationPresetModalProps {
  pipelines: AutomationPipeline[]
  onImportPipelines: (newPipelines: AutomationPipeline[], replace?: boolean) => void
  onClose: () => void
}

interface TemplatePreset {
  key: string
  titleKey: string
  descKey: string
  icon: React.JSX.Element
  pipelines: Omit<AutomationPipeline, 'id'>[]
}

const TEMPLATES: TemplatePreset[] = [
  {
    key: 'mobileEco',
    titleKey: 'automation.presetMobileEcoTitle',
    descKey: 'automation.presetMobileEcoDesc',
    icon: <WeatherMoon24Regular />,
    pipelines: [
      {
        name: 'Mobile Eco Saver',
        iconName: 'WeatherMoon24',
        isExclusive: true,
        trigger: { $type: 'acAdapterDisconnected' },
        steps: [
          { $type: 'powerMode', powerMode: 'Quiet' },
          { $type: 'refreshRate', refreshRate: 60 }
        ]
      }
    ]
  },
  {
    key: 'acPerformance',
    titleKey: 'automation.presetAcPerformanceTitle',
    descKey: 'automation.presetAcPerformanceDesc',
    icon: <Flash24Regular />,
    pipelines: [
      {
        name: 'AC High Performance',
        iconName: 'Flash24',
        isExclusive: true,
        trigger: { $type: 'acAdapterConnected' },
        steps: [
          { $type: 'powerMode', powerMode: 'Performance' }
        ]
      }
    ]
  },
  {
    key: 'gameBooster',
    titleKey: 'automation.presetGameBoosterTitle',
    descKey: 'automation.presetGameBoosterDesc',
    icon: <Rocket24Regular />,
    pipelines: [
      {
        name: 'Game Auto Boost',
        iconName: 'TopSpeed24',
        isExclusive: true,
        trigger: { $type: 'gamesAreRunning' },
        steps: [
          { $type: 'powerMode', powerMode: 'Performance' }
        ]
      },
      {
        name: 'Game Exit Balance',
        iconName: 'TopSpeed24',
        isExclusive: true,
        trigger: { $type: 'gamesStop' },
        steps: [
          { $type: 'powerMode', powerMode: 'Balance' }
        ]
      }
    ]
  },
  {
    key: 'battery80Notice',
    titleKey: 'automation.presetBattery80Title',
    descKey: 'automation.presetBattery80Desc',
    icon: <BatteryCharge24Regular />,
    pipelines: [
      {
        name: 'Battery 80% Reminder',
        iconName: 'BatteryCharge24',
        isExclusive: true,
        trigger: {
          $type: 'batteryPercentage',
          comparison: 'AboveOrEqual',
          threshold: 80,
          chargeFilter: 'Charging'
        },
        steps: [
          {
            $type: 'notification',
            title: 'UDT Battery Guardian',
            message: 'Battery level reached 80%. Consider unplugging to preserve battery health.'
          }
        ]
      }
    ]
  }
]

export default function AutomationPresetModal({
  pipelines,
  onImportPipelines,
  onClose
}: AutomationPresetModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [activeTab, setActiveTab] = useState<'presets' | 'export' | 'import'>('presets')
  const [importText, setImportText] = useState('')

  const handleApplyPreset = (preset: TemplatePreset): void => {
    const generated: AutomationPipeline[] = preset.pipelines.map((p) => ({
      ...p,
      id: crypto.randomUUID()
    }))
    onImportPipelines(generated, false)
    void message.success(t('automation.presetAppliedSuccess', { defaultValue: 'Preset applied successfully!' }))
    onClose()
  }

  const exportJson = JSON.stringify(pipelines, null, 2)

  const handleCopyExport = (): void => {
    void navigator.clipboard.writeText(exportJson).then(() => {
      void message.success(t('automation.exportCopied', { defaultValue: 'Export JSON copied to clipboard!' }))
    })
  }

  const handleDownloadExport = (): void => {
    try {
      const blob = new Blob([exportJson], { type: 'application/json' })
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = 'udt-automation-pipelines.json'
      a.click()
      URL.revokeObjectURL(url)
      void message.success(t('automation.exportDownloaded', { defaultValue: 'Export file downloaded!' }))
    } catch {
      handleCopyExport()
    }
  }

  const handleImportText = (replace = false): void => {
    if (!importText.trim()) {
      void message.error(t('automation.importEmptyError', { defaultValue: 'Please enter JSON content.' }))
      return
    }
    try {
      const parsed = JSON.parse(importText.trim())
      const items = Array.isArray(parsed) ? parsed : [parsed]
      if (items.length === 0) {
        void message.error(t('automation.importInvalidError', { defaultValue: 'Invalid pipeline configuration format.' }))
        return
      }
      const newPipelines: AutomationPipeline[] = items.map((item: Partial<AutomationPipeline>) => ({
        ...item,
        id: crypto.randomUUID(),
        name: typeof item.name === 'string' ? item.name : 'Imported Pipeline',
        steps: Array.isArray(item.steps) ? item.steps : []
      }))
      onImportPipelines(newPipelines, replace)
      void message.success(t('automation.importSuccess', { defaultValue: 'Successfully imported pipelines!' }))
      onClose()
    } catch {
      void message.error(t('automation.importJsonError', { defaultValue: 'Failed to parse JSON. Please check syntax.' }))
    }
  }

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>): void => {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = (event) => {
      const content = String(event.target?.result ?? '')
      setImportText(content)
    }
    reader.readAsText(file)
  }

  return (
    <AutomationModal
      title={t('automation.presetsAndSharing', { defaultValue: 'Presets & Configuration Sharing' })}
      onClose={onClose}
      wide
    >
      <div className="udt-preset-modal">
        <div className="udt-preset-tabs">
          <button
            type="button"
            className={`udt-preset-tab ${activeTab === 'presets' ? 'active' : ''}`}
            onClick={() => setActiveTab('presets')}
          >
            {t('automation.tabPresets', { defaultValue: 'Recommended Presets' })}
          </button>
          <button
            type="button"
            className={`udt-preset-tab ${activeTab === 'export' ? 'active' : ''}`}
            onClick={() => setActiveTab('export')}
          >
            <ArrowExport24Regular /> {t('automation.tabExport', { defaultValue: 'Export' })}
          </button>
          <button
            type="button"
            className={`udt-preset-tab ${activeTab === 'import' ? 'active' : ''}`}
            onClick={() => setActiveTab('import')}
          >
            <ArrowImport24Regular /> {t('automation.tabImport', { defaultValue: 'Import' })}
          </button>
        </div>

        <div className="udt-preset-content">
          {activeTab === 'presets' && (
            <div className="udt-preset-list">
              {TEMPLATES.map((tpl) => (
                <div key={tpl.key} className="udt-preset-card">
                  <div className="udt-preset-card__icon">{tpl.icon}</div>
                  <div className="udt-preset-card__info">
                    <div className="udt-preset-card__title">
                      {t(tpl.titleKey, { defaultValue: tpl.pipelines[0]?.name ?? tpl.key })}
                    </div>
                    <div className="udt-preset-card__desc">
                      {t(tpl.descKey, { defaultValue: 'Applies recommended action pipeline.' })}
                    </div>
                  </div>
                  <button
                    type="button"
                    className="udt-btn udt-btn--primary udt-btn--sm"
                    onClick={() => handleApplyPreset(tpl)}
                  >
                    {t('automation.applyPreset', { defaultValue: 'Apply' })}
                  </button>
                </div>
              ))}
            </div>
          )}

          {activeTab === 'export' && (
            <div className="udt-export-panel">
              <p className="udt-panel-hint">
                {t('automation.exportHint', {
                  defaultValue: 'Share your automation pipelines with other users or save a backup file.'
                })}
              </p>
              <textarea
                className="udt-textarea"
                readOnly
                value={exportJson}
                rows={10}
                style={{ fontFamily: 'monospace', fontSize: 11 }}
              />
              <div className="udt-panel-actions">
                <button type="button" className="udt-btn udt-btn--secondary" onClick={handleCopyExport}>
                  <Copy24Regular /> {t('automation.copyJson', { defaultValue: 'Copy JSON' })}
                </button>
                <button type="button" className="udt-btn udt-btn--primary" onClick={handleDownloadExport}>
                  <ArrowDownload24Regular /> {t('automation.downloadFile', { defaultValue: 'Save as File' })}
                </button>
              </div>
            </div>
          )}

          {activeTab === 'import' && (
            <div className="udt-import-panel">
              <p className="udt-panel-hint">
                {t('automation.importHint', {
                  defaultValue: 'Paste exported JSON content or upload a .json file.'
                })}
              </p>
              <div style={{ marginBottom: 8 }}>
                <input type="file" accept=".json,application/json" onChange={handleFileUpload} />
              </div>
              <textarea
                className="udt-textarea"
                placeholder={t('automation.importPlaceholder', { defaultValue: 'Paste JSON content here...' })}
                value={importText}
                onChange={(e) => setImportText(e.target.value)}
                rows={8}
                style={{ fontFamily: 'monospace', fontSize: 11 }}
              />
              <div className="udt-panel-actions">
                <button type="button" className="udt-btn udt-btn--secondary" onClick={() => handleImportText(false)}>
                  {t('automation.importAppend', { defaultValue: 'Import & Append' })}
                </button>
                <button type="button" className="udt-btn udt-btn--danger" onClick={() => handleImportText(true)}>
                  {t('automation.importReplaceAll', { defaultValue: 'Replace All' })}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </AutomationModal>
  )
}
