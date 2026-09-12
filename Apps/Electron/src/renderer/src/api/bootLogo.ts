import { invokeObject } from './bridge'

/**
 * Boot logo bridge — renderer counterpart of Electron Lib/System/BootLogo.cs.
 * The host is expected to expose:
 *   bootLogo.getStatus -> { enabled, resolution: { DisplayName }, formats: string[], filters: string[] }
 *   bootLogo.enable  { filePath }  -> { ok }
 *   bootLogo.disable               -> { ok }
 */

export interface BootLogoStatus {
  enabled: boolean
  /** Built-in display resolution, e.g. { DisplayName: "2560 x 1600" }. */
  resolution?: { DisplayName?: string } | null
  /** Supported image formats, e.g. ["BMP", "JPG", "PNG"]. */
  formats?: string[] | null
  /** OpenFileDialog filters, e.g. ["*.bmp;*.jpg;*.jpeg;*.png"]. */
  filters?: string[] | null
}

export const bootLogoApi = {
  getStatus: (): Promise<BootLogoStatus> => invokeObject<BootLogoStatus>('bootLogo.getStatus', {}),
  enable: (filePath: string): Promise<{ ok: boolean }> =>
    invokeObject<{ ok: boolean }>('bootLogo.enable', { filePath }),
  disable: (): Promise<{ ok: boolean }> => invokeObject<{ ok: boolean }>('bootLogo.disable', {})
}
