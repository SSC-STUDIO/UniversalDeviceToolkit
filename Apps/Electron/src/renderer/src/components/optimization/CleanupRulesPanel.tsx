import { useTranslation } from 'react-i18next'
import { Delete24Regular, Edit24Regular, FolderAdd24Regular } from '../icons/fluent'
import { message } from 'antd'
import { useEffect, useState } from 'react'
import { optimizationApi } from '../../api/optimization'
import { localizeHostError } from '../../api/bridge'
import { useCleanupStore } from '../../stores/cleanupStore'
import EmptyState from '../EmptyState'
import { resolveActionError, shouldShowEmptyPlaceholder } from '../../utils/optimizationPresentation'
import './optimization.css'

/**
 * Custom cleanup rules card (WindowsOptimizationPage_CustomCleanup_*).
 * Mirrors WindowsOptimizationPage.Cleanup.cs: add/edit via folder picker,
 * remove per rule, clear all — persisted to the host settings store.
 */
export default function CleanupRulesPanel(): React.JSX.Element {
  const { t } = useTranslation()
  const rules = useCleanupStore((s) => s.rules)
  const loading = useCleanupStore((s) => s.loading)
  const load = useCleanupStore((s) => s.load)
  const addRule = useCleanupStore((s) => s.addRule)
  const updateRulePath = useCleanupStore((s) => s.updateRulePath)
  const removeRule = useCleanupStore((s) => s.removeRule)
  const clearRules = useCleanupStore((s) => s.clearRules)
  const cleanupError = useCleanupStore((s) => s.error)
  const loaded = useCleanupStore((s) => s.loaded)
  const [busyRuleId, setBusyRuleId] = useState<string | null>(null)

  const reportCleanupError = (error: string | null | undefined): void => {
    void message.error(
      localizeHostError(resolveActionError(error, t('optimization.cleanupFailed')), t)
    )
  }

  useEffect(() => {
    void load()
  }, [load])

  const pickFolder = async (): Promise<string | null> => {
    try {
      return await optimizationApi.selectFolder()
    } catch {
      message.error(t('optimization.cleanup.custom.folderPickerFailed'))
      return null
    }
  }

  const handleAdd = async (): Promise<void> => {
    const path = await pickFolder()
    if (!path) return
    const ok = await addRule(path)
    if (ok) message.success(t('optimization.cleanup.custom.added'))
    else reportCleanupError(useCleanupStore.getState().error)
  }

  const handleEdit = async (id: string): Promise<void> => {
    const rule = rules.find((r) => r.id === id)
    if (!rule) return
    setBusyRuleId(id)
    try {
      const path = await pickFolder()
      if (!path) return
      const ok = await updateRulePath(id, path)
      if (ok) message.success(t('optimization.cleanup.custom.updated'))
      else reportCleanupError(useCleanupStore.getState().error)
    } finally {
      setBusyRuleId(null)
    }
  }

  const handleRemove = async (id: string): Promise<void> => {
    const ok = await removeRule(id)
    if (!ok) reportCleanupError(useCleanupStore.getState().error)
  }

  const handleClear = async (): Promise<void> => {
    const ok = await clearRules()
    if (!ok) reportCleanupError(useCleanupStore.getState().error)
  }

  const header = t('wpf.windowsOptimizationPagecustomCleanupheader', {
    defaultValue: t('optimization.cleanup.custom.header')
  })
  const emptyTitle = t('wpf.windowsOptimizationPagecustomCleanupempty', {
    defaultValue: t('optimization.cleanup.custom.empty')
  })
  const addLabel = t('wpf.windowsOptimizationPagecustomCleanupadd', {
    defaultValue: t('optimization.cleanup.custom.add')
  })
  const clearLabel = t('wpf.windowsOptimizationPagecustomCleanupclear', {
    defaultValue: t('optimization.cleanup.custom.clear')
  })

  return (
    <div className="udt-card udt-side-card udt-cleanup-rules">
      <div className="udt-card__title">{header}</div>

      {loading && <div className="udt-skeleton-list"><div className="udt-skeleton-card" /></div>}

      {rules.length > 0 && (
        <div className="udt-cleanup-rules__list">
          {rules.map((rule) => (
            <div key={rule.id} className="udt-cleanup-rules__item">
              <div className="udt-cleanup-rules__item-main">
                <div className="udt-cleanup-rules__item-path" title={rule.directoryPath}>
                  {rule.directoryPath}
                </div>
                <div className="udt-cleanup-rules__item-extensions">
                  {rule.extensions.length > 0
                    ? rule.extensions.join(', ')
                    : t('optimization.cleanup.custom.noExtensions')}
                </div>
                {rule.recursive && (
                  <div className="udt-cleanup-rules__item-recursive">
                    {t('wpf.windowsOptimizationPagecustomCleanuprecursivelabel', {
                      defaultValue: t('optimization.cleanup.custom.recursive')
                    })}
                  </div>
                )}
              </div>
              <div className="udt-cleanup-rules__item-actions">
                <button
                  type="button"
                  className="udt-action-btn"
                  title={t('wpf.windowsOptimizationPagecustomCleanupedit', {
                    defaultValue: t('optimization.cleanup.custom.edit')
                  })}
                  disabled={busyRuleId === rule.id}
                  onClick={() => void handleEdit(rule.id)}
                >
                  <Edit24Regular />
                </button>
                <button
                  type="button"
                  className="udt-action-btn udt-action-btn--danger"
                  title={t('wpf.windowsOptimizationPagecustomCleanupremove', {
                    defaultValue: t('optimization.cleanup.custom.remove')
                  })}
                  onClick={() => void handleRemove(rule.id)}
                >
                  <Delete24Regular />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {shouldShowEmptyPlaceholder({
        loading,
        itemCount: rules.length,
        error: cleanupError,
        loaded
      }) && (
        <EmptyState
          className="udt-cleanup-rules__empty"
          icon={<Delete24Regular />}
          title={emptyTitle}
        />
      )}

      {cleanupError && <div className="udt-page-error">{cleanupError}</div>}

      <div className="udt-side-card__actions">
        <button type="button" className="udt-btn udt-btn--secondary" onClick={() => void handleAdd()}>
          <FolderAdd24Regular /> {addLabel}
        </button>
        <button
          type="button"
          className="udt-btn udt-btn--secondary"
          disabled={rules.length === 0}
          onClick={() => void handleClear()}
        >
          {clearLabel}
        </button>
      </div>
    </div>
  )
}
