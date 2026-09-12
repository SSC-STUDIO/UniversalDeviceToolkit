export type AutomationDialogKeyAction = 'close' | 'wrap-start' | 'wrap-end' | null

const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])'
].join(',')

export function resolveAutomationDialogKey(
  key: string,
  shiftKey: boolean,
  focusableCount: number,
  activeIndex: number
): AutomationDialogKeyAction {
  if (key === 'Escape') return 'close'
  if (key !== 'Tab' || focusableCount <= 0) return null
  if (shiftKey && activeIndex <= 0) return 'wrap-start'
  if (!shiftKey && (activeIndex < 0 || activeIndex >= focusableCount - 1)) return 'wrap-end'
  return null
}

export function collectAutomationDialogFocusables(root: ParentNode): HTMLElement[] {
  return Array.from(root.querySelectorAll(FOCUSABLE_SELECTOR)).filter((node): node is HTMLElement => {
    if (!(node instanceof HTMLElement)) return false
    if (node.getAttribute('aria-hidden') === 'true') return false
    return node.getClientRects().length > 0
  })
}
