import { useEffect, useId, useRef, type ReactNode } from 'react'
import {
  collectAutomationDialogFocusables,
  resolveAutomationDialogKey
} from './automationDialog'

export interface AutomationModalProps {
  title: string
  onClose: () => void
  children: ReactNode
  actions?: ReactNode
  wide?: boolean
  className?: string
}

export default function AutomationModal(props: AutomationModalProps): React.JSX.Element {
  const { title, onClose, children, actions, wide, className } = props
  const titleId = useId()
  const dialogRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  useEffect(() => {
    const root = dialogRef.current
    if (root == null) return
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null
    const initial = collectAutomationDialogFocusables(root)[0] ?? root
    initial.focus()

    const onKeyDown = (event: KeyboardEvent): void => {
      const items = collectAutomationDialogFocusables(root)
      const active = document.activeElement
      const activeIndex = active instanceof HTMLElement ? items.indexOf(active) : -1
      const action = resolveAutomationDialogKey(event.key, event.shiftKey, items.length, activeIndex)
      if (action === 'close') {
        event.stopPropagation()
        event.preventDefault()
        onCloseRef.current()
        return
      }
      if (action === 'wrap-end') {
        event.preventDefault()
        items[0]?.focus()
        return
      }
      if (action === 'wrap-start') {
        event.preventDefault()
        items[items.length - 1]?.focus()
      }
    }

    root.addEventListener('keydown', onKeyDown)
    return () => {
      root.removeEventListener('keydown', onKeyDown)
      previous?.focus()
    }
  }, [])

  const modalClass = ['udt-modal', wide === true ? 'udt-modal--wide' : null, className]
    .filter((value): value is string => value != null && value !== '')
    .join(' ')

  return (
    <div className="udt-modal-backdrop" onClick={onClose}>
      <div
        ref={dialogRef}
        className={modalClass}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        onClick={(event) => event.stopPropagation()}
      >
        <div className="udt-modal__title" id={titleId}>
          {title}
        </div>
        {children}
        {actions != null && <div className="udt-modal__actions">{actions}</div>}
      </div>
    </div>
  )
}
