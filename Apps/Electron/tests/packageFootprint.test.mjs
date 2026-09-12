import assert from 'node:assert/strict'
import { createPackage } from '@electron/asar'
import { Buffer } from 'node:buffer'
import { mkdtemp, mkdir, open, readFile, readdir, rm, writeFile } from 'node:fs/promises'
import { builtinModules } from 'node:module'
import { tmpdir } from 'node:os'
import { basename, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import {
  BUDGETS,
  CHROMIUM_LOCALES,
  auditArtifactFiles,
  auditPackagedApplication
} from '../scripts/package-footprint.mjs'

const projectDirectory = dirname(dirname(fileURLToPath(import.meta.url)))
const expectedApplicationLanguages = [
  'en', 'zh-CN', 'zh-Hant', 'ja', 'de', 'fr', 'es', 'it', 'pt-BR', 'pt', 'ru',
  'uk', 'pl', 'cs', 'sk', 'hu', 'ro', 'bg', 'tr', 'el', 'ar', 'lv', 'nl-NL',
  'vi', 'uz-Latn-UZ'
]
const rendererPackages = [
  '@fluentui/react-icons', 'antd', 'echarts', 'i18next', 'react', 'react-dom',
  'react-i18next', 'react-router-dom', 'zustand'
]
const nodeBuiltinModules = new Set(builtinModules.flatMap(moduleName => [moduleName, `node:${moduleName}`]))

async function createFixture(options = {}) {
  const root = await mkdtemp(join(tmpdir(), 'udt-package-footprint-'))
  const appDirectory = join(root, 'app')
  const sourceDirectory = join(root, 'asar-source')
  const hostDirectory = join(appDirectory, 'resources', 'host')
  const localesDirectory = join(appDirectory, 'locales')
  const reportDirectory = join(root, 'reports')
  await mkdir(hostDirectory, { recursive: true })
  await mkdir(localesDirectory, { recursive: true })
  await mkdir(sourceDirectory, { recursive: true })
  await writeFile(join(sourceDirectory, 'index.js'), 'export {}\n')

  if (options.includeNodeModules === true) {
    await mkdir(join(sourceDirectory, 'node_modules'), { recursive: true })
    await writeFile(join(sourceDirectory, 'node_modules', 'unexpected.js'), 'export {}\n')
  }
  if (options.appAsarOverBudget === true) {
    await writeFile(join(sourceDirectory, 'large.bin'), Buffer.alloc(BUDGETS.appAsar + 1))
  }
  if (options.includeHostPdb === true) {
    await writeFile(join(hostDirectory, 'UniversalDeviceToolkit.Host.pdb'), 'debug symbols')
  }

  for (const locale of [...CHROMIUM_LOCALES, ...(options.extraLocales ?? [])]) {
    await writeFile(join(localesDirectory, `${locale}.pak`), locale)
  }

  await createPackage(sourceDirectory, join(appDirectory, 'resources', 'app.asar'))

  return {
    appDirectory,
    reportDirectory,
    async dispose() {
      await rm(root, { recursive: true, force: true })
    }
  }
}

async function expectAuditFailure(options, expectedMessage) {
  const fixture = await createFixture(options)
  try {
    await assert.rejects(
      auditPackagedApplication({
        appOutDirectory: fixture.appDirectory,
        runtimeIdentifier: 'win-x64',
        reportDirectory: fixture.reportDirectory
      }),
      expectedMessage
    )
  } finally {
    await fixture.dispose()
  }
}

async function sourceFiles(directory) {
  const files = []
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) files.push(...await sourceFiles(path))
    else if (entry.isFile() && entry.name.endsWith('.ts')) files.push(path)
  }
  return files
}

test('renderer libraries stay in dev dependencies and Host-facing sources have no third-party bare imports', async () => {
  const packageJson = JSON.parse(await readFile(join(projectDirectory, 'package.json'), 'utf8'))
  assert.deepEqual(packageJson.dependencies, {})
  for (const packageName of rendererPackages) {
    assert.equal(packageJson.devDependencies[packageName] !== undefined, true, `${packageName} must remain a build dependency`)
  }

  const sourceRoots = [join(projectDirectory, 'src', 'main'), join(projectDirectory, 'src', 'preload')]
  for (const sourceRoot of sourceRoots) {
    for (const path of await sourceFiles(sourceRoot)) {
      const source = await readFile(path, 'utf8')
      const pattern = /(?:from\s+|import\s*\(|require\()\s*['"]([^'"]+)['"]/g
      for (const match of source.matchAll(pattern)) {
        const specifier = match[1]
        assert.equal(
          specifier.startsWith('.') || nodeBuiltinModules.has(specifier) || specifier === 'electron',
          true,
          `${basename(path)} imports third-party module ${specifier}`
        )
      }
    }
  }
})

test('application and Chromium language contracts retain the planned locale sets', async () => {
  const i18nSource = await readFile(join(projectDirectory, 'src', 'renderer', 'src', 'i18n', 'index.ts'), 'utf8')
  const applicationLanguages = [...i18nSource.matchAll(/code:\s*['"]([^'"]+)['"]/g)].map(match => match[1])
  assert.deepEqual(applicationLanguages, expectedApplicationLanguages)

  const builderConfig = await readFile(join(projectDirectory, 'electron-builder.yml'), 'utf8')
  const localeBlock = builderConfig.match(/electronLanguages:\s*\n((?:\s+- .+\n?)+)/)
  assert.notEqual(localeBlock, null)
  const configuredLocales = [...localeBlock[1].matchAll(/^\s+-\s+(.+)$/gm)].map(match => match[1])
  assert.deepEqual(configuredLocales, CHROMIUM_LOCALES)
})

test('package auditor accepts the exact footprint fixture', async () => {
  const fixture = await createFixture()
  try {
    const report = await auditPackagedApplication({
      appOutDirectory: fixture.appDirectory,
      runtimeIdentifier: 'win-x64',
      reportDirectory: fixture.reportDirectory
    })
    assert.deepEqual(report.failures, [])
    assert.deepEqual(report.measurements.chromiumLocales, [...CHROMIUM_LOCALES].sort())
  } finally {
    await fixture.dispose()
  }
})

test('package auditor rejects node_modules, PDBs, unexpected locales, and over-budget archives', async () => {
  await expectAuditFailure({ includeNodeModules: true }, /node_modules entries/)
  await expectAuditFailure({ includeHostPdb: true }, /Host contains PDB files/)
  await expectAuditFailure({ extraLocales: ['ko'] }, /Chromium locale mismatch/)
  await expectAuditFailure({ appAsarOverBudget: true }, /app\.asar is/)
})

test('artifact auditor applies full and Online package budgets', async () => {
  const root = await mkdtemp(join(tmpdir(), 'udt-artifact-footprint-'))
  try {
    const fullArtifact = join(root, 'UniversalDeviceToolkitSetup.exe')
    const onlineArtifact = join(root, 'UniversalDeviceToolkitOnlineSetup.exe')
    await writeFile(fullArtifact, Buffer.alloc(0))
    await writeFile(onlineArtifact, Buffer.alloc(0))
    const fullHandle = await open(fullArtifact, 'w')
    const onlineHandle = await open(onlineArtifact, 'w')
    await fullHandle.truncate(BUDGETS.distributable + 1)
    await onlineHandle.truncate(BUDGETS.onlineBootstrap + 1)
    await fullHandle.close()
    await onlineHandle.close()

    await assert.rejects(auditArtifactFiles([fullArtifact], root), /budget is 185 MiB/)
    await assert.rejects(auditArtifactFiles([onlineArtifact], root), /budget is 85 MiB/)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})
