import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs'
import { createRequire } from 'node:module'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import vm from 'node:vm'
import ts from 'typescript'

const nodeRequire = createRequire(import.meta.url)
function loadModule(name, environment, mocks) {
  const source = readFileSync(new URL(`../src/main/${name}.ts`, import.meta.url), 'utf8')
  const compiled = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
  }).outputText
  const module = { exports: {} }
  vm.runInNewContext(compiled, {
    exports: module.exports, module, console,
    process: { env: environment, argv: ['electron', 'main'] },
    require: name => mocks[name] ?? nodeRequire(name)
  })
  return module.exports
}

test('data override isolates Electron profile, session cache and external Host flags', context => {
  const directory = mkdtempSync(join(tmpdir(), 'udt-user-data-'))
  context.after(() => rmSync(directory, { recursive: true, force: true }))
  const paths = { userData: 'original-profile', sessionData: 'original-cache' }
  const app = { getPath: name => paths[name], setPath: (name, value) => { paths[name] = value } }
  const environment = { UDT_APPDATA_OVERRIDE: directory, LOCALAPPDATA: join(directory, 'unrelated') }
  writeFileSync(join(directory, 'args.txt'), '--safe-start\n--no-hardware\n')
  const userData = loadModule('user-data', environment, { electron: { app } })

  userData.configureUserDataDirectory()
  assert.equal(paths.userData, resolve(directory))
  assert.equal(paths.sessionData, join(directory, 'Electron'))
  assert.equal(existsSync(paths.sessionData), true)
  const flags = loadModule('flags', environment, { electron: { app }, './user-data': userData })
  assert.deepEqual(Array.from(flags.loadExternalArgs()), ['--safe-start', '--no-hardware'])
  assert.equal(flags.flags.safeStart, true)
  assert.equal(flags.flags.noHardware, true)
})

test('unset or empty data overrides preserve the existing Electron profile', () => {
  for (const configured of [undefined, '', '   ']) {
    const paths = []
    const app = { setPath: (...args) => paths.push(args) }
    const userData = loadModule('user-data', { UDT_APPDATA_OVERRIDE: configured }, { electron: { app } })
    userData.configureUserDataDirectory()
    assert.equal(userData.dataDirectoryOverride(), undefined)
    assert.deepEqual(paths, [])
  }
})
