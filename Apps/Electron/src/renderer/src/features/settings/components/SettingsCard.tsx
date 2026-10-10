import type { KeyboardEvent, ReactNode } from 'react'
import { ChevronRight16Regular } from '@fluentui/react-icons'

interface SettingsCardProps {
  title?: string
  description?: string
  action?: ReactNode
  children?: ReactNode
  onClick?: () => void
}

export function SettingsCard({
  title,
  description,
  action,
  children,
  onClick
}: SettingsCardProps): React.JSX.Element {
  const clickable = onClick != null
  const row = children == null && (action != null || clickable)

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>): void => {
    if (!clickable) return
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      onClick?.()
    }
  }

  return (
    <div
      className={`udt-settings-card${row ? ' udt-settings-card--row' : ''}${clickable ? ' udt-settings-card--action' : ''}`}
      role={clickable ? 'button' : undefined}
      tabIndex={clickable ? 0 : undefined}
      onClick={onClick}
      onKeyDown={handleKeyDown}
    >
      {(title != null || action != null) && (
        <div className="udt-settings-card__header">
          <div className="udt-settings-card__copy">
            {title != null && <div className="udt-settings-card__title">{title}</div>}
            {description != null && (
              <div className="udt-settings-card__description">{description}</div>
            )}
          </div>
          {action != null && <div className="udt-settings-card__action">{action}</div>}
          {clickable && action == null && (
            <span className="udt-settings-card__chevron">
              <ChevronRight16Regular />
            </span>
          )}
        </div>
      )}
      {children != null && <div className="udt-settings-card__content">{children}</div>}
    </div>
  )
}
