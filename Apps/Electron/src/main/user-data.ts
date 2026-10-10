import { app } from 'electron'
import { mkdirSync } from 'fs'
import { join, resolve } from 'path'

export function dataDirectoryOverride(): string | undefined {
  const configured = process.env['UDT_APPDATA_OVERRIDE']
  return configured?.trim() ? resolve(configured) : undefined
}

export function configureUserDataDirectory(): void {
  const directory = dataDirectoryOverride()
  if (!directory) return
  const sessionDirectory = join(directory, 'Electron')
  mkdirSync(sessionDirectory, { recursive: true })
  app.setPath('userData', directory)
  app.setPath('sessionData', sessionDirectory)
}
