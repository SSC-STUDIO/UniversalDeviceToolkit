import { spawn } from 'node:child_process'
import { readFile, stat, writeFile } from 'node:fs/promises'
import { createHash } from 'node:crypto'
import { createReadStream } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { assertOfflinePayload, auditArtifactFiles } from './package-footprint.mjs'

const projectRoot = dirname(dirname(fileURLToPath(import.meta.url)))
const builder = join(projectRoot, 'node_modules/electron-builder/out/cli/cli.js')

function runBuilder(args) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [builder, ...args, '--publish', 'never'], {
      cwd: projectRoot, stdio: 'inherit', windowsHide: true
    })
    child.once('error', reject)
    child.once('exit', (code) => code === 0
      ? resolve()
      : reject(new Error(`Compatibility installer build exited with code ${code ?? 'unknown'}`)))
  })
}

const hostDirectory = join(projectRoot, '../Host/publish/win-x64')
await stat(join(hostDirectory, 'coreclr.dll'))
// The Full marker keeps offline resources and updater selection compatible.
await writeFile(join(projectRoot, 'resources/install-channel'), 'full', 'ascii')
await runBuilder(['--config', 'electron-builder.yml', '--win', 'dir', '--x64'])
const payload = join(projectRoot, 'dist/win-unpacked')
await assertOfflinePayload(payload)
await runBuilder(['--config', 'compatibility-installer.yml', '--win', 'nsis', '--x64', '--prepackaged', payload])

const { version } = JSON.parse(await readFile(join(projectRoot, 'package.json'), 'utf8'))
const artifact = join(projectRoot, 'dist/compatibility', `UniversalDeviceToolkitCompatibilitySetup-${version}.exe`)
const report = await auditArtifactFiles([artifact], join(projectRoot, 'dist/footprint/compatibility'))
const hash = createHash('sha256')
for await (const chunk of createReadStream(artifact)) hash.update(chunk)
await writeFile(`${artifact}.sha256`, `${hash.digest('hex')}  UniversalDeviceToolkitCompatibilitySetup-${version}.exe\n`, 'utf8')
console.log(`Offline compatibility installer: ${report.artifacts[0].bytes} bytes (${report.artifacts[0].mebibytes} MiB)`)
