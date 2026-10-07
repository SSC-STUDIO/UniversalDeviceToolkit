import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { mkdtemp, mkdir, readFile, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { crc32 } from 'node:zlib'
import { getMakeNsisPath } from 'app-builder-lib/out/toolsets/windows.js'
import { bootstrapScript, ownershipUninstallFunctions, prepareSetup } from '../scripts/lightweight-installer.mjs'
import { compatibilityScript } from '../scripts/compatibility-installer.mjs'

function execute(command, args, env = process.env, options = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { env, windowsHide: true, ...options })
    let diagnostic = ''
    child.stdout.on('data', chunk => { diagnostic += chunk })
    child.stderr.on('data', chunk => { diagnostic += chunk })
    child.once('error', reject)
    child.once('exit', code => resolve({ code, diagnostic }))
  })
}

async function holdReadLock(path, context) {
  const literal = "'" + path.replaceAll("'", "''") + "'"
  const command = `$lock = [System.IO.File]::Open(${literal}, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read); [Console]::Out.WriteLine('locked'); [Console]::Out.Flush(); [Console]::ReadLine() | Out-Null; $lock.Dispose()`
  const child = spawn('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', command], { windowsHide: true })
  context.after(() => { if (child.exitCode === null) child.kill() })
  const exited = new Promise((resolve, reject) => {
    child.once('error', reject)
    child.once('exit', code => resolve(code))
  })
  let diagnostic = ''
  child.stderr.on('data', chunk => { diagnostic += chunk })
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Read lock helper did not start: ' + diagnostic)), 10000)
    let output = ''
    child.stdout.on('data', chunk => {
      output += chunk
      if (output.includes('locked')) { clearTimeout(timer); resolve() }
    })
    child.once('error', error => { clearTimeout(timer); reject(error) })
    child.once('exit', code => { clearTimeout(timer); reject(new Error(`Read lock helper exited (${code}): ${diagnostic}`)) })
  })
  return async () => {
    child.stdin.end('\n')
    assert.equal(await exited, 0, diagnostic)
  }
}

test('ownership uninstaller rejects paths that cannot be represented safely', () => {
  for (const path of ['../outside.dll', 'file.dll:stream', 'folder/*.dll', 'bad\r\nline', '\\root.dll', 'trailing.']) {
    assert.throws(() => ownershipUninstallFunctions([path]), /Invalid installation manifest entry/)
  }
})

test('complete native registration helper compiles ownership cleanup with no warnings', {
  skip: process.platform !== 'win32'
}, async (context) => {
  const work = await mkdtemp(join(tmpdir(), 'udt-registration-'))
  context.after(() => rm(work, { recursive: true, force: true }))
  const payload = join(work, 'payload')
  await mkdir(payload)
  await writeFile(join(payload, 'UniversalDeviceToolkit.exe'), 'fixture payload')
  const compiler = await getMakeNsisPath()
  const project = dirname(dirname(fileURLToPath(import.meta.url)))
  await prepareSetup(payload, project, '6.1.4', compiler.path, async (command, args, options = {}) => {
    const checked = command === compiler.path ? ['/INPUTCHARSET', 'UTF8', '/WX', ...args] : args
    const { env, ...launchOptions } = options
    const executed = await execute(command, checked, { ...process.env, ...compiler.env, ...env }, launchOptions)
    assert.equal(executed.code, 0, executed.diagnostic)
    assert.equal(executed.diagnostic, '')
  })
  const uninstaller = await readFile(join(payload, 'resources/setup/uninstall.exe'))
  assert.equal(uninstaller.subarray(0, 2).toString(), 'MZ')
  assert.ok(uninstaller.includes(Buffer.from('level="requireAdministrator"')), 'The uninstaller must request elevation.')
  const signature = Buffer.from('efbeadde4e756c6c736f6674496e7374', 'hex')
  const headerOffset = uninstaller.indexOf(signature) - 4
  assert.ok(headerOffset >= 512, 'The generated uninstaller lost its NSIS data header.')
  const checksumOffset = headerOffset + uninstaller.readUInt32LE(headerOffset + 24) - 4
  assert.equal(uninstaller.readUInt32LE(checksumOffset), crc32(uninstaller.subarray(512, checksumOffset)),
    'The generated uninstaller fails its NSIS CRC check after resource editing.')
})

test('explicit NSIS destination with spaces overrides the existing install location in both packages', {
  skip: process.platform !== 'win32'
}, async (context) => {
  const work = await mkdtemp(join(tmpdir(), 'udt-nsis-destination-'))
  context.after(() => rm(work, { recursive: true, force: true }))
  const existing = join(work, 'existing install')
  const requested = join(work, 'new requested install')
  const result = join(work, 'destination.txt')
  const resources = join(dirname(dirname(fileURLToPath(import.meta.url))), 'buildResources')
  const compiler = await getMakeNsisPath()
  const escape = value => value.replaceAll('$', '$$').replaceAll('"', '$\\"')
  for (const variant of ['webview2', 'compatibility']) {
    const output = join(work, `${variant}.exe`)
    let script = variant === 'webview2'
      ? bootstrapScript(join(work, 'payload'), output, join(resources, 'icon.ico'))
      : compatibilityScript(join(work, 'payload'), output, resources)
    script = script.replace(/^InstallDirRegKey .*\r?\n/m, '')
      .replace(/^ {2}ReadRegStr \$0 HKLM .*"InstallLocation"\r?$/m, `  StrCpy $0 "${escape(existing)}"`)
      .replace(/^Section[\s\S]*?SectionEnd/m, `Section
  FileOpen $0 "${escape(result)}" w
  FileWriteUTF16LE $0 "$INSTDIR"
  FileClose $0
SectionEnd`)
      .replace('RequestExecutionLevel admin', 'RequestExecutionLevel user')
    const source = join(work, `${variant}.nsi`)
    await writeFile(source, script, 'utf8')
    const compiled = await execute(compiler.path, ['/INPUTCHARSET', 'UTF8', '/V2', '/WX', source], {
      ...process.env, ...compiler.env
    })
    assert.equal(compiled.code, 0, compiled.diagnostic)
    assert.equal(compiled.diagnostic, '')
    let launched = await execute(output, ['/S'])
    assert.equal(launched.code, 0, launched.diagnostic)
    assert.equal(await readFile(result, 'utf16le'), existing, variant)
    launched = await execute(output, ['/S', '/D=' + requested], process.env, { windowsVerbatimArguments: true })
    assert.equal(launched.code, 0, launched.diagnostic)
    assert.equal(await readFile(result, 'utf16le'), requested, variant)
  }
})

test('native uninstaller deletes only selected owned files and refuses directory links', {
  skip: process.platform !== 'win32'
}, async (context) => {
  const work = await mkdtemp(join(tmpdir(), 'udt-owned-uninstaller-'))
  context.after(() => rm(work, { recursive: true, force: true }))
  const destination = join(work, 'installed with spaces')
  const outside = join(work, 'outside')
  const unicodeFile = 'unicode-\u65e5.dll'
  const known = ['UniversalDeviceToolkit.exe', 'owned.dll', 'UniversalDeviceToolkit.NetworkProxy.dll', 'linked/owned.dll', unicodeFile]
  const script = join(work, 'uninstaller.nsi')
  const generator = join(work, 'generator.exe')
  const completion = join(work, 'completed.txt')
  const escape = value => value.replaceAll('$', '$$').replaceAll('"', '$\\"')
  await mkdir(destination, { recursive: true })
  await writeFile(script, `Unicode true
Name "Ownership fixture"
OutFile "${escape(generator)}"
InstallDir "${escape(destination)}"
RequestExecutionLevel user
SilentInstall silent
!include "FileFunc.nsh"
!include "TextFunc.nsh"
${ownershipUninstallFunctions(known)}
Section
  WriteUninstaller "$INSTDIR\\Uninstall.exe"
SectionEnd
Section "Uninstall"
  \${GetParameters} $0
  ClearErrors
  \${GetOptions} $0 "/LENGTHCHECK" $1
  IfErrors normalRemoval
  StrCpy $INSTDIR "C:\\root"
  IntOp $4 \${NSIS_MAX_STRLEN} - 4
makeLongRoot:
  StrLen $7 $INSTDIR
  IntCmp $7 $4 longRootReady appendLongRoot longRootReady
appendLongRoot:
  StrCpy $INSTDIR "$INSTDIR\\a"
  Goto makeLongRoot
longRootReady:
  StrCpy $1 "owned.dll"
  StrCpy $3 0
  Call un.DeleteOwnedPath
  Goto finishedRemoval
normalRemoval:
  Call un.DeleteOwnedFiles
finishedRemoval:
  FileOpen $0 "${escape(completion)}" w
  FileWrite $0 "$3|$5"
  FileClose $0
  SetErrorLevel $3
SectionEnd
`, 'utf8')
  const compiler = await getMakeNsisPath()
  const compiled = await execute(compiler.path, ['/INPUTCHARSET', 'UTF8', '/V2', '/WX', script], {
    ...process.env, ...compiler.env
  })
  assert.equal(compiled.code, 0, compiled.diagnostic)
  assert.equal(compiled.diagnostic, '')

  const writeInstallation = async extra => {
    await rm(completion, { force: true })
    await mkdir(join(destination, 'resources'), { recursive: true })
    for (const file of ['UniversalDeviceToolkit.exe', 'owned.dll', unicodeFile]) {
      await writeFile(join(destination, file), 'installed')
    }
    await writeFile(join(destination, 'UniversalDeviceToolkit.NetworkProxy.dll'), 'user file in omitted feature path')
    await writeFile(join(destination, 'keep.txt'), 'user file')
    await writeFile(join(destination, 'installer-selection.ini'), 'selected')
    await writeFile(join(destination, 'resources/install-files.json'), '[]')
    await writeFile(join(destination, 'resources/install-files.txt'), [
      'UniversalDeviceToolkit.exe', 'owned.dll', unicodeFile, 'installer-selection.ini', 'Uninstall.exe',
      'resources\\install-files.json', 'resources\\install-files.txt', ...extra
    ].join('\r\n') + '\r\n', 'utf16le')
    const generated = await execute(generator, ['/S'])
    assert.equal(generated.code, 0, generated.diagnostic)
  }
  // NSIS starts a temporary copy; wait for that process rather than only its launcher.
  const uninstall = async (args = [], expectedError) => {
    const launched = await execute(join(destination, 'Uninstall.exe'), ['/S', ...args])
    assert.equal(launched.code, 0, launched.diagnostic)
    const deadline = Date.now() + 10000
    while (Date.now() < deadline) {
      try {
        const [code, error] = (await readFile(completion, 'utf8')).split('|').map(Number)
        if (expectedError !== undefined) assert.equal(error, expectedError)
        return code
      }
      catch (error) { if (error.code !== 'ENOENT') throw error }
      await new Promise(resolve => setTimeout(resolve, 50))
    }
    throw new Error('The temporary uninstaller did not complete.')
  }
  await writeInstallation(['..\\outside.txt', 'owned*.dll', join(outside, 'owned.dll')])
  assert.equal(await uninstall(['/LENGTHCHECK'], 206), 1)
  assert.equal(await readFile(join(destination, 'owned.dll'), 'utf8'), 'installed')
  await rm(completion)
  assert.equal(await uninstall(), 0)
  for (const file of ['UniversalDeviceToolkit.exe', 'owned.dll', unicodeFile, 'resources/install-files.txt', 'Uninstall.exe']) {
    await assert.rejects(readFile(join(destination, file)), { code: 'ENOENT' })
  }
  assert.equal(await readFile(join(destination, 'UniversalDeviceToolkit.NetworkProxy.dll'), 'utf8'), 'user file in omitted feature path')
  assert.equal(await readFile(join(destination, 'keep.txt'), 'utf8'), 'user file')

  await writeInstallation([])
  const release = await holdReadLock(join(destination, 'resources/install-files.txt'), context)
  assert.equal(await uninstall(), 1)
  assert.equal((await readFile(join(destination, 'Uninstall.exe'))).subarray(0, 2).toString(), 'MZ')
  assert.equal(await readFile(join(destination, 'resources/install-files.json'), 'utf8'), '[]')
  assert.equal(await readFile(join(destination, 'installer-selection.ini'), 'utf8'), 'selected')
  assert.ok((await readFile(join(destination, 'resources/install-files.txt'), 'utf16le')).includes('owned.dll'))
  assert.equal(await readFile(join(destination, 'keep.txt'), 'utf8'), 'user file')
  await release()
  await rm(completion)
  assert.equal(await uninstall(), 0)
  await assert.rejects(readFile(join(destination, 'Uninstall.exe')), { code: 'ENOENT' })

  await mkdir(outside)
  await writeFile(join(outside, 'owned.dll'), 'must survive')
  await symlink(outside, join(destination, 'linked'), 'junction')
  await writeInstallation(['linked\\owned.dll'])
  assert.equal(await uninstall(), 1)
  assert.equal(await readFile(join(outside, 'owned.dll'), 'utf8'), 'must survive')
  assert.equal((await readFile(join(destination, 'Uninstall.exe'))).subarray(0, 2).toString(), 'MZ')
  assert.ok((await readFile(join(destination, 'resources/install-files.txt'))).length > 0)
  await rm(join(destination, 'linked'))
  await rm(completion)
  assert.equal(await uninstall(), 0)
  await assert.rejects(readFile(join(destination, 'Uninstall.exe')), { code: 'ENOENT' })
})
