/**
 * Clipboard process-list API. Unlike api/bridge.ts, which routes to the host
 * JSON-RPC process, this channel is served by the Electron main process, so it
 * goes through the dedicated preload bridge method instead of bridge.invoke.
 */

const getBridge = (): typeof window.bridge => window.bridge

/** Write one executable path per line. */
export async function writeLines(lines: string[]): Promise<{ ok: boolean }> {
  const bridge = getBridge()
  if (!bridge?.writeClipboardLines) {
    throw new Error('Bridge is not available')
  }
  return bridge.writeClipboardLines(lines)
}

export const clipboardApi = {
  writeLines
}
