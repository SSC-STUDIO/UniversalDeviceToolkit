import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Select } from 'antd'
import type { AutomationPipeline, AutomationStepType } from '../../api/automation'
import { featuresApi } from '../../api/features'
import {
  getStepDef,
  normalizeState,
  stateLabel,
  statesEqual,
  type StepOption,
  type StepState
} from './steps'
import AutomationModal from './AutomationModal'

export interface StepEditorModalProps {
  step: AutomationStepType
  /** All pipelines of the current configuration (used by the Quick Action step). */
  pipelines: AutomationPipeline[]
  onApply: (next: AutomationStepType) => void
  onCancel: () => void
}

function optionKey(state: StepState): string {
  return typeof state === 'string' ? state : JSON.stringify(state)
}

/**
 * Modal parameter editor for a single automation step. Mirrors the Electron step
 * controls: combo-box backed steps load their states from the host
 * (feature.getStates, equivalent to GetAllStatesAsync) with a static enum
 * fallback; notification/play-sound/quick-action get dedicated editors.
 */
export default function StepEditorModal({ step, pipelines, onApply, onCancel }: StepEditorModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const def = getStepDef(String(step.$type))
  const fileInputRef = useRef<HTMLInputElement>(null)

  const [options, setOptions] = useState<StepOption[]>(def?.staticOptions ?? [])
  const [optionsLoading, setOptionsLoading] = useState(false)
  const [value, setValue] = useState<StepState | string | null>(() => {
    if (!def) return null
    switch (def.kind) {
      case 'select': {
        const raw = step.state
        if (raw === undefined || raw === null) return def.staticOptions?.[0]?.value ?? ''
        return normalizeState(raw)
      }
      case 'text':
        return typeof step.text === 'string' ? step.text : ''
      case 'file':
        return typeof step.path === 'string' ? step.path : ''
      case 'pipeline':
        return typeof step.pipelineId === 'string' ? step.pipelineId : ''
    }
  })

  useEffect(() => {
    if (!def || def.kind !== 'select' || !def.featureKey) return
    let cancelled = false
    setOptionsLoading(true)
    featuresApi
      .getStates(def.featureKey)
      .then((result) => {
        if (cancelled) return
        const fetched = (result.states ?? [])
          .map((raw) => ({ value: normalizeState(raw) }))
          .filter((option) => option.value !== '')
        setOptions(fetched.length > 0 ? fetched : (def.staticOptions ?? []))
      })
      .catch(() => {
        if (!cancelled) setOptions(def.staticOptions ?? [])
      })
      .finally(() => {
        if (!cancelled) setOptionsLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [def])

  if (!def) {
    return (
      <AutomationModal
        title={t('automation.addStep')}
        onClose={onCancel}
        actions={
          <button type="button" className="udt-btn udt-btn--secondary" onClick={onCancel}>
            {t('common.cancel', { defaultValue: '取消' })}
          </button>
        }
      >
        <div className="udt-step-editor__desc">{String(step.$type)}</div>
      </AutomationModal>
    )
  }

  const quickActionPipelines = pipelines.filter((p) => p.trigger == null)

  const effectiveOptions: StepOption[] =
    def.kind === 'select' && value !== ''
      ? [{ value: value as StepState }, ...options]
      : options

  const uniqueOptions = effectiveOptions.filter(
    (option, index, all) => all.findIndex((other) => statesEqual(other.value, option.value)) === index,
  )

  const handleSelect = (key: string): void => {
    const option = uniqueOptions.find((o) => optionKey(o.value) === key)
    if (option) setValue(option.value)
  }

  const handleFilePicked = (file: File | undefined): void => {
    if (!file) return
    const withPath = file as File & { path?: string }
    setValue(withPath.path ?? file.name)
  }

  const handleBrowseFile = async (): Promise<void> => {
    const bridge = window.bridge
    if (bridge?.selectAudioFile != null) {
      try {
        const filePath = await bridge.selectAudioFile()
        if (filePath) setValue(filePath)
      } catch {
        // dialog unavailable; fall back to the hidden input
        fileInputRef.current?.click()
      }
      return
    }
    fileInputRef.current?.click()
  }

  const canSave =
    def.kind === 'pipeline'
      ? quickActionPipelines.some((p) => p.id === value) || value === ''
      : def.kind === 'select'
        ? value !== ''
        : true

  const handleApply = (): void => {
    const $type = def.discriminator
    switch (def.kind) {
      case 'select':
        onApply({ $type, state: value as StepState })
        break
      case 'text':
        onApply({ $type, text: (value as string).trim() || null })
        break
      case 'file':
        onApply({ $type, path: value ? String(value) : null })
        break
      case 'pipeline':
        onApply({ $type, pipelineId: value ? String(value) : null })
        break
    }
  }

  return (
    <AutomationModal
      title={t(`automation.stepEditors.${def.i18nKey}.title`)}
      onClose={onCancel}
      actions={
        <>
          <button type="button" className="udt-btn udt-btn--secondary" onClick={onCancel}>
            {t('common.cancel', { defaultValue: '取消' })}
          </button>
          <button type="button" className="udt-btn udt-btn--primary" disabled={!canSave} onClick={handleApply}>
            {t('common.confirm', { defaultValue: '确定' })}
          </button>
        </>
      }
    >
      <div className="udt-step-editor__desc">{t(`automation.stepEditors.${def.i18nKey}.desc`)}</div>

      <div className="udt-step-editor__body">
        {def.kind === 'select' && (
          <>
            <Select
              className="udt-select"
              value={uniqueOptions.length === 0 ? undefined : optionKey(value as StepState)}
              loading={optionsLoading}
              placeholder={optionsLoading ? t('automation.optionsLoading') : undefined}
              onChange={handleSelect}
              options={uniqueOptions.map((option) => ({
                value: optionKey(option.value),
                label: option.labelText ?? stateLabel(option.value, def, t)
              }))}
            />
            {!optionsLoading && uniqueOptions.length === 0 && (
              <div className="udt-step-editor__empty">{t(`automation.stepEditors.${def.i18nKey}.empty`)}</div>
            )}
          </>
        )}

        {def.kind === 'text' && (
          <input
            autoFocus
            className="udt-input"
            value={(value as string) ?? ''}
            placeholder={t(`automation.stepEditors.${def.i18nKey}.placeholder`)}
            onChange={(e) => setValue(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') handleApply()
            }}
          />
        )}

        {def.kind === 'file' && (
          <div className="udt-file-row">
            <span className="udt-file-row__name" title={(value as string) ?? ''}>
              {(value as string) || t(`automation.stepEditors.${def.i18nKey}.none`)}
            </span>
            <button
              type="button"
              className="udt-btn udt-btn--secondary"
              onClick={() => void handleBrowseFile()}
            >
              {t(`automation.stepEditors.${def.i18nKey}.browse`)}
            </button>
            <input
              ref={fileInputRef}
              type="file"
              accept="audio/*,.wav,.mp3,.ogg,.flac,.aac,.m4a"
              style={{ display: 'none' }}
              onChange={(e) => handleFilePicked(e.target.files?.[0])}
            />
          </div>
        )}

        {def.kind === 'pipeline' && (
          <>
            <Select
              className="udt-select"
              value={(value as string) || undefined}
              placeholder={t(`automation.stepEditors.${def.i18nKey}.placeholder`)}
              onChange={(next) => setValue(next ?? null)}
              options={quickActionPipelines.map((pipeline) => ({
                value: pipeline.id,
                label: pipeline.name ?? t('automation.quickAction')
              }))}
              allowClear
            />
            {quickActionPipelines.length === 0 && (
              <div className="udt-step-editor__empty">{t(`automation.stepEditors.${def.i18nKey}.empty`)}</div>
            )}
          </>
        )}
      </div>
    </AutomationModal>
  )
}
