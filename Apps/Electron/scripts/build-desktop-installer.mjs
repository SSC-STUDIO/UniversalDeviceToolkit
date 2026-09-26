import { spawn } from 'node:child_process'
import { fileURLToPath } from 'node:url'

if (process.platform === 'win32') {
  await import('./build-windows-installer.mjs')
} else {
  const builder = fileURLToPath(new URL('../node_modules/electron-builder/out/cli/cli.js', import.meta.url))
  const project = fileURLToPath(new URL('..', import.meta.url))
  const code = await new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [builder, '--config', 'electron-builder.yml',
      process.platform === 'darwin' ? '--mac' : '--linux', '--publish', 'never'], { cwd: project, stdio: 'inherit' })
    child.once('error', reject)
    child.once('exit', resolve)
  })
  if (code !== 0) throw new Error(`Desktop packaging exited with code ${code ?? 'unknown'}`)
}
