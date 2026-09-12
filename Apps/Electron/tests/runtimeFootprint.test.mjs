import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const projectDirectory = dirname(dirname(fileURLToPath(import.meta.url)))

test('main process keeps Chromium memory switches and disables spellcheck', async () => {
  const source = await readFile(join(projectDirectory, 'src', 'main', 'index.ts'), 'utf8')
  assert.match(source, /disable-features/)
  assert.match(source, /OutOfBlinkCors/)
  assert.match(source, /TranslateUI/)
  assert.match(source, /MediaRouter/)
  assert.match(source, /disable-background-networking/)
  assert.match(source, /disable-component-update/)
  assert.match(source, /disable-breakpad/)
  assert.match(source, /--optimize-for-size/)
  assert.match(source, /spellcheck:\s*false/)
  assert.match(source, /backgroundThrottling:\s*true/)
})

test('renderer vendor chunks are graph-based so icon barrels stay tree-shaken', async () => {
  const source = await readFile(join(projectDirectory, 'electron.vite.config.ts'), 'utf8')
  assert.match(source, /manualChunks\(id\)/)
  assert.doesNotMatch(source, /icons:\s*\['@fluentui\/react-icons'\]/)
  assert.match(source, /node_modules\/@fluentui\/react-icons/)
  assert.match(source, /node_modules\/echarts/)
})

test('network acceleration polls pause when the UI is hidden', async () => {
  const source = await readFile(
    join(projectDirectory, 'src', 'renderer', 'src', 'pages', 'WindowsOptimizationPage.tsx'),
    'utf8'
  )
  assert.match(source, /subscribeUiVisibility/)
  assert.match(source, /if \(!document\.hidden\) startPolls\(\)/)
})

test('tray background destroys the main window instead of hiding it', async () => {
  const source = await readFile(join(projectDirectory, 'src', 'main', 'index.ts'), 'utf8')
  assert.match(source, /let trayOnlySession = false/)
  assert.match(source, /function enterBackground\(\): void/)
  assert.match(source, /function restoreMainWindow\(route\?: string\): void/)
  assert.match(source, /pending\.destroy\(\)/)
  assert.match(source, /session\.defaultSession\.clearCache\(\)/)
  assert.match(source, /trayOnlySession && isTrayActive\(\)/)
  assert.match(source, /enterBackground\(\)/)
})

test('tray restore recreates a destroyed main window', async () => {
  const tray = await readFile(join(projectDirectory, 'src', 'main', 'tray.ts'), 'utf8')
  assert.match(tray, /restoreWindow\?: \(route\?: string\) => void/)
  assert.match(tray, /restoreWindow\?\.\(route\)/)
  const single = await readFile(join(projectDirectory, 'src', 'main', 'single-instance.ts'), 'utf8')
  assert.match(single, /restoreMainWindow\?\.\(\)/)
})
