import { useCallback, useEffect, useRef, useState } from 'react'
import type { MacroEvent, MacroRecordingMode } from '../api/macro'
import {
  MacroRecorderController,
  type MacroRecorderState
} from './macroRecorderCore'

export type { MacroRecorderState } from './macroRecorderCore'

/**
 * Renderer-side macro recorder — port of the Electron MacroRecorder + the
 * MacroRecordingWindow flow:
 *
 * - "KeyboardMouseMovement" first shows a 3-second "preparing" state
 *   (MacroSequenceControl.RecordAsync) before capturing starts.
 * - While recording, key/mouse input is captured into MacroEvent[]
 *   (delayMs between events, first event 0) and ESC (VK_ESCAPE) stops the
 *   recording with interrupted = true (MacroRecorder.LowLevelKeyboardProc).
 * - The capture phase runs in the renderer, so input is only captured while
 *   the app window is focused — the headless host cannot install global
 *   input hooks (macro.startRecording returns -1005).
 */
export function useMacroRecorder(
  onComplete: (events: MacroEvent[], interrupted: boolean) => void
): {
  state: MacroRecorderState
  start: (mode: MacroRecordingMode) => void
  stop: () => void
  cancel: () => void
} {
  const [state, setState] = useState<MacroRecorderState>('idle')
  const onCompleteRef = useRef(onComplete)

  useEffect(() => {
    onCompleteRef.current = onComplete
  }, [onComplete])

  const controllerRef = useRef<MacroRecorderController | null>(null)
  useEffect(() => {
    const controller = new MacroRecorderController(
      {
        target: window,
        now: () => performance.now(),
        setTimer: (callback, delayMs) => window.setTimeout(callback, delayMs),
        clearTimer: (timer) => window.clearTimeout(timer)
      },
      {
        onStateChange: setState,
        onComplete: (events, interrupted) => onCompleteRef.current(events, interrupted)
      }
    )
    controllerRef.current = controller
    return () => {
      controller.dispose()
      controllerRef.current = null
    }
  }, [])

  const start = useCallback(
    (mode: MacroRecordingMode): void => controllerRef.current?.start(mode),
    []
  )

  const stop = useCallback((): void => controllerRef.current?.stop(), [])
  const cancel = useCallback((): void => controllerRef.current?.cancel(), [])

  return { state, start, stop, cancel }
}
