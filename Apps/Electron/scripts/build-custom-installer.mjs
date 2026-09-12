import { spawn } from 'node:child_process'
import { access } from 'node:fs/promises'
import { constants } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const projectRoot = dirname(dirname(fileURLToPath(import.meta.url)))

function run(command, args) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd: projectRoot, stdio: 'inherit', shell: process.platform === 'win32' })
    child.once('error', reject)
    child.once('exit', (code) => code === 0 ? resolve() : reject(new Error(`${command} exited with code ${code ?? 'unknown'}`)))
  })
}

const packageRunner = process.platform === 'win32' ? 'npx.cmd' : 'npx'

await run(packageRunner, ['electron-builder', '--config', 'electron-builder.yml', '--win', 'dir'])
await access(join(projectRoot, 'dist', 'win-unpacked', 'UniversalDeviceToolkit.exe'), constants.F_OK)
await run(packageRunner, ['electron-builder', '--config', 'custom-installer.yml', '--win', 'portable'])
