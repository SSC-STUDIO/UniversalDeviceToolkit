import { createHash } from 'node:crypto'
import { cp, mkdir, mkdtemp, readFile, readdir, rm, stat, writeFile } from 'node:fs/promises'
import { spawn } from 'node:child_process'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parseArgs } from 'node:util'
import { getMakeNsisPath } from 'app-builder-lib/out/toolsets/windows.js'
import { prepareSetup } from './lightweight-installer.mjs'
import { compatibilityScript } from './compatibility-installer.mjs'
import { assertOfflinePayload, auditArtifactFiles } from './package-footprint.mjs'
import { assertSafePackagingDirectories } from './packaging-paths.mjs'

const project = dirname(dirname(fileURLToPath(import.meta.url)))
const repository = resolve(project, '../..')
const { values: options } = parseArgs({ options: {
  'prepare-only': { type: 'boolean' }, 'package-only': { type: 'boolean' },
  'payload-directory': { type: 'string', default: 'dist/compatibility-payload' },
  'output-directory': { type: 'string', default: 'dist/compatibility' }, version: { type: 'string' }
} })
if (process.platform !== 'win32') throw new Error('Compatibility packaging requires Windows.')
if (options['prepare-only'] && options['package-only']) throw new Error('Choose one build phase.')
const payload = resolve(project, options['payload-directory'])
const output = resolve(project, options['output-directory'])
const version = options.version ?? JSON.parse(await readFile(join(project, 'package.json'), 'utf8')).version
if (!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(version)) throw new Error('Invalid release version.')
const allowedRoots = [join(project, 'dist'), join(repository, 'BuildInstallerPayload')]
await assertSafePackagingDirectories(payload, output, allowedRoots)
const compiler = await getMakeNsisPath()
function run(command, args, extra = {}) {
  return new Promise((resolveRun, reject) => {
    const child = spawn(command, args, { cwd: repository, stdio: 'inherit', windowsHide: true,
      ...extra, env: { ...process.env, ...compiler.env, ...extra.env } })
    child.once('error', reject)
    child.once('exit', code => code === 0 ? resolveRun() : reject(new Error(`${command} exited with code ${code}`)))
  })
}

await mkdir(join(project, 'dist'), { recursive: true })
if (!options['package-only']) {
  await stat(join(repository, 'Apps/Host/publish/win-x64/coreclr.dll'))
  await run(process.execPath, [join(project, 'node_modules/electron-builder/out/cli/cli.js'),
    '--config', 'compatibility-installer.yml', `--config.extraMetadata.version=${version}`,
    '--win', 'dir', '--x64', '--publish', 'never'], { cwd: project })
  const unpacked = join(project, 'dist/compatibility/win-unpacked')
  await assertOfflinePayload(unpacked)
  await assertSafePackagingDirectories(payload, output, allowedRoots)
  await rm(payload, { recursive: true, force: true })
  await cp(unpacked, payload, { recursive: true })
  await writeFile(join(payload, 'resources/install-channel'), 'electron-compatibility', 'ascii')
  const helper = await mkdtemp(join(project, 'dist/.compatibility-helper-'))
  try {
    await run('dotnet', ['publish', 'Apps/Windows/UniversalDeviceToolkit.Windows.csproj', '-c', 'Release',
      '-r', 'win-x64', '--self-contained', 'true', '--disable-build-servers', '-m:1', `-p:Version=${version}`, '-o', helper])
    for (const file of await readdir(helper, { withFileTypes: true })) {
      const name = file.name === 'UniversalDeviceToolkit.exe' ? 'UniversalDeviceToolkit.InstallHelper.exe' : file.name
      await cp(join(helper, file.name), join(payload, 'resources/host', name), { recursive: true, force: true })
    }
  } finally { await rm(helper, { recursive: true, force: true }) }
  await prepareSetup(payload, project, version, compiler.path, run)
  await writeFile(join(payload, 'resources/setup/build-version'), version, 'ascii')
}
if (!options['prepare-only']) {
  if ((await readFile(join(payload, 'resources/setup/build-version'), 'ascii')) !== version)
    throw new Error('Prepared payload version does not match the requested release.')
  if ((await readFile(join(payload, 'resources/install-channel'), 'ascii')) !== 'electron-compatibility')
    throw new Error('Prepared payload is not an Electron compatibility application.')
  await assertOfflinePayload(payload)
  await mkdir(output, { recursive: true })
  const scratch = await mkdtemp(join(project, 'dist/.compatibility-package-'))
  try {
    const installer = join(output, `UniversalDeviceToolkitCompatibilitySetup-${version}.exe`)
    const script = join(scratch, 'compatibility.nsi')
    await writeFile(script, compatibilityScript(payload, installer, join(project, 'buildResources')), 'utf8')
    await run(compiler.path, ['/INPUTCHARSET', 'UTF8', '/V2', '/WX', script])
    await auditArtifactFiles([installer], join(output, 'footprint/compatibility'))
    const hash = createHash('sha256').update(await readFile(installer)).digest('hex')
    await writeFile(installer + '.sha256', `${hash}  ${installer.split(/[\\/]/).pop()}\n`, 'utf8')
    console.log(`Electron compatibility installer: ${(await stat(installer)).size} bytes; ${installer}`)
  } finally { await rm(scratch, { recursive: true, force: true }) }
}
