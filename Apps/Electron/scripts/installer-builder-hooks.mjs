export function beforeBuild() {
  // The installer shell uses only Electron and Node built-ins. Returning false
  // prevents electron-builder from copying the main application's production
  // dependencies into the shell a second time; the signed payload remains intact.
  return false
}
