/**
 * App-level APIs served by the Electron main process (window shell).
 * Windows autorun is the Host scheduled task (`startupApi` / `app.setAutorun`).
 * macOS/Linux use this login-item / XDG channel because Host Autorun is Windows-only.
 */

const getBridge = (): typeof window.bridge => window.bridge

export async function setAutorun(enabled: boolean): Promise<{ ok: boolean; enabled: boolean }> {
  const bridge = getBridge()
  if (!bridge?.setAutorun) {
    throw new Error('Bridge is not available')
  }
  return bridge.setAutorun(enabled)
}

export async function getAutorun(): Promise<{ enabled: boolean }> {
  const bridge = getBridge()
  if (!bridge?.getAutorun) {
    throw new Error('Bridge is not available')
  }
  return bridge.getAutorun()
}

export const appApi = {
  setAutorun,
  getAutorun
}
