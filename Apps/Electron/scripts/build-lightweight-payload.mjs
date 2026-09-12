import { createHash } from 'node:crypto'
import { cp, mkdir, mkdtemp, readFile, readdir, rm, stat, writeFile } from 'node:fs/promises'
import { spawn } from 'node:child_process'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

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
  await cp(cab, outputDirectory + `/UniversalDeviceToolkitLightweightPayload-${version}.cab`, { force: true })
  const output = join(outputDirectory, `UniversalDeviceToolkitLightweightPayload-${version}.cab`)
  await writeFile(`${output}.sha256`, `${await sha256(output)}  ${output.split(/[\\/]/).pop()}\n`, 'utf8')
  console.log(`Lightweight WebView2 payload: ${size} bytes (${(size / 1_000_000).toFixed(2)} MB)`)
} finally {
  await rm(workDirectory, { recursive: true, force: true })
}
