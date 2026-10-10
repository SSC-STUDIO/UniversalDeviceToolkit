import { invoke, invokeObject } from './bridge'



export interface ShellApi {
  /** Native folder picker; null when cancelled. */
  selectFolder(): Promise<string | null>
  /** Open a path in the system explorer. */
  openPath(path: string): Promise<{ ok: boolean }>
  /** Open an http(s) URL in the default browser. */
  openUrl(url: string): Promise<{ ok: boolean }>
}

export const shellApi: ShellApi = {
  async selectFolder() {
    return invoke<string | null>('dialog:select-folder', {})
  },

  async openPath(path) {
    return invokeObject<{ ok: boolean }>('dialog:open-path', { path })
  },

  async openUrl(url) {
    return invokeObject<{ ok: boolean }>('dialog:open-url', { url })
  }
}
