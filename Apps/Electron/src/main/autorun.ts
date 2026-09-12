/**
 * Login-item autostart for macOS/Linux.
 *
 * Windows autorun is owned by the Host (Autorun.Set scheduled task pointed at
 * the Electron shell via UDT_SHELL_PATH); the app:set-autorun IPC is a no-op
 * there. Linux uses an XDG autostart .desktop file under ~/.config/autostart
 * ("enabled" is the file's existence); macOS uses the Electron login item.
 */
import { app } from 'electron'
import { existsSync, mkdirSync, unlinkSync, writeFileSync } from 'fs'
import { dirname, join } from 'path'

const AUTOSTART_FILE_NAME = 'universal-device-toolkit.desktop'

function linuxAutostartFilePath(): string {
  return join(app.getPath('home'), '.config', 'autostart', AUTOSTART_FILE_NAME)
}

/** AppImage extracts under /tmp/.mount_*; that path dies when the session ends. */
function isEphemeralAppImageMount(filePath: string): boolean {
  return /(?:^|[/\\])tmp[/\\]\.mount_/i.test(filePath)
}

/**
 * Login-item Exec must survive reboot. The AppImage runtime sets APPIMAGE to
 * the persistent .AppImage file; process.execPath is the FUSE mount binary.
 */
function linuxPersistentExecPath(): string {
  const appImage = process.env.APPIMAGE
  if (typeof appImage === 'string') {
    const persistent = appImage.trim()
    if (
      persistent.length > 0 &&
      !isEphemeralAppImageMount(persistent) &&
      existsSync(persistent)
    ) {
      return persistent
    }
  }
  const execPath = process.execPath
  if (isEphemeralAppImageMount(execPath)) {
    throw new Error(
      'Cannot enable autostart: AppImage is running from an ephemeral mount and APPIMAGE is unset or invalid.'
    )
  }
  return execPath
}

function quoteDesktopExec(filePath: string): string {
  return `"${filePath.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`
}

export function applyAutorun(enabled: boolean): void {
  if (process.platform === 'linux') {
    const filePath = linuxAutostartFilePath()
    if (enabled) {
      mkdirSync(dirname(filePath), { recursive: true })
      // Exec quotes the path (spaces in install locations). X-GNOME-Autostart
      // is understood by GNOME; other DEs fall back to the generic Desktop Entry.
      writeFileSync(
        filePath,
        [
          '[Desktop Entry]',
          'Type=Application',
          'Name=Universal Device Toolkit',
          `Exec=${quoteDesktopExec(linuxPersistentExecPath())}`,
          'X-GNOME-Autostart-enabled=true'
        ].join('\n') + '\n',
        'utf8'
      )
    } else if (existsSync(filePath)) {
      unlinkSync(filePath)
    }
    return
  }
  // Windows registry Run key / macOS login item via Electron.
  app.setLoginItemSettings({ openAtLogin: enabled })
}

export function readAutorun(): boolean {
  if (process.platform === 'linux') {
    return existsSync(linuxAutostartFilePath())
  }
  return app.getLoginItemSettings().openAtLogin
}
