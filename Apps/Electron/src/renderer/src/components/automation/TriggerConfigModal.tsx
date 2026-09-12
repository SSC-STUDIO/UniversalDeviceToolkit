/**
 * Trigger configuration for an existing pipeline — port of Electron
 * Windows/Automation/AutomationPipelineTriggerConfigurationWindow.xaml.cs.
 *
 * Tabs are seeded from the pipeline's current triggers (single or composite);
 * Save merges the configured tabs back into a single or `and` composite.
 */
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { AutomationTrigger } from './triggers'
import { composeTriggers, flattenTriggers, isTriggerValid, normalizeTriggerKind, triggerDisplayNameKey } from './triggers'
import { triggerIcon } from './triggerMeta'
import AutomationModal from './AutomationModal'
import {
  BatteryPercentageEditor,
  DeviceEditor,
  GodModePresetEditor,
  HardwareSensorEditor,
  PeriodicEditor,
  PowerModeEditor,
  ProcessesEditor,
  TimeEditor,
  UserInactivityEditor,
  WiFiEditor,
} from './TriggerEditors'

export interface TriggerConfigModalProps {
  trigger: AutomationTrigger
  onSave: (trigger: AutomationTrigger) => void
  onCancel: () => void
}

interface EditableTab {
  key: string
  trigger: AutomationTrigger
  label: string
}

const EDITOR_BY_KIND: Record<string, (props: { trigger: AutomationTrigger; onChange: (next: AutomationTrigger) => void }) => React.JSX.Element> = {
  powerMode: PowerModeEditor,
  godModePresetChanged: GodModePresetEditor,
  periodic: PeriodicEditor,
  processesAreRunning: ProcessesEditor,
  processesStopRunning: ProcessesEditor,
  time: TimeEditor,
  userInactivity: UserInactivityEditor,
  wiFiConnected: WiFiEditor,
  hardwareSensor: HardwareSensorEditor,
  batteryPercentage: BatteryPercentageEditor,
  deviceConnected: DeviceEditor,
  deviceDisconnected: DeviceEditor,
}

export default function TriggerConfigModal(props: TriggerConfigModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const children = useMemo(() => flattenTriggers(props.trigger), [props.trigger])
  const [tabs, setTabs] = useState<EditableTab[]>(() =>
    children
      .map((child) => ({
        key: child.$type,
        trigger: child,
        label: t(triggerDisplayNameKey(child), { defaultValue: child.$type }),
      }))
      .filter((tab) => isTriggerValid(tab.trigger) && EDITOR_BY_KIND[normalizeTriggerKind(tab.trigger.$type) ?? ''] != null)
  )
  const [active, setActive] = useState(0)

  if (tabs.length === 0) {
    return (
      <AutomationModal
        title={t('automation.triggerConfig.title')}
        onClose={props.onCancel}
        actions={
          <button type="button" className="udt-btn udt-btn--secondary" onClick={props.onCancel}>
            {t('common.cancel', { defaultValue: 'Cancel' })}
          </button>
        }
      >
        <div className="udt-trigger-field__empty">
          {t('automation.triggerConfig.noEditableTriggers', { defaultValue: 'This trigger has no configurable parameters.' })}
        </div>
      </AutomationModal>
    )
  }

  const updateTab = (index: number, next: AutomationTrigger): void => {
    setTabs((prev) => prev.map((tab, i) => (i === index ? { ...tab, trigger: next } : tab)))
  }

  const handleSave = (): void => {
    const result = composeTriggers(tabs.map((tab) => tab.trigger))
    if (result != null) props.onSave(result)
  }

  const activeTab = tabs[Math.min(active, tabs.length - 1)]
  const Editor = EDITOR_BY_KIND[normalizeTriggerKind(activeTab.trigger.$type) ?? '']

  return (
    <AutomationModal
      title={t('automation.triggerConfig.title')}
      onClose={props.onCancel}
      wide
      actions={
        <>
          <button type="button" className="udt-btn udt-btn--secondary" onClick={props.onCancel}>
            {t('common.cancel', { defaultValue: 'Cancel' })}
          </button>
          <button type="button" className="udt-btn udt-btn--primary" onClick={handleSave}>
            {t('common.save', { defaultValue: 'Save' })}
          </button>
        </>
      }
    >
      <div className="udt-trigger-tabs">
        {tabs.map((tab, index) => (
          <button
            key={`${tab.key}-${index}`}
            type="button"
            className={`udt-trigger-tab${index === Math.min(active, tabs.length - 1) ? ' udt-trigger-tab--active' : ''}`}
            onClick={() => setActive(index)}
          >
            {triggerIcon(tab.trigger.$type)}
            <span>{tab.label}</span>
          </button>
        ))}
      </div>
      <div className="udt-modal__scroll">
        {Editor != null && (
          <Editor trigger={activeTab.trigger} onChange={(next) => updateTab(Math.min(active, tabs.length - 1), next)} />
        )}
      </div>
    </AutomationModal>
  )
}
