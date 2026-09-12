import './custom.css'
import { useEffect, useId, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { ChevronDown16Regular } from '@fluentui/react-icons'

/**
 * Expandable card — port of Electron Controls/Custom/CardExpander.cs
 * (Wpf.Ui CardExpander: clickable header toggles the body).
 */

export interface CardExpanderProps {
  /** Header content; when a string, rendered as the card title. */
  header?: ReactNode
  /** Secondary text under a string header. */
  description?: string
  /** Leading icon shown before the header copy (Electron CardExpander.Icon). */
  icon?: ReactNode
  /** Trailing control before the chevron (e.g. Configure); clicks do not toggle. */
  accessory?: ReactNode
  defaultExpanded?: boolean
  expanded?: boolean
  onExpandedChange?: (expanded: boolean) => void
  children?: ReactNode
  className?: string
}

export default function CardExpander({
  header,
  description,
  icon,
  accessory,
  defaultExpanded = false,
  expanded,
  onExpandedChange,
  children,
  className
}: CardExpanderProps): React.JSX.Element {
  const [internal, setInternal] = useState(defaultExpanded)
  const isExpanded = expanded ?? internal
  const bodyId = useId()
  const headerRef = useRef<HTMLButtonElement>(null)
  const bodyRef = useRef<HTMLDivElement>(null)
  const wasExpandedRef = useRef(isExpanded)

  useEffect(() => {
    if (expanded !== undefined) setInternal(expanded)
  }, [expanded])

  useEffect(() => {
    if (wasExpandedRef.current && !isExpanded) {
      const body = bodyRef.current
      const active = document.activeElement
      if (body != null && active instanceof Node && body.contains(active)) {
        headerRef.current?.focus()
      }
    }
    wasExpandedRef.current = isExpanded
  }, [isExpanded])

  const toggle = (): void => {
    const next = !isExpanded
    setInternal(next)
    onExpandedChange?.(next)
  }

  const classes = ['udt-card-expander']
  if (isExpanded) classes.push('udt-card-expander--expanded')
  if (className) classes.push(className)

  return (
    <div className={classes.join(' ')}>
      <div className="udt-card-expander__header-row">
        {icon != null && <span className="udt-card-expander__icon">{icon}</span>}
        <button
          ref={headerRef}
          type="button"
          className="udt-card-expander__header"
          onClick={toggle}
          aria-expanded={isExpanded}
          aria-controls={bodyId}
        >
          <span className="udt-card-expander__copy">
            {typeof header === 'string' ? (
              <>
                <span className="udt-card-expander__title">{header}</span>
                {description != null && description !== '' && (
                  <span className="udt-card-expander__desc">{description}</span>
                )}
              </>
            ) : (
              header
            )}
          </span>
        </button>
        {accessory != null && <div className="udt-card-expander__accessory">{accessory}</div>}
        <button
          type="button"
          className="udt-card-expander__chevron-btn"
          onClick={toggle}
          tabIndex={-1}
          aria-hidden="true"
        >
          <span className="udt-card-expander__chevron" aria-hidden="true">
            <ChevronDown16Regular />
          </span>
        </button>
      </div>
      {/* Body stays mounted; the 0fr→1fr grid rows transition animates the
          expand/collapse height (no layout thrash, works with content changes). */}
      <div
        id={bodyId}
        ref={bodyRef}
        className={`udt-card-expander__body-wrap${isExpanded ? ' udt-card-expander__body-wrap--expanded' : ''}`}
        aria-hidden={!isExpanded}
        {...(!isExpanded ? { inert: true } : {})}
      >
        <div className="udt-card-expander__body">{children}</div>
      </div>
    </div>
  )
}
