import { createHash } from 'node:crypto'
import { cp, mkdir, mkdtemp, readFile, readdir, rm, stat, writeFile } from 'node:fs/promises'
import { spawn } from 'node:child_process'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parseArgs } from 'node:util'
import { getMakeNsisPath } from 'app-builder-lib/out/toolsets/windows.js'
import { getPath7za } from 'app-builder-lib/out/toolsets/7zip.js'
import { prepareSetup, bootstrapScript } from './lightweight-installer.mjs'
import { auditArtifactFiles } from './package-footprint.mjs'

const projectRoot = dirname(dirname(fileURLToPath(import.meta.url)))
const repositoryRoot = resolve(projectRoot, '../..')
const { values: options } = parseArgs({ options: {
  'prepare-only': { type: 'boolean' }, 'package-only': { type: 'boolean' },
  'skip-app-check': { type: 'boolean' }, 'use-published-host': { type: 'boolean' },
  'payload-directory': { type: 'string', default: 'dist/.webview2-payload' },
  'output-directory': { type: 'string', default: 'dist/windows' },
  version: { type: 'string' }
} })
if (process.platform !== 'win32') throw new Error('Windows packaging requires Windows.')
if (options['prepare-only'] && options['package-only']) throw new Error('Choose one build phase.')
const payload = resolve(projectRoot, options['payload-directory'])
const outputDirectory = resolve(projectRoot, options['output-directory'])
const version = options.version ?? JSON.parse(await readFile(join(projectRoot, 'package.json'), 'utf8')).version
if (!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(version)) throw new Error('Invalid release version.')
// Only dedicated generated directories may be replaced during preparation.
const allowedRoots = [join(projectRoot, 'dist'), join(repositoryRoot, 'BuildInstallerPayload')]
if (!allowedRoots.some(root => relative(root, payload) && !relative(root, payload).startsWith('..') && !relative(root, payload).includes(':')))
  throw new Error('The payload must be below dist or BuildInstallerPayload.')
if (outputDirectory === payload || outputDirectory.startsWith(payload + '\\') || payload.startsWith(outputDirectory + '\\'))
  throw new Error('Installer output and payload directories must be separate.')
const compiler = await getMakeNsisPath()

function run(command, args, extra = {}) {
  return new Promise((resolveRun, reject) => {
    const child = spawn(command, args, {
      cwd: repositoryRoot, stdio: 'inherit', windowsHide: true,
      ...extra, env: { ...process.env, ...compiler.env, ...extra.env }
    })
    child.once('error', reject)
    child.once('exit', code => code === 0 ? resolveRun() : reject(new Error(`${command} exited with code ${code ?? 'unknown'}`)))
  })
}

async function hash(path) {
  return createHash('sha256').update(await readFile(path)).digest('hex')
}

const host = join(repositoryRoot, 'Apps/Host/publish/win-x64')
if (!options['package-only']) {
  const renderer = join(projectRoot, 'out/renderer')
  await stat(join(renderer, 'index.html'))
  if (!options['use-published-host']) {
    await run('dotnet', ['publish', 'Apps/Host/UniversalDeviceToolkit.Host.csproj', '-c', 'Release', '-r', 'win-x64',
      '--self-contained', 'true', '--disable-build-servers', '-m:1', '-o', host])
  }
  await stat(join(host, 'UniversalDeviceToolkit.Host.exe'))
  await rm(payload, { recursive: true, force: true })
  await run('dotnet', ['publish', 'Apps/Windows/UniversalDeviceToolkit.Windows.csproj', '-c', 'Release', '-r', 'win-x64',
    '--self-contained', 'true', '--disable-build-servers', '-m:1', '-p:RestoreLockedMode=false', `-p:Version=${version}`, '-o', payload])
  for (const entry of await readdir(host, { withFileTypes: true })) {
    if (entry.name !== 'amd64') await cp(join(host, entry.name), join(payload, entry.name), { recursive: true, force: true })
  }
  await cp(renderer, join(payload, 'resources/ui'), { recursive: true, force: true })
  await writeFile(join(payload, 'resources/install-channel'), 'webview2', 'ascii')
  await prepareSetup(payload, projectRoot, version, compiler.path, run)
  if (!options['skip-app-check'])
    await run(join(payload, 'UniversalDeviceToolkit.exe'), ['--diagnose-ui'], { cwd: payload, timeout: 120_000 })
  await run(join(payload, 'UniversalDeviceToolkit.exe'), ['--setup', '--preview', '--diagnose-setup'], { cwd: payload, timeout: 60_000 })
  await writeFile(join(payload, 'resources/setup/build-version'), version, 'ascii')
  console.log(`Prepared WebView2 payload for signing: ${payload}`)
}

if (!options['prepare-only']) {
  if ((await readFile(join(payload, 'resources/setup/build-version'), 'ascii')) !== version)
    throw new Error('Prepared payload version does not match the requested release.')
  if ((await readFile(join(payload, 'resources/install-channel'), 'ascii')) !== 'webview2')
    throw new Error('The prepared payload is not a WebView2 application.')
  if ((await readdir(payload)).some(name => ['chrome_100_percent.pak', 'chrome_200_percent.pak', 'resources.pak'].includes(name)))
    throw new Error('The Windows payload contains a legacy Chromium distribution.')
  await mkdir(outputDirectory, { recursive: true })
  // Release packaging writes the installer outside Apps/Electron, so dist is only the NSIS scratch parent.
  const packageScratch = join(projectRoot, 'dist')
  await mkdir(packageScratch, { recursive: true })
  const work = await mkdtemp(join(packageScratch, '.windows-package-'))
  try {
    const installer = join(outputDirectory, `UniversalDeviceToolkitWebView2Setup-${version}.exe`)
    const script = join(work, 'setup.nsi')
    await writeFile(script, bootstrapScript(payload, installer, join(projectRoot, 'buildResources/icon.ico')), 'utf8')
    await run(compiler.path, ['/V2', script])
    await writeFile(installer + '.sha256', `${await hash(installer)}  ${installer.split(/[\\/]/).pop()}\n`, 'utf8')
    const portable = join(outputDirectory, `UniversalDeviceToolkitWebView2-${version}-win-x64.zip`)
    const files = JSON.parse(await readFile(join(payload, 'resources/setup/files.json'), 'utf8'))
    const list = join(work, 'portable-files.txt')
    await writeFile(list, files.join('\n') + '\n', 'utf8')
    // Installer pages and signing helpers are excluded from the portable app.
    await rm(portable, { force: true })
    await run(await getPath7za(), ['a', '-tzip', '-mx=9', '-scsUTF-8', portable, '@' + list], { cwd: payload })
    await auditArtifactFiles([installer, portable], join(outputDirectory, 'footprint'))
    console.log(`WebView2 installer: ${(await stat(installer)).size} bytes; ${installer}`)
  } finally { await rm(work, { recursive: true, force: true }) }
}
