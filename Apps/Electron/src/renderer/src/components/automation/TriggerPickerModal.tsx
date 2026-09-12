/**
 * Trigger picker for new automatic pipelines - port of Electron
 * Windows/Automation/CreateAutomationPipelineWindow.xaml.cs.
 */
import { useMemo, useState } from 'react'
import { CheckboxChecked24Regular, Add24Regular } from '../icons/fluent'
import { ChevronRight16Regular } from '@fluentui/react-icons'
import { useTranslation } from 'react-i18next'
import type { AutomationTrigger } from './triggers'
import { TRIGGER_DEFINITIONS } from './triggers'
import { triggerIcon } from './triggerMeta'
import AutomationModal from './AutomationModal'

export interface TriggerPickerModalProps {
  /** Kinds already used by existing automatic pipelines (disallow-duplicates gate). */
  existingKinds: string[]
  onPick: (trigger: AutomationTrigger) => void
  onCancel: () => void
}

export default function TriggerPickerModal(props: TriggerPickerModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [multiSelect, setMultiSelect] = useState(false)
  const [checked, setChecked] = useState<Set<number>>(new Set())

  const definitions = useMemo(() => TRIGGER_DEFINITIONS, [])

  const toggleChecked = (index: number): void => {
    setChecked((prev) => {
      const next = new Set(prev)
      if (next.has(index)) {
        next.delete(index)
      } else {
        next.add(index)
      }
      return next
    })
  }

  const pick = (index: number): void => {
    if (!multiSelect) {
      props.onPick(definitions[index].createDefault())
      return
    }
    toggleChecked(index)
  }

  const createComposite = (): void => {
    const triggers = [...checked]
      .sort((a, b) => a - b)
      .map((index) => definitions[index].createDefault())
    if (triggers.length === 0) return
    props.onPick(triggers.length === 1 ? triggers[0] : { $type: 'and', triggers })
  }

  const isDisabled = (kind: string, index: number): boolean => {
    if (multiSelect) return false
    const definition = definitions[index]
    return definition.disallowDuplicates && props.existingKinds.includes(kind)
  }

  return (
    <AutomationModal
      title={t('automation.triggerPicker.title')}
      onClose={props.onCancel}
      wide
      actions={
        <>
          <button type="button" className="udt-btn udt-btn--secondary" onClick={props.onCancel}>
            {t('common.cancel', { defaultValue: 'Cancel' })}
          </button>
          {multiSelect && (
            <button
              type="button"
              className="udt-btn udt-btn--primary"
              disabled={checked.size === 0}
              onClick={createComposite}
            >
              <Add24Regular /> {t('common.confirm', { defaultValue: 'Create' })}
            </button>
          )}
        </>
      }
    >
      <div className="udt-modal__scroll">
        {!multiSelect && (
          <button
            type="button"
            className="udt-trigger-card"
            onClick={() => setMultiSelect(true)}
          >
            <span className="udt-trigger-card__icon">
              <CheckboxChecked24Regular />
            </span>
            <span className="udt-trigger-card__copy">
              <span className="udt-trigger-card__title">
                {t('wpf.multipleTriggersAutomationPipelineTriggerdisplayName', { defaultValue: 'Multiple triggers' })}
              </span>
            </span>
            <ChevronRight16Regular className="udt-trigger-card__chevron" />
          </button>
        )}
        {definitions.map((definition, index) => {
          const disabled = isDisabled(definition.kind, index)
          const isChecked = checked.has(index)
          return (
            <button
              key={`${definition.kind}-${definition.nameKey}-${index}`}
              type="button"
              className={`udt-trigger-card${disabled ? ' udt-trigger-card--disabled' : ''}${isChecked ? ' udt-trigger-card--checked' : ''}`}
              disabled={disabled}
              onClick={() => pick(index)}
            >
              <span className="udt-trigger-card__icon">
                {triggerIcon(definition.kind) ?? <Add24Regular />}
              </span>
              <span className="udt-trigger-card__copy">
                <span className="udt-trigger-card__title">
                  {definition.wpfKey != null
                    ? t(`wpf.${definition.wpfKey}`, { defaultValue: t(`automation.triggerNames.${definition.nameKey}`) })
                    : t(`automation.triggerNames.${definition.nameKey}`)}
                </span>
              </span>
              {multiSelect ? (
                <input
                  type="checkbox"
                  className="udt-trigger-card__checkbox"
                  checked={isChecked}
                  onChange={() => toggleChecked(index)}
                  onClick={(e) => e.stopPropagation()}
                />
              ) : (
                <ChevronRight16Regular className="udt-trigger-card__chevron" />
              )}
            </button>
          )
        })}
      </div>
    </AutomationModal>
  )
}
