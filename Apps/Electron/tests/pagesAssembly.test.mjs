import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import test from 'node:test'

const execute = promisify(execFile)
const repository = fileURLToPath(new URL('../../../', import.meta.url))
const powershell = process.platform === 'win32' ? 'powershell.exe' : 'pwsh'

async function createFile(root, path, contents) {
  const target = join(root, path)
  await mkdir(dirname(target), { recursive: true })
  await writeFile(target, contents, 'utf8')
}

async function assemble(context, { directRelease = false, noRelease = false } = {}) {
  const root = await mkdtemp(join(tmpdir(), 'udt-pages-assembly-'))
  context.after(() => rm(root, { recursive: true, force: true }))
  const workspace = join(root, 'workspace')
  const artifact = join(root, 'release-pages')
  const runner = join(root, 'runner')
  await mkdir(runner, { recursive: true })
  await createFile(workspace, 'Site/index.html', 'site content')
  await createFile(workspace, 'Assets/Logo.png', 'logo fixture')
  await createFile(workspace, 'Assets/Screenshot_main.png', 'screenshot fixture')
  await createFile(workspace, 'Resources/stable/catalog.json', JSON.stringify({ appVersion: '5.0.1' }))
  await createFile(workspace, 'Resources/5.0.1/languages/en.zip', 'archived language fixture')
  await createFile(artifact, 'resources/stable/catalog.json', JSON.stringify({ appVersion: '6.1.4' }))
  await createFile(artifact, 'stable/catalog.json', JSON.stringify({ appVersion: '6.1.4' }))
  await createFile(artifact, 'resources/6.1.4/languages/en.zip', 'current language fixture')
  await createFile(artifact, 'resources/6.1.4/devices/lenovo.zip', 'current device fixture')

  const workflow = await readFile(join(repository, '.github/workflows/pages.yml'), 'utf8')
  const assembly = / {6}- name: Assemble site and resources\r?\n[\s\S]*? {8}run: \|\r?\n([\s\S]*?)\r?\n {6}- name: Upload Pages artifact/.exec(workflow)
  assert.ok(assembly, 'the deployed Pages assembly script must exist')
  const script = assembly[1].split(/\r?\n/).map(line => line.replace(/^ {10}/, '')).join('\n')
    .replaceAll('${{ github.repository }}', 'fixture/repository')
  const mockGitHub = `
$ProgressPreference = 'SilentlyContinue'
$global:LASTEXITCODE = 0
function gh {
  $global:LASTEXITCODE = 0
  if ($args[0] -eq 'run' -and $args[1] -eq 'list') {
    if ($env:UDT_PAGES_NO_RELEASE -eq 'true') { return '[]' }
    return '[{"databaseId":1234}]'
  }
  if ($args[0] -eq 'run' -and $args[1] -eq 'download') {
    if ($args[2] -ne '1234') { throw 'Unexpected release artifact run.' }
    $patternIndex = [Array]::IndexOf($args, '--pattern')
    if ($patternIndex -lt 0 -or $args[$patternIndex + 1] -ne 'pages-resources-v*') {
      throw 'Pages must download only the resource artifact.'
    }
    $directoryIndex = [Array]::IndexOf($args, '--dir')
    if ($directoryIndex -lt 0) { throw 'Missing artifact download directory.' }
    $target = Join-Path $args[$directoryIndex + 1] 'pages-resources-v6.1.4'
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -Path (Join-Path $env:UDT_PAGES_ARTIFACT '*') -Destination $target -Recurse -Force
    return
  }
  throw 'Unexpected GitHub call in the Pages assembly fixture.'
}
`
  await execute(powershell, [
    '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
    '-EncodedCommand', Buffer.from(mockGitHub + script, 'utf16le').toString('base64')
  ], {
    windowsHide: true,
    timeout: 30000,
    env: {
      ...process.env,
      GITHUB_WORKSPACE: workspace,
      RUNNER_TEMP: runner,
      RELEASE_RUN_ID: directRelease ? '1234' : '',
      RELEASE_CONCLUSION: directRelease ? 'success' : '',
      UDT_PAGES_ARTIFACT: artifact,
      UDT_PAGES_NO_RELEASE: String(noRelease)
    }
  })
  return join(workspace, '_pages_site')
}

for (const directRelease of [true, false]) {
  test(`${directRelease ? 'release-triggered' : 'site-only'} Pages deployment preserves the current release catalog and packs`, async context => {
    const output = await assemble(context, { directRelease })
    const stable = JSON.parse(await readFile(join(output, 'resources/stable/catalog.json'), 'utf8'))
    const alias = JSON.parse(await readFile(join(output, 'stable/catalog.json'), 'utf8'))
    assert.equal(stable.appVersion, '6.1.4')
    assert.deepEqual(alias, stable)
    assert.equal(await readFile(join(output, 'resources/6.1.4/languages/en.zip'), 'utf8'), 'current language fixture')
    assert.equal(await readFile(join(output, 'resources/6.1.4/devices/lenovo.zip'), 'utf8'), 'current device fixture')
    assert.equal(await readFile(join(output, 'resources/5.0.1/languages/en.zip'), 'utf8'), 'archived language fixture')
    assert.equal(await readFile(join(output, 'index.html'), 'utf8'), 'site content')
  })
}

test('Pages assembly without a successful Release retains the repository fallback', async context => {
  const output = await assemble(context, { noRelease: true })
  const stable = JSON.parse(await readFile(join(output, 'resources/stable/catalog.json'), 'utf8'))
  const alias = JSON.parse(await readFile(join(output, 'stable/catalog.json'), 'utf8'))
  assert.equal(stable.appVersion, '5.0.1')
  assert.deepEqual(alias, stable)
  assert.equal(await readFile(join(output, 'index.html'), 'utf8'), 'site content')
})
