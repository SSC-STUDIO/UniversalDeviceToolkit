import { createHash } from 'node:crypto'
import { cp, mkdir, mkdtemp, readFile, readdir, rm, stat, writeFile } from 'node:fs/promises'
import { spawn } from 'node:child_process'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { auditArtifactFiles } from './package-footprint.mjs'

const projectRoot = dirname(dirname(fileURLToPath(import.meta.url)))
const repositoryRoot = resolve(projectRoot, '../..')
const hostSource = join(repositoryRoot, 'Apps/Host/publish/win-x64')
const rendererSource = join(projectRoot, 'out/renderer')
const outputDirectory = join(projectRoot, 'dist/lightweight')
const maxBytes = 40_000_000

function run(command, argumentsList, options = {}) {
  return new Promise((resolvePromise, reject) => {
    const child = spawn(command, argumentsList, { ...options, stdio: 'inherit', windowsHide: true })
    child.once('error', reject)
    child.once('exit', code => code === 0
      ? resolvePromise()
      : reject(new Error(`${command} exited with code ${code ?? 'unknown'}`)))
  })
}

async function copyDirectoryWithoutDiagnostics(source, destination) {
  for (const entry of await readdir(source, { withFileTypes: true })) {
    if (entry.name === 'amd64') continue
    await cp(join(source, entry.name), join(destination, entry.name), { recursive: true, force: true })
  }
}

async function sha256(path) {
  const hash = createHash('sha256')
  hash.update(await readFile(path))
  return hash.digest('hex')
}

async function findMakensis() {
  const localAppData = process.env.LOCALAPPDATA
  if (!localAppData) throw new Error('LOCALAPPDATA is required to locate the NSIS tool cache.')
  const cache = join(localAppData, 'electron-builder/Cache')
  const entries = await readdir(cache, { recursive: true, withFileTypes: true }).catch(() => [])
  const match = entries.find(entry => entry.isFile() && entry.name.toLowerCase() === 'makensis.exe')
  if (!match) throw new Error('makensis.exe was not found in the electron-builder cache.')
  return join(match.parentPath, match.name)
}

if (process.platform !== 'win32') throw new Error('The lightweight Windows payload can only be built on Windows.')
await stat(join(hostSource, 'UniversalDeviceToolkit.Host.exe'))
await stat(join(rendererSource, 'index.html'))

const workDirectory = await mkdtemp(join(projectRoot, 'dist/.lightweight-'))
try {
  const shellPublish = join(workDirectory, 'shell')
  const payload = join(workDirectory, 'payload')
  await run('dotnet', [
    'publish', join(repositoryRoot, 'Apps/Windows/UniversalDeviceToolkit.Windows.csproj'),
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '--disable-build-servers', '-m:1', '-p:RestoreLockedMode=false', '-o', shellPublish
  ], { cwd: repositoryRoot })
  await cp(shellPublish, payload, { recursive: true, force: true })
  await copyDirectoryWithoutDiagnostics(hostSource, payload)
  await cp(rendererSource, join(payload, 'resources/ui'), { recursive: true, force: true })
  // A healthy Host alone cannot detect an invisible or blank WebView renderer.
  await run(join(payload, 'UniversalDeviceToolkit.exe'), ['--diagnose-ui'], { cwd: payload, timeout: 120_000 })

  const ddf = join(workDirectory, 'payload.ddf')
  const lines = [
    '.OPTION EXPLICIT',
    '.Set CabinetNameTemplate=UniversalDeviceToolkitLightweight.cab',
    `.Set DiskDirectoryTemplate=${workDirectory}`,
    `.Set InfFileName=${join(workDirectory, 'setup.inf')}`,
    `.Set RptFileName=${join(workDirectory, 'setup.rpt')}`,
    '.Set CompressionType=LZX',
    '.Set CompressionMemory=21',
    '.Set Cabinet=on',
    '.Set MaxDiskSize=0'
  ]
  for (const entry of await readdir(payload, { recursive: true, withFileTypes: true })) {
    if (!entry.isFile()) continue
    const absolute = join(entry.parentPath, entry.name)
    const relative = absolute.slice(payload.length + 1)
    lines.push(`"${absolute}" "${relative}"`)
  }
  await writeFile(ddf, `${lines.join('\n')}\n`, 'ascii')
  await run('makecab.exe', ['/F', ddf], { cwd: repositoryRoot })

  const version = JSON.parse(await readFile(join(projectRoot, 'package.json'), 'utf8')).version
  const cab = join(workDirectory, 'UniversalDeviceToolkitLightweight.cab')
  const size = (await stat(cab)).size
  if (size >= maxBytes) throw new Error(`Lightweight payload is ${size} bytes; limit is ${maxBytes}.`)
  await mkdir(outputDirectory, { recursive: true })
  const payloadOutput = join(outputDirectory, `UniversalDeviceToolkitLightweightPayload-${version}.cab`)
  await cp(cab, payloadOutput, { force: true })
  await writeFile(`${payloadOutput}.sha256`, `${await sha256(payloadOutput)}  ${payloadOutput.split(/[\\/]/).pop()}\n`, 'utf8')

  const installerOutput = join(outputDirectory, `UniversalDeviceToolkitLightweightSetup-${version}.exe`)
  const script = join(workDirectory, 'installer.nsi')
  const escapedPayload = payload.replaceAll('\\', '\\\\')
  const escapedInstaller = installerOutput.replaceAll('\\', '\\\\')
  await writeFile(script, `!include "MUI2.nsh"\nUnicode true\nName "Universal Device Toolkit (WebView2)"\nOutFile "${escapedInstaller}"\nInstallDir "$PROGRAMFILES64\\Universal Device Toolkit"\nRequestExecutionLevel admin\nSetCompressor /SOLID lzma\n!define WEBVIEW2_GUID "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"\nFunction .onInit\n  ReadRegStr $0 HKLM "SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdate\\Clients\\\${WEBVIEW2_GUID}" "pv"\n  StrCmp $0 "" 0 +4\n  ReadRegStr $0 HKCU "SOFTWARE\\Microsoft\\EdgeUpdate\\Clients\\\${WEBVIEW2_GUID}" "pv"\n  StrCmp $0 "" 0 +2\n  MessageBox MB_ICONSTOP "Microsoft Edge WebView2 Runtime is required. Use the offline compatibility installer to install without this prerequisite."\n  StrCmp $0 "" 0 +2\n  Abort\nFunctionEnd\nSection\n  SetOutPath "$INSTDIR"\n  File /r "${escapedPayload}\\*"\n  WriteUninstaller "$INSTDIR\\Uninstall.exe"\n  CreateDirectory "$SMPROGRAMS\\Universal Device Toolkit"\n  CreateShortCut "$SMPROGRAMS\\Universal Device Toolkit\\Universal Device Toolkit.lnk" "$INSTDIR\\UniversalDeviceToolkit.exe"\n  CreateShortCut "$DESKTOP\\Universal Device Toolkit.lnk" "$INSTDIR\\UniversalDeviceToolkit.exe"\nSectionEnd\nSection "Uninstall"\n  Delete "$DESKTOP\\Universal Device Toolkit.lnk"\n  Delete "$SMPROGRAMS\\Universal Device Toolkit\\Universal Device Toolkit.lnk"\n  RMDir "$SMPROGRAMS\\Universal Device Toolkit"\n  RMDir /r "$INSTDIR"\nSectionEnd\n`, 'ascii')
  await run(await findMakensis(), ['/V2', script], { cwd: projectRoot })
  const installerSize = (await stat(installerOutput)).size
  if (installerSize >= maxBytes) throw new Error(`Lightweight installer is ${installerSize} bytes; limit is ${maxBytes}.`)
  await writeFile(`${installerOutput}.sha256`, `${await sha256(installerOutput)}  ${installerOutput.split(/[\\/]/).pop()}\n`, 'utf8')
  await auditArtifactFiles([payloadOutput, installerOutput], join(projectRoot, 'dist/footprint/lightweight'))
  console.log(`Lightweight WebView2 payload: ${size} bytes; installer: ${installerSize} bytes`)
} finally {
  await rm(workDirectory, { recursive: true, force: true })
}
