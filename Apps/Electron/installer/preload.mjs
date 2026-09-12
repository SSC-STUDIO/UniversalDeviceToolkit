import { contextBridge, ipcRenderer } from 'electron'

contextBridge.exposeInMainWorld('installerApi', {
  isUninstaller: process.argv.includes('--uninstall'),
  getInfo: () => ipcRenderer.invoke('installer:info'),
  getTheme: () => ipcRenderer.invoke('installer:theme-info'),
  chooseDirectory: () => ipcRenderer.invoke('installer:choose-directory'),
  install: (options) => ipcRenderer.invoke('installer:install', options),
  uninstall: () => ipcRenderer.invoke('installer:uninstall'),
  launch: (executable) => ipcRenderer.invoke('installer:launch', executable),
  minimize: () => ipcRenderer.send('installer:minimize'),
  close: () => ipcRenderer.send('installer:close'),
  onProgress: (callback) => {
    const listener = (_event, payload) => callback(payload)
    ipcRenderer.on('installer:progress', listener)
    return () => ipcRenderer.removeListener('installer:progress', listener)
  },
  onThemeChanged: (callback) => {
    const listener = (_event, payload) => callback(payload)
    ipcRenderer.on('installer:theme', listener)
    return () => ipcRenderer.removeListener('installer:theme', listener)
  }
})
