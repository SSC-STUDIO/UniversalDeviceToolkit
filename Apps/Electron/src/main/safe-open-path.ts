import { existsSync, statSync } from 'fs'
import { extname, resolve } from 'path'

const BLOCKED_FILE_EXTENSIONS = new Set([
  '.bat',
  '.cmd',
  '.com',
  '.cpl',
  '.exe',
  '.hta',
  '.js',
  '.jse',
  '.lnk',
  '.msi',
  '.msp',
  '.mst',
  '.ps1',
  '.psm1',
  '.reg',
  '.scr',
  '.url',
  '.vbe',
  '.vbs',
  '.wsf',
  '.wsh'
])

export function resolveSafeOpenPath(input: unknown): string {
  if (typeof input !== 'string' || input.trim().length === 0) {
    throw new Error('A file path is required.')
  }

  const target = resolve(input)
  if (!existsSync(target)) {
    throw new Error(`Path does not exist: ${target}`)
  }

  const stats = statSync(target)
  if (stats.isDirectory()) {
    return target
  }
  if (!stats.isFile()) {
    throw new Error('Only directories and regular files can be opened.')
  }

  if (BLOCKED_FILE_EXTENSIONS.has(extname(target).toLowerCase())) {
    throw new Error('Executable and script files cannot be opened from the renderer.')
  }

  return target
}
