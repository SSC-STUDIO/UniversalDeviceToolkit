/**
 * Shared UI-visibility helper: pauses dashboard polling when the main window
 * is hidden (tray) or the document is backgrounded.
 *
 * Active only when both signals are true. Independent events are merged so a
 * visible document cannot restart polls while the app is in the tray (and the
 * reverse). The listener is invoked only when the merged value changes.
 */

export function mergeUiVisibility(documentVisible: boolean, appVisible: boolean): boolean {
  return documentVisible && appVisible
}

export function createUiVisibilityGate(listener: (active: boolean) => void): {
  setDocumentVisible: (visible: boolean) => void
  setAppVisible: (visible: boolean) => void
  getActive: () => boolean
} {
  let documentVisible = true
  let appVisible = true
  let lastActive: boolean | null = null

  const emit = (): void => {
    const next = mergeUiVisibility(documentVisible, appVisible)
    if (next === lastActive) return
    lastActive = next
    listener(next)
  }

  return {
    setDocumentVisible(visible: boolean): void {
      documentVisible = visible
      emit()
    },
    setAppVisible(visible: boolean): void {
      appVisible = visible
      emit()
    },
    getActive(): boolean {
      return mergeUiVisibility(documentVisible, appVisible)
    }
  }
}

function readAppVisible(data: unknown): boolean {
  return (data as { active?: boolean } | null)?.active !== false
}

export function subscribeUiVisibility(listener: (active: boolean) => void): () => void {
  const gate = createUiVisibilityGate(listener)
  gate.setDocumentVisible(!document.hidden)

  const onBridge = (data: unknown): void => {
    gate.setAppVisible(readAppVisible(data))
  }
  const offBridge = window.bridge?.on('app:ui-visibility', onBridge)
  const onDocument = (): void => {
    gate.setDocumentVisible(!document.hidden)
  }
  document.addEventListener('visibilitychange', onDocument)
  return () => {
    offBridge?.()
    document.removeEventListener('visibilitychange', onDocument)
  }
}
