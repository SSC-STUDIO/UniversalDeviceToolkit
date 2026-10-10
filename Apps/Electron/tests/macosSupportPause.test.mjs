import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import test from 'node:test'
import {
  assertMacosPackagingAllowed,
  beforePack,
  MACOS_SUPPORT_PAUSED_REASON
} from '../scripts/macos-support-paused.mjs'

const execute = promisify(execFile)
const project = fileURLToPath(new URL('..', import.meta.url))
const repository = fileURLToPath(new URL('../../..', import.meta.url))

test('electron-builder rejects macOS targets on any host and keeps Windows/Linux enabled', () => {
  for (const platform of ['darwin', 'mac', 'macos', 'osx', 'osx-arm64', 'osx-x64']) {
    assert.throws(() => assertMacosPackagingAllowed(platform), { message: MACOS_SUPPORT_PAUSED_REASON })
    assert.throws(() => beforePack({ electronPlatformName: platform }), { message: MACOS_SUPPORT_PAUSED_REASON })
  }
  for (const platform of ['win32', 'linux']) {
    assert.doesNotThrow(() => beforePack({ electronPlatformName: platform }))
  }
  for (const context of [null, [], {}, { electronPlatformName: false }]) {
    assert.throws(() => beforePack(context), TypeError)
  }
})

test('default and direct Mac package entry points refuse work before calling the builder', async () => {
  const manifest = JSON.parse(readFileSync(join(project, 'package.json'), 'utf8'))
  assert.equal(manifest.scripts['dist:mac'], 'node scripts/macos-support-paused.mjs mac')
  assert.ok(manifest.scripts.dist.startsWith('node scripts/macos-support-paused.mjs && '))
  assert.doesNotMatch(manifest.scripts['dist:mac'], /npm run build|electron-builder/)
  const config = readFileSync(join(project, 'electron-builder.yml'), 'utf8')
  assert.match(config, /^beforePack: \.\/scripts\/macos-support-paused\.mjs$/m)
  assert.match(config, /^afterPack: \.\/scripts\/package-footprint\.mjs$/m)
  assert.match(config, /^mac:$/m, 'Keep the Mac configuration available for restoration')

  for (const arguments_ of [
    ['scripts/macos-support-paused.mjs', 'mac'],
    ['scripts/build-desktop-installer.mjs', '--mac'],
    ['scripts/build-desktop-installer.mjs', '--mac=arm64'],
    ['scripts/build-desktop-installer.mjs', '-m']
  ]) {
    await assert.rejects(execute(process.execPath, arguments_, { cwd: project, windowsHide: true, timeout: 10000 }),
      error => error.code === 1 && error.stderr.includes(MACOS_SUPPORT_PAUSED_REASON))
  }
})

test('CLI asset guidance names supported platforms and does not advertise Mac runtime support', () => {
  const source = readFileSync(join(repository, 'Scripts/Build-CrossPlatformCliAsset.ps1'), 'utf8')
  const readme = source.match(/\$readme = @'\r?\n([\s\S]*?)\r?\n'@/)[1]
  assert.match(readme, /Windows:/)
  assert.match(readme, /Linux:/)
  assert.match(readme, /macOS support is temporarily paused/)
  assert.doesNotMatch(readme, /macOS\/Linux|Any OS:/)
  assert.match(source, /Darwin\*\)/, 'The shared Unix launcher rejects the paused runtime')
})

test('build.sh refuses explicit Mac runtimes and platform overrides before any dotnet call', async context => {
  const bash = process.platform === 'win32'
    ? join(process.env.ProgramFiles ?? 'C:/Program Files', 'Git/bin/bash.exe')
    : '/bin/bash'
  if (!existsSync(bash)) {
    context.skip('Bash is unavailable for the platform-entry behavior check')
    return
  }
  const script = 'build.sh'
  for (const fixture of [
    { arguments: [script, 'Release', 'osx-arm64'], env: {} },
    { arguments: [script, 'host'], env: { UDT_RID: 'osx-x64' } },
    { arguments: [script, 'host'], env: { UDT_PLATFORM: 'macos' } }
  ]) {
    await assert.rejects(execute(bash, fixture.arguments, {
      cwd: repository, windowsHide: true, timeout: 10000,
      env: { ...process.env, ...fixture.env }
    }), error => error.code === 1 && error.stderr.includes(MACOS_SUPPORT_PAUSED_REASON))
  }
})
