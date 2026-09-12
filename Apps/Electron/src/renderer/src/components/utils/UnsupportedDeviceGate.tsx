import { useEffect } from 'react'
import { waitForHostReady } from '../../api/bridge'
import { systemApi, type SystemInfo } from '../../api/system'
import { openUnsupportedDevice } from './UnsupportedDeviceModal'

async function fetchSystemInfo(retries = 6): Promise<SystemInfo | null> {
  for (let attempt = 0; attempt < retries; attempt++) {
    try {
      return await systemApi.info()
    } catch {
      await new Promise((resolve) => window.setTimeout(resolve, 1000))
    }
  }
  return null
}

/** Shows the compatibility warning only after the Host has initialized. */
export default function UnsupportedDeviceGate(): React.JSX.Element {
  useEffect(() => {
    let cancelled = false

    const run = async (): Promise<void> => {
      try {
        await waitForHostReady()
      } catch {
        return
      }
      if (cancelled) return

      const info = await fetchSystemInfo()
      if (cancelled || info == null || info.isCompatible !== false) return

      const shouldContinue = await openUnsupportedDevice({
        vendor: info.vendor ?? null,
        model: info.model ?? null,
        machineType: info.machineType ?? null
      })
      if (cancelled || shouldContinue) return
      window.bridge?.quitApp?.()
    }

    void run()
    return () => {
      cancelled = true
    }
  }, [])

  return <></>
}
