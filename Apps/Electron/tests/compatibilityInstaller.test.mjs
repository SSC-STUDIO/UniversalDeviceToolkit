import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { createHash } from 'node:crypto'
import { EventEmitter } from 'node:events'
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import vm from 'node:vm'
import { getMakeNsisPath } from 'app-builder-lib/out/toolsets/windows.js'
import { compatibilityScript } from '../scripts/compatibility-installer.mjs'

const builderUrl = new URL('../scripts/build-compatibility-installer.mjs', import.meta.url)
const builderSource = (await readFile(builderUrl, 'utf8'))
  .replace(/^import .*\r?\n/gm, '')
  .replaceAll('import.meta.url', JSON.stringify(builderUrl.href))
const project = dirname(dirname(fileURLToPath(builderUrl)))
const payload = join(project, 'dist', 'compatibility-payload')

async function runBuilderFixture(options = {}, preparedVersion = '6.1.4') {
  const files = new Map([
    [join(payload, 'resources/install-channel'), 'electron-compatibility']
  ])
  if (preparedVersion !== null) files.set(join(payload, 'resources/setup/build-version'), preparedVersion)
  const commands = []
  const prepared = []
  let nextScratch = 0
  const execution = vm.runInNewContext(`(async () => { ${builderSource}\n })()`, {
    createHash, dirname, join, relative, resolve, fileURLToPath,
    console: { log() {} },
    process: { platform: 'win32', execPath: process.execPath, env: {} },
    parseArgs: () => ({ values: {
      'payload-directory': 'dist/compatibility-payload', 'output-directory': 'dist/compatibility',
      version: '6.1.4', ...options
    } }),
    getMakeNsisPath: async () => ({ path: 'makensis.exe', env: {} }),
    mkdir: async () => {},
    stat: async () => ({ size: 1024 }),
    readFile: async path => {
      if (files.has(path)) return files.get(path)
      if (path.endsWith('.exe')) return Buffer.from('fixture installer')
      const error = new Error('Missing fixture file: ' + path)
      error.code = 'ENOENT'
      throw error
    },
    writeFile: async (path, value) => { files.set(path, value) },
    readdir: async () => [],
    cp: async () => {},
    rm: async () => {},
    mkdtemp: async prefix => prefix + ++nextScratch,
    spawn: (command, args) => {
      commands.push({ command, args })
      const child = new EventEmitter()
      queueMicrotask(() => child.emit('exit', 0))
      return child
    },
    assertOfflinePayload: async () => {},
    auditArtifactFiles: async () => {},
    prepareSetup: async (directory, projectDirectory, version) => {
      prepared.push({ directory, projectDirectory, version })
    },
    compatibilityScript: () => 'fixture NSIS script'
  })
  return { files, commands, prepared, execution }
}

test('compatibility preparation applies the requested version to Electron and the prepared payload', async () => {
  const version = '6.1.4-preview.1'
  const fixture = await runBuilderFixture({ version, 'prepare-only': true })
  await fixture.execution
  const electronBuild = fixture.commands.find(({ args }) => args.includes('compatibility-installer.yml'))
  assert.ok(electronBuild)
  assert.ok(electronBuild.args.includes(`--config.extraMetadata.version=${version}`))
  assert.equal(fixture.prepared[0]?.version, version)
  assert.equal(fixture.files.get(join(payload, 'resources/setup/build-version')), version)
})

test('compatibility package-only rejects a payload prepared for a different release', async () => {
  const fixture = await runBuilderFixture({ 'package-only': true }, '6.1.3')
  await assert.rejects(fixture.execution, /Prepared payload version does not match/)
  assert.equal(fixture.commands.length, 0)
})

test('compatibility package-only rejects a payload without a build version', async () => {
  const fixture = await runBuilderFixture({ 'package-only': true }, null)
  await assert.rejects(fixture.execution, { code: 'ENOENT' })
  assert.equal(fixture.commands.length, 0)
})

test('compatibility package-only accepts the matching release without rebuilding payloads', async () => {
  const fixture = await runBuilderFixture({ 'package-only': true })
  await fixture.execution
  assert.equal(fixture.prepared.length, 0)
  assert.deepEqual(fixture.commands.map(({ command }) => command), ['makensis.exe'])
  assert.ok([...fixture.files.keys()].some(path => path.endsWith('UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe.sha256')))
})

test('native compatibility installer compiles all supported languages without WebView2 pages', {
  skip: process.platform !== 'win32'
}, async (context) => {
  const work = await mkdtemp(join(tmpdir(), 'udt-native-installer-'))
  context.after(() => rm(work, { recursive: true, force: true }))
  const payload = join(work, 'payload')
  await mkdir(payload)
  await writeFile(join(payload, 'fixture.txt'), 'test payload')
  const installer = join(work, 'UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe')
  const script = join(work, 'setup.nsi')
  const resources = join(dirname(dirname(fileURLToPath(import.meta.url))), 'buildResources')
  await writeFile(script, compatibilityScript(payload, installer, resources), 'utf8')
  const compiler = await getMakeNsisPath()
  const output = await new Promise((resolve, reject) => {
    const child = spawn(compiler.path, ['/INPUTCHARSET', 'UTF8', '/V2', '/WX', script], {
      env: { ...process.env, ...compiler.env }, windowsHide: true
    })
    let diagnostic = ''
    child.stdout.on('data', chunk => { diagnostic += chunk })
    child.stderr.on('data', chunk => { diagnostic += chunk })
    child.once('error', reject)
    child.once('exit', code => code === 0 ? resolve(diagnostic) : reject(new Error(diagnostic)))
  })
  assert.equal(output, '')
  assert.equal((await readFile(installer)).subarray(0, 2).toString(), 'MZ')
})
