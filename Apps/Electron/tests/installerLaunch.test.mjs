import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { EventEmitter } from 'node:events'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { join } from 'node:path'
import { Readable } from 'node:stream'
import test from 'node:test'
import vm from 'node:vm'
import ts from 'typescript'

const require = createRequire(import.meta.url)
const source = readFileSync(new URL('../src/main/update-downloader.ts', import.meta.url), 'utf8')
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText

function launcher(directSuccess = false) {
  const module = { exports: {} }
  const calls = []
  const bytes = Buffer.from('verified update installer')
  const userData = join(process.cwd(), '__update-fixture')
  const path = join(userData, 'updates', "setup's.exe")
  let quits = 0
  vm.runInNewContext(compiled + '\nexports.recordVerifiedInstaller = recordVerifiedInstaller;', {
    module, exports: module.exports, console, Buffer,
    process: { platform: 'win32', resourcesPath: 'C:/app/resources', env: {} },
    require(name) {
      if (name === 'electron') return { app: { getPath: () => userData, getAppPath: () => 'C:/app', quit: () => quits++ } }
      if (name === 'fs') return {
        ...require('node:fs'), existsSync: () => true, readFileSync: () => 'electron-compatibility',
        mkdirSync() {}, unlinkSync() {}, createReadStream: () => Readable.from([bytes])
      }
      if (name === 'child_process') return { spawn(command, args, options) {
        const child = new EventEmitter()
        child.stderr = new EventEmitter()
        calls.push({ command, args, options, child })
        queueMicrotask(() => {
          if (command === 'powershell.exe' || directSuccess) child.emit('spawn')
          else child.emit('error', new Error('Elevation required'))
        })
        return child
      } }
      return require(name)
    }
  })
  module.exports.recordVerifiedInstaller(path, createHash('sha256').update(bytes).digest('hex'))
  return { calls, launch: () => module.exports.launchInstaller(path), quits: () => quits }
}

async function waitForHelper(state) {
  await new Promise(setImmediate)
  assert.equal(state.calls.length, 2)
  assert.equal(state.calls[1].command, 'powershell.exe')
  return state.calls[1]
}

test('elevation cancellation reports failure and keeps the application open', async () => {
  const state = launcher()
  let settled = false
  const launching = state.launch().then((result) => { settled = true; return result })
  const helper = await waitForHelper(state)
  assert.equal(settled, false)
  assert.equal(state.quits(), 0)
  assert.equal(helper.options.windowsHide, true)
  assert.match(helper.args.at(-1), /setup''s\.exe/)
  assert.match(helper.args.at(-1), /-ErrorAction Stop/)
  helper.child.stderr.emit('data', Buffer.from('The operation was canceled by the user.'))
  helper.child.emit('close', 1)
  const result = await launching
  assert.equal(result.ok, false)
  assert.equal(result.error, 'The operation was canceled by the user.')
  assert.equal(state.quits(), 0)
})

test('application exits only after the elevation helper confirms the installer started', async () => {
  const state = launcher()
  const launching = state.launch()
  const helper = await waitForHelper(state)
  assert.equal(state.quits(), 0)
  helper.child.emit('close', 0)
  assert.equal((await launching).ok, true)
  assert.equal(state.quits(), 1)
})

test('a missing elevation helper returns its launch error without quitting', async () => {
  const state = launcher()
  const launching = state.launch()
  const helper = await waitForHelper(state)
  helper.child.emit('error', new Error('PowerShell not found'))
  const result = await launching
  assert.equal(result.ok, false)
  assert.equal(result.error, 'PowerShell not found')
  assert.equal(state.quits(), 0)
})

test('legacy asInvoker installers retain the direct silent launch path', async () => {
  const state = launcher(true)
  assert.equal((await state.launch()).ok, true)
  assert.equal(state.calls.length, 1)
  assert.deepEqual(Array.from(state.calls[0].args), ['/S'])
  assert.equal(state.quits(), 1)
})
