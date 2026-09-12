import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import {
  ArrowDown24Regular,
  ArrowUp24Regular,
  Checkmark24Regular,
  Delete24Regular,
  Edit24Regular
} from '../icons/fluent'
import { Input, Modal, Spin } from 'antd'
import type { TFunction } from 'i18next'
import { useTranslation } from 'react-i18next'
import { dashboardApi, type DashboardConfig, type DashboardGroup, type DashboardItem } from '../../api/dashboard'
import { DEFAULT_DASHBOARD_GROUPS, dashboardItemLabel } from './dashboardItems'
import AddDashboardItemModal from './AddDashboardItemModal'
import InfoBar from '../InfoBar'

/**
 * Parity modal for Electron Windows/Dashboard/EditDashboardWindow:
 * sensors toggle, group list with add / rename / delete / move, per-item
 * visibility (checkbox), per-item move/delete, an add-item picker and a
 * "Default" reset that restores the built-in groups.
 */
interface EditDashboardModalProps {
  config: DashboardConfig
  onCancel: () => void
  onSaved: (config: DashboardConfig) => void
}

function groupTitle(group: DashboardGroup, t: TFunction): string {
  if (group.type === 'Custom' && group.customName) return group.customName
  return t(`dashboard.group.${group.type.toLowerCase()}`, { defaultValue: group.type })
}

interface NamePromptState {
  mode: 'add' | 'rename'
  groupIndex: number
}

export default function EditDashboardModal({
  config,
  onCancel,
  onSaved
}: EditDashboardModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [showSensors, setShowSensors] = useState(config.showSensors !== false)
  const [groups, setGroups] = useState<DashboardGroup[]>(
    config.groups != null && config.groups.length > 0 ? config.groups : DEFAULT_DASHBOARD_GROUPS
  )
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [addItemGroup, setAddItemGroup] = useState<number | null>(null)
  const [namePrompt, setNamePrompt] = useState<NamePromptState | null>(null)
  const [nameInput, setNameInput] = useState('')

  // Electron shows a short loading state before revealing the editor.
  useEffect(() => {
    const timer = window.setTimeout(() => setLoading(false), 500)
    return () => window.clearTimeout(timer)
  }, [])

  const allUsedItems = (): DashboardItem[] => groups.flatMap((group) => group.items)

  const toggleItem = (groupIndex: number, item: DashboardItem): void => {
    setGroups((prev) =>
      prev.map((group, index) => {
        if (index !== groupIndex) return group
        const visible = group.items.includes(item)
        return {
          ...group,
          items: visible
            ? group.items.filter((existing) => existing !== item)
            : [...group.items, item]
        }
      })
    )
  }

  const moveGroup = (groupIndex: number, direction: -1 | 1): void => {
    setGroups((prev) => {
      const target = groupIndex + direction
      if (target < 0 || target >= prev.length) return prev
      const next = [...prev]
      const [group] = next.splice(groupIndex, 1)
      next.splice(target, 0, group)
      return next
    })
  }

  const deleteGroup = (groupIndex: number): void => {
    setGroups((prev) => prev.filter((_, index) => index !== groupIndex))
  }

  const moveItem = (groupIndex: number, itemIndex: number, direction: -1 | 1): void => {
    setGroups((prev) =>
      prev.map((group, index) => {
        if (index !== groupIndex) return group
        const target = itemIndex + direction
        if (target < 0 || target >= group.items.length) return group
        const items = [...group.items]
        const [item] = items.splice(itemIndex, 1)
        items.splice(target, 0, item)
        return { ...group, items }
      })
    )
  }

  const deleteItem = (groupIndex: number, item: DashboardItem): void => {
    setGroups((prev) =>
      prev.map((group, index) =>
        index === groupIndex
          ? { ...group, items: group.items.filter((existing) => existing !== item) }
          : group
      )
    )
  }

  const openNamePrompt = (mode: 'add' | 'rename', groupIndex: number, currentName: string): void => {
    setNameInput(mode === 'rename' ? currentName : '')
    setNamePrompt({ mode, groupIndex })
  }

  const confirmNamePrompt = (): void => {
    if (namePrompt == null) return
    const name = nameInput.trim()
    setNamePrompt(null)
    if (name.length === 0) return
    if (namePrompt.mode === 'add') {
      setGroups((prev) => [...prev, { type: 'Custom', customName: name, items: [] }])
      return
    }
    setGroups((prev) =>
      prev.map((group, index) =>
        index === namePrompt.groupIndex
          ? { ...group, type: 'Custom', customName: name }
          : group
      )
    )
  }

  const addItem = (groupIndex: number, item: DashboardItem): void => {
    setGroups((prev) =>
      prev.map((group, index) =>
        index === groupIndex ? { ...group, items: [...group.items, item] } : group
      )
    )
  }

  const handleApply = async (nextGroups: DashboardGroup[], nextShowSensors: boolean): Promise<void> => {
    setSaving(true)
    setError(null)
    const nextConfig: DashboardConfig = {
      showSensors: nextShowSensors,
      groups: nextGroups,
      sensorsRefreshIntervalSeconds: config.sensorsRefreshIntervalSeconds
    }
    try {
      const result = await dashboardApi.saveConfig(nextConfig)
      if (result.saved !== true) {
        setError(t('dashboard.edit.saveFailed', {
          defaultValue: 'Dashboard configuration was not saved.'
        }))
        return
      }
      onSaved(nextConfig)
    } catch (reason) {
      setError((reason as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const handleDefault = (): void => {
    void handleApply(DEFAULT_DASHBOARD_GROUPS, true)
  }

  const handleCancel = (): void => {
    setNamePrompt(null)
    setAddItemGroup(null)
    onCancel()
  }

  return createPortal(
    <div className="udt-dashboard-edit-backdrop" onClick={handleCancel}>
      <div
        className="udt-dashboard-edit"
        role="dialog"
        aria-modal="true"
        aria-label={t('dashboard.edit.title')}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="udt-dashboard-edit__title">{t('dashboard.edit.title')}</div>
        <div className="udt-dashboard-edit__description">{t('dashboard.edit.description')}</div>
        <InfoBar
          severity="informational"
          title={t('dashboard.edit.disclaimer')}
          className="udt-dashboard-edit__info"
        />

        {loading ? (
          <div className="udt-dashboard-edit__loading">
            <Spin size="large" />
          </div>
        ) : (
          <>
            <label className="udt-dashboard-edit__row">
              <span className="udt-dashboard-edit__row-copy">
                <span className="udt-dashboard-edit__row-title">{t('dashboard.edit.showSensors')}</span>
              </span>
              <input
                type="checkbox"
                className="udt-dashboard-edit__checkbox"
                checked={showSensors}
                onChange={(event) => setShowSensors(event.target.checked)}
              />
              <span className="udt-dashboard-edit__switch" aria-hidden="true" />
            </label>

            <div className="udt-dashboard-edit__groups-label">{t('dashboard.edit.groups')}</div>
            <div className="udt-dashboard-edit__groups">
              {groups.map((group, groupIndex) => (
                <div key={`${group.type}-${groupIndex}`} className="udt-dashboard-edit__group">
                  <div className="udt-dashboard-edit__group-head">
                    <span className="udt-dashboard-edit__group-title">
                      {groupTitle(group, t)}
                      <span className="udt-dashboard-edit__group-count">{group.items.length}</span>
                    </span>
                    <span className="udt-dashboard-edit__group-actions">
                      <button
                        type="button"
                        className="udt-dashboard-edit__icon-btn"
                        title={t('dashboard.edit.renameGroup')}
                        onClick={() => openNamePrompt('rename', groupIndex, groupTitle(group, t))}
                      >
                        <Edit24Regular />
                      </button>
                      <button
                        type="button"
                        className="udt-dashboard-edit__icon-btn"
                        title={t('dashboard.edit.moveUp')}
                        disabled={groupIndex === 0}
                        onClick={() => moveGroup(groupIndex, -1)}
                      >
                        <ArrowUp24Regular />
                      </button>
                      <button
                        type="button"
                        className="udt-dashboard-edit__icon-btn"
                        title={t('dashboard.edit.moveDown')}
                        disabled={groupIndex === groups.length - 1}
                        onClick={() => moveGroup(groupIndex, 1)}
                      >
                        <ArrowDown24Regular />
                      </button>
                      <button
                        type="button"
                        className="udt-dashboard-edit__icon-btn"
                        title={t('dashboard.edit.deleteGroup')}
                        onClick={() => deleteGroup(groupIndex)}
                      >
                        <Delete24Regular />
                      </button>
                    </span>
                  </div>
                  <div className="udt-dashboard-edit__group-items">
                    {group.items.map((item, itemIndex) => (
                      <div key={item} className="udt-dashboard-edit__item-row">
                        <label className="udt-dashboard-edit__item">
                          <input
                            type="checkbox"
                            checked={group.items.includes(item)}
                            onChange={() => toggleItem(groupIndex, item)}
                          />
                          <span className="udt-dashboard-edit__item-check">
                            <Checkmark24Regular />
                          </span>
                          <span className="udt-dashboard-edit__item-label">
                            {dashboardItemLabel(item, t)}
                          </span>
                        </label>
                        <span className="udt-dashboard-edit__item-actions">
                          <button
                            type="button"
                            className="udt-dashboard-edit__icon-btn"
                            title={t('dashboard.edit.moveUp')}
                            disabled={itemIndex === 0}
                            onClick={() => moveItem(groupIndex, itemIndex, -1)}
                          >
                            <ArrowUp24Regular />
                          </button>
                          <button
                            type="button"
                            className="udt-dashboard-edit__icon-btn"
                            title={t('dashboard.edit.moveDown')}
                            disabled={itemIndex === group.items.length - 1}
                            onClick={() => moveItem(groupIndex, itemIndex, 1)}
                          >
                            <ArrowDown24Regular />
                          </button>
                          <button
                            type="button"
                            className="udt-dashboard-edit__icon-btn"
                            title={t('dashboard.edit.deleteItem')}
                            onClick={() => deleteItem(groupIndex, item)}
                          >
                            <Delete24Regular />
                          </button>
                        </span>
                      </div>
                    ))}
                    <button
                      type="button"
                      className="udt-dashboard-edit__add-item-btn"
                      onClick={() => setAddItemGroup(groupIndex)}
                    >
                      {t('dashboard.edit.addItem')}
                    </button>
                  </div>
                </div>
              ))}
            </div>

            {error != null && <div className="udt-dashboard-edit__error">{error}</div>}

            <div className="udt-dashboard-edit__actions">
              <button
                type="button"
                className="udt-btn udt-btn--secondary"
                onClick={() => openNamePrompt('add', 0, '')}
              >
                {t('dashboard.edit.addGroup')}
              </button>
              <button
                type="button"
                className="udt-btn udt-btn--secondary"
                onClick={handleDefault}
              >
                {t('dashboard.edit.default')}
              </button>
              <button
                type="button"
                className="udt-btn udt-btn--secondary"
                onClick={handleCancel}
              >
                {t('dashboard.edit.cancel')}
              </button>
              <button
                type="button"
                className="udt-btn udt-btn--primary"
                disabled={saving}
                onClick={() => void handleApply(groups, showSensors)}
              >
                {t('dashboard.edit.save')}
              </button>
            </div>
          </>
        )}
      </div>

      {addItemGroup != null && (
        <AddDashboardItemModal
          open
          existingItems={allUsedItems()}
          onAdd={(item) => addItem(addItemGroup, item)}
          onClose={() => setAddItemGroup(null)}
        />
      )}

      <Modal
        centered
        open={namePrompt != null}
        title={
          namePrompt?.mode === 'rename'
            ? t('dashboard.edit.renameGroup')
            : t('dashboard.edit.addGroup')
        }
        okText={t('common.ok')}
        cancelText={t('common.cancel')}
        onOk={confirmNamePrompt}
        onCancel={() => setNamePrompt(null)}
        destroyOnHidden
      >
        <Input
          autoFocus
          placeholder={t('dashboard.edit.groupNamePlaceholder')}
          value={nameInput}
          onChange={(event) => setNameInput(event.target.value)}
          onPressEnter={confirmNamePrompt}
        />
      </Modal>
    </div>,
    document.body
  )
}
