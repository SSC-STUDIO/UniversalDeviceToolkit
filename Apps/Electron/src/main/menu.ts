import { app, Menu, shell } from 'electron'

/**
 * macOS application menu (the system menu bar at the top of the screen).
 * Windows/Linux keep the frameless custom title bar and the auto-hidden
 * default menu; on macOS the OS-drawn menu is the native convention, so we
 * build a proper one with standard roles and shortcuts (Cmd+Q, Cmd+W, ...).
 */

export function installApplicationMenu(): void {
  if (process.platform !== 'darwin') return

  const isMac = true
  const template: Electron.MenuItemConstructorOptions[] = [
    // App menu (first menu — the app name is automatic on macOS)
    ...(isMac
      ? [
          {
            label: app.name,
            submenu: [
              { role: 'about' as const },
              { type: 'separator' as const },
              { role: 'services' as const },
              { type: 'separator' as const },
              { role: 'hide' as const },
              { role: 'hideOthers' as const },
              { role: 'unhide' as const },
              { type: 'separator' as const },
              { role: 'quit' as const }
            ]
          }
        ]
      : []),
    // File
    {
      label: 'File',
      submenu: [
        { role: 'close' as const, label: 'Close Window' },
        { type: 'separator' as const },
        { role: 'quit' as const }
      ]
    },
    // Edit (standard roles give Cmd+C/X/V/A on macOS)
    {
      label: 'Edit',
      submenu: [
        { role: 'undo' as const },
        { role: 'redo' as const },
        { type: 'separator' as const },
        { role: 'cut' as const },
        { role: 'copy' as const },
        { role: 'paste' as const },
        { role: 'selectAll' as const }
      ]
    },
    // View. The Chromium zoom roles (resetZoom/zoomIn/zoomOut) are intentionally
    // absent: renderer zoom is owned by ui-scale.ts (interface scale setting),
    // and the roles would stack a third, unmanaged factor on top of it.
    {
      label: 'View',
      submenu: [
        { role: 'reload' as const, label: 'Reload' },
        { role: 'forceReload' as const },
        { role: 'toggleDevTools' as const },
        { type: 'separator' as const },
        { role: 'togglefullscreen' as const }
      ]
    },
    // Window
    {
      label: 'Window',
      submenu: [
        { role: 'minimize' as const },
        { role: 'zoom' as const },
        { type: 'separator' as const },
        { role: 'front' as const }
      ]
    },
    // Help — links out to the project
    {
      label: 'Help',
      submenu: [
        {
          label: 'GitHub Project',
          click: (): void => {
            void shell.openExternal('https://github.com/SSC-STUDIO/UniversalDeviceToolkit')
          }
        },
        {
          label: 'Report an Issue',
          click: (): void => {
            void shell.openExternal('https://github.com/SSC-STUDIO/UniversalDeviceToolkit/issues')
          }
        }
      ]
    }
  ]

  Menu.setApplicationMenu(Menu.buildFromTemplate(template))
}
