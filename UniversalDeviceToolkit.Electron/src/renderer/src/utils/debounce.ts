/**
 * Debounce/Throttle dispatcher — port of Electron Utils/DebounceDispatcher.cs.
 * Single-flight semantics: debounce resets the timer on every call,
 * throttle ignores calls while a timer is pending.
 */
export interface DebounceDispatcher {
  debounce: (delayMs: number, action: () => void) => void
  throttle: (intervalMs: number, action: () => void) => void
  cancel: () => void
}

export function createDebounceDispatcher(): DebounceDispatcher {
  let timer: ReturnType<typeof setTimeout> | null = null
  let disposed = false

  const clear = (): void => {
    if (timer !== null) {
      clearTimeout(timer)
      timer = null
    }
  }

  const schedule = (delayMs: number, action: () => void, reset: boolean): void => {
    if (disposed) return
    if (!reset && timer !== null) return
    clear()
    timer = setTimeout(() => {
      timer = null
      if (!disposed) action()
    }, delayMs)
  }

  return {
    debounce(delayMs, action) {
      schedule(delayMs, action, true)
    },
    throttle(intervalMs, action) {
      schedule(intervalMs, action, false)
    },
    cancel() {
      disposed = true
      clear()
    }
  }
}
