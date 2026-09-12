import { useMemo, useState } from 'react'
import { ChevronRight24Regular } from '@fluentui/react-icons'
import { Empty, Input, Modal, Tooltip } from 'antd'
import { Search24Regular } from '../icons/fluent'
import { useTranslation } from 'react-i18next'
import type { DashboardItem } from '../../api/dashboard'
import { ALL_DASHBOARD_ITEMS, dashboardItemLabel } from './dashboardItems'

/**
 * Parity modal for Electron Windows/Dashboard/AddDashboardItemWindow:
 * lists every dashboard item that is not already used by any group;
 * clicking a card adds the item and closes the dialog.
 */
interface AddDashboardItemModalProps {
  open: boolean
  existingItems: DashboardItem[]
  onAdd: (item: DashboardItem) => void
  onClose: () => void
}

export default function AddDashboardItemModal({
  open,
  existingItems,
  onAdd,
  onClose
}: AddDashboardItemModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [query, setQuery] = useState('')

  const available = useMemo(() => {
    const existing = new Set(existingItems)
    const normalized = query.trim().toLowerCase()
    return ALL_DASHBOARD_ITEMS
      .filter((item) => !existing.has(item))
      .filter((item) => {
        if (normalized.length === 0) return true
        return dashboardItemLabel(item, t).toLowerCase().includes(normalized)
      })
  }, [existingItems, query, t])

  const handleAdd = (item: DashboardItem): void => {
    onAdd(item)
    onClose()
  }

  return (
    <Modal
      centered
      open={open}
      title={t('dashboard.addItem.title')}
      width={600}
      footer={null}
      onCancel={onClose}
    >
      <Input
        allowClear
        autoFocus
        prefix={<Search24Regular />}
        placeholder={t('dashboard.addItem.searchPlaceholder')}
        value={query}
        onChange={(event) => setQuery(event.target.value)}
      />
      <div className="udt-dashboard-add-items">
        {available.length === 0 ? (
          <Empty description={t('dashboard.addItem.empty')} image={Empty.PRESENTED_IMAGE_SIMPLE} />
        ) : (
          available.map((item) => (
            <button
              key={item}
              type="button"
              className="udt-dashboard-add-item"
              onClick={() => handleAdd(item)}
            >
              <span className="udt-dashboard-add-item__title">{dashboardItemLabel(item, t)}</span>
              <Tooltip title={t('dashboard.addItem.addHint')}>
                <span className="udt-dashboard-add-item__icon" aria-hidden="true">
                  <ChevronRight24Regular />
                </span>
              </Tooltip>
            </button>
          ))
        )}
      </div>
    </Modal>
  )
}
