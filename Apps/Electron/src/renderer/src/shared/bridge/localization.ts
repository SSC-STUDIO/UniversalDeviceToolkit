import { getHostStatus, invoke, on } from './bridge'

export interface HostCultureResponse {
  culture: string
}

function readCultureResponse(result: HostCultureResponse, method: string): HostCultureResponse {
  if (result == null || typeof result !== 'object' || typeof result.culture !== 'string') {
    throw new Error(`${method} returned an invalid payload`)
  }
  return result
}

export const localizationApi = {
  async getCulture() {
    return readCultureResponse(
      await invoke<HostCultureResponse>('localization.getCulture', {}),
      'localization.getCulture'
    )
  },
  async setCulture(culture: string) {
    return readCultureResponse(
      await invoke<HostCultureResponse>('localization.setCulture', { culture }),
      'localization.setCulture'
    )
  }
}

/** Maps Electron's renderer codes to the canonical Host catalog names. */
export function hostCultureForLanguage(language: string): string {
  return language === 'zh-CN' ? 'zh-Hans' : language
}

type CultureSynchronizedListener = (culture: string) => void

let latestSequence = 0
let synchronizationQueue = Promise.resolve()
const synchronizedListeners = new Set<CultureSynchronizedListener>()

/**
 * Synchronizes the latest renderer selection with the Host when it is ready.
 * Requests are serialized because the Host dispatches RPC calls concurrently;
 * a stale request must never finish after a newer selection and become final.
 */
export function syncCultureToHost(language: string): Promise<boolean> {
  const sequence = ++latestSequence
  const culture = hostCultureForLanguage(language)
  const run = async (): Promise<boolean> => {
    if (sequence !== latestSequence) return false

    try {
      const status = await getHostStatus()
      if (!status.ready || sequence !== latestSequence) return false
    } catch (error) {
      console.warn('[i18n] Host status unavailable during culture sync:', error)
      return false
    }

    try {
      const result = await localizationApi.setCulture(culture)
      if (sequence !== latestSequence) return false
      synchronizedListeners.forEach((listener) => listener(result.culture))
      return true
    } catch (error) {
      console.warn('[i18n] failed to synchronize Host culture:', error)
      return false
    }
  }

  const result = synchronizationQueue.then(run, run)
  synchronizationQueue = result.then(
    () => undefined,
    () => undefined
  )
  return result
}

export function onCultureSynchronized(listener: CultureSynchronizedListener): () => void {
  synchronizedListeners.add(listener)
  return () => synchronizedListeners.delete(listener)
}

/** Replays the current renderer selection after the Host publishes host.ready. */
export function registerHostCultureRetry(currentLanguage: () => string): () => void {
  return on('host.ready', () => {
    void syncCultureToHost(currentLanguage())
  })
}
