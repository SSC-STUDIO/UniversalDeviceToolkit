import assert from 'node:assert/strict'
import { mkdtemp, mkdir, readFile, realpath, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { assertSafePackagingDirectories } from '../scripts/packaging-paths.mjs'

test('packaging requires a dedicated generated child and distinct output directories', async context => {
  // macOS /var is a system link; isolate fixtures under its physical temporary root.
  const root = await mkdtemp(join(await realpath(tmpdir()), 'udt-packaging-paths-'))
  context.after(() => rm(root, { recursive: true, force: true }))
  const generated = join(root, 'dist')
  const payload = join(generated, 'payload')
  const output = join(generated, 'artifacts')
  await assertSafePackagingDirectories(payload, output, [generated])
  await assert.rejects(assertSafePackagingDirectories(generated, output, [generated]), /must be below/)
  await assert.rejects(assertSafePackagingDirectories(join(root, 'outside'), output, [generated]), /must be below/)
  await assert.rejects(assertSafePackagingDirectories(payload, join(payload, 'artifacts'), [generated]), /must be separate/)
  await assert.rejects(assertSafePackagingDirectories(payload, generated, [generated]), /must be separate/)
  await assert.rejects(assertSafePackagingDirectories(payload, payload.toUpperCase(), [generated], 'win32'), /must be separate/)
  await assert.rejects(assertSafePackagingDirectories(payload, join(payload.toUpperCase(), 'ARTIFACTS'), [generated], 'win32'), /must be separate/)
})

test('packaging rejects linked payload ancestors and linked output directories without touching targets', async context => {
  const root = await mkdtemp(join(await realpath(tmpdir()), 'udt-packaging-links-'))
  context.after(() => rm(root, { recursive: true, force: true }))
  const generated = join(root, 'dist')
  const outside = join(root, 'external')
  const link = join(generated, 'linked')
  await mkdir(generated)
  await mkdir(outside)
  await writeFile(join(outside, 'keep.txt'), 'external user file')
  await symlink(outside, link, process.platform === 'win32' ? 'junction' : 'dir')
  await assert.rejects(assertSafePackagingDirectories(join(link, 'payload'), join(generated, 'artifacts'), [generated]), /symbolic link or junction/)
  await assert.rejects(assertSafePackagingDirectories(join(generated, 'payload'), link, [generated]), /symbolic link or junction/)
  assert.equal(await readFile(join(outside, 'keep.txt'), 'utf8'), 'external user file')
})
