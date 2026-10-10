import './custom.css'
import type { ReactNode } from 'react'

/**
 * Unified empty-state presenter — port of Electron Controls/Custom/EmptyState.cs
 * (hero icon, title, description and an optional action slot).
 */

export interface EmptyStateProps {
  /** Hero glyph shown above the title (defaults to a search glyph). */
  icon?: ReactNode
  title?: string
  description?: string
  /** Optional action slot (e.g. a Button) rendered under the description. */
  action?: ReactNode
  className?: string
  children?: ReactNode
}

export default function EmptyState({
  icon,
  title,
  description,
  action,
  className,
  children
}: EmptyStateProps): React.JSX.Element {
  const classes = ['udt-empty-state']
  if (className) classes.push(className)
  return (
    <div className={classes.join(' ')}>
      {icon != null && <span className="udt-empty-state__icon" aria-hidden="true">{icon}</span>}
      {title != null && title !== '' && <div className="udt-empty-state__title">{title}</div>}
      {description != null && description !== '' && (
        <div className="udt-empty-state__description">{description}</div>
      )}
      {action != null && <div className="udt-empty-state__action">{action}</div>}
      {children}
    </div>
  )
}
