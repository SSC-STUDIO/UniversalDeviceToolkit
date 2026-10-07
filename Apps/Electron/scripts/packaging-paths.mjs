import { lstat } from 'node:fs/promises'
import { dirname, resolve, sep } from 'node:path'

function comparablePath(path, platform) {
  const normalized = resolve(path)
  return platform === 'win32' ? normalized.toLowerCase() : normalized
}

function isWithin(path, root) {
  return path === root || path.startsWith(root.endsWith(sep) ? root : root + sep)
}

async function rejectLinkedParents(directory) {
  for (let current = resolve(directory); ; current = dirname(current)) {
    let info
    try { info = await lstat(current) }
    catch (error) { if (error.code !== 'ENOENT') throw error }
    if (info?.isSymbolicLink()) throw new Error('Packaging directory passes through a symbolic link or junction: ' + current)
    if (info && !info.isDirectory()) throw new Error('Packaging path is not a directory: ' + current)
    if (dirname(current) === current) return
  }
}

// Preparation replaces the payload tree, so validate physical ancestors before deletion.
export async function assertSafePackagingDirectories(payload, output, generatedRoots, platform = process.platform) {
  const target = comparablePath(payload, platform)
  const artifacts = comparablePath(output, platform)
  if (!generatedRoots.some(root => {
    const generated = comparablePath(root, platform)
    return target !== generated && isWithin(target, generated)
  })) throw new Error('The payload must be below dist or BuildInstallerPayload.')
  if (isWithin(target, artifacts) || isWithin(artifacts, target)) {
    throw new Error('Installer output and payload directories must be separate.')
  }
  await rejectLinkedParents(payload)
  await rejectLinkedParents(output)
}
