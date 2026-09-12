import { useEffect, useId, useRef, type RefObject } from 'react'

const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  'summary',
  '[tabindex]:not([tabindex="-1"])'
].join(',')

export interface UseUtilsDialogOptions {
  /** When the dialog is rendered by another component, resolve its root after paint. */
  rootSelector?: string
}

function isFocusable(element: HTMLElement): boolean {
  if (element.hasAttribute('disabled') || element.tabIndex < 0) return false
  if (element.getClientRects().length === 0) return false
  const style = window.getComputedStyle(element)
  return style.visibility !== 'hidden' && style.display !== 'none'
}

export function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return [...container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)].filter(isFocusable)
}

function resolveRoot(
  dialogRef: { current: HTMLElement | null },
  rootSelector: string | undefined
): HTMLElement | null {
  return dialogRef.current ?? (rootSelector != null ? document.querySelector<HTMLElement>(rootSelector) : null)
}

/**
 * Dialog chrome for Utils hosts: role/aria-modal, labelled title, Tab focus trap,
 * optional Escape dismiss, initial focus, and restore-focus on close.
 */
export function useUtilsDialog(
  open: boolean,
  onEscape: (() => void) | null,
  options: UseUtilsDialogOptions = {}
): {
  dialogRef: RefObject<HTMLDivElement | null>
  titleId: string
  dialogProps: {
    role: 'dialog'
    'aria-modal': true
    'aria-labelledby': string
    tabIndex: -1
  }
} {
  const dialogRef = useRef<HTMLDivElement>(null)
  const onEscapeRef = useRef(onEscape)
  const previousFocusRef = useRef<HTMLElement | null>(null)
  const titleId = useId()
  const rootSelector = options.rootSelector
  onEscapeRef.current = onEscape

  useEffect(() => {
    if (!open) return
    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null

    const focusInitial = (): void => {
      const root = resolveRoot(dialogRef, rootSelector)
      if (root == null) return
      const marked = root.querySelector<HTMLElement>('[data-utils-initial-focus]')
      if (marked != null && isFocusable(marked)) {
        marked.focus()
        return
      }
      const first = getFocusableElements(root)[0]
      if (first != null) first.focus()
      else root.focus()
    }
    const timer = window.setTimeout(focusInitial, 0)

    const onKeyDown = (event: KeyboardEvent): void => {
      const root = resolveRoot(dialogRef, rootSelector)
      if (root == null) return

      if (event.key === 'Escape') {
        const dismiss = onEscapeRef.current
        if (dismiss == null) return
        event.preventDefault()
        event.stopPropagation()
        dismiss()
        return
      }

      if (event.key !== 'Tab') return
      const nodes = getFocusableElements(root)
      if (nodes.length === 0) {
        event.preventDefault()
        root.focus()
        return
      }
      const first = nodes[0]
      const last = nodes[nodes.length - 1]
      const active = document.activeElement
      if (event.shiftKey && (active === first || !root.contains(active))) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && (active === last || !root.contains(active))) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', onKeyDown, true)
    return () => {
      window.clearTimeout(timer)
      document.removeEventListener('keydown', onKeyDown, true)
      const previous = previousFocusRef.current
      if (previous != null && previous.isConnected) previous.focus()
    }
  }, [open, rootSelector])

  return {
    dialogRef,
    titleId,
    dialogProps: {
      role: 'dialog',
      'aria-modal': true,
      'aria-labelledby': titleId,
      tabIndex: -1
    }
  }
}
