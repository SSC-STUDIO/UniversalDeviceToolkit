import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import test from 'node:test'

const execute = promisify(execFile)
const script = fileURLToPath(new URL('../../../Scripts/New-ReleaseNotes.ps1', import.meta.url))
const powershell = process.platform === 'win32' ? 'powershell.exe' : 'pwsh'
const command = `
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$fixture = $env:UDT_RELEASE_NOTES_FIXTURE | ConvertFrom-Json
$parameters = @{
  Version = $fixture.version
  ChangelogPath = $fixture.changelog
  AssetNames = @($fixture.assets)
}
if ($fixture.output) { $parameters['OutputPath'] = $fixture.output }
if ($fixture.releaseDate) { $parameters['ReleaseDate'] = $fixture.releaseDate }
& $env:UDT_RELEASE_NOTES_SCRIPT @parameters
if (-not $?) { exit 1 }
`

async function generateNotes(context, changelog, options = {}) {
  const directory = await mkdtemp(join(tmpdir(), 'udt-release-notes-'))
  context.after(() => rm(directory, { recursive: true, force: true }))
  const path = join(directory, 'CHANGELOG.md')
  await writeFile(path, changelog, 'utf8')
  const output = options.writeOutput ? join(directory, 'release-notes.md') : undefined
  const result = await execute(powershell, [
    '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
    '-EncodedCommand', Buffer.from(command, 'utf16le').toString('base64')
  ], {
    windowsHide: true,
    env: {
      ...process.env,
      UDT_RELEASE_NOTES_SCRIPT: script,
      UDT_RELEASE_NOTES_FIXTURE: JSON.stringify({
        version: options.version ?? '6.1.4', changelog: path,
        assets: options.assets ?? ['UniversalDeviceToolkit_v6.1.4_SHA256.txt'],
        output, releaseDate: options.releaseDate
      })
    }
  })
  assert.equal(result.stderr, '')
  return output ? readFile(output, 'utf8') : result.stdout
}

const candidate = `# Changelog
## [6.1.4] - Unreleased candidate
### Highlights
- Candidate highlight is retained.
### Fixed
- Candidate fixes are retained.
## [6.1.3] - 2026-10-01
- Historical changes stay in their own version.
`

test('candidate release preparation lists both installers and keeps the unreleased changelog', async context => {
  const notes = await generateNotes(context, candidate, {
    assets: [
      'UniversalDeviceToolkitWebView2Setup-6.1.4.exe',
      'UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe',
      'UniversalDeviceToolkit_v6.1.4_Full_Setup.exe',
      'UniversalDeviceToolkit_v6.1.4_Online_Setup.exe',
      'UniversalDeviceToolkit_v6.1.4_SHA256.txt'
    ],
    releaseDate: '2026-10-07'
  })
  assert.match(notes, /Status: Unreleased candidate/)
  assert.match(notes, /Release date: Not released/)
  assert.match(notes, /this candidate has not been formally released/)
  assert.match(notes, /Candidate highlight is retained/)
  assert.match(notes, /Candidate fixes are retained/)
  assert.doesNotMatch(notes, /Historical changes|No changelog section|Release date: 2026-10-07/)
  const downloads = notes.split('## Downloads\n')[1].split('## Online resources')[0]
  assert.match(downloads, /UniversalDeviceToolkitWebView2Setup-6\.1\.4\.exe/)
  assert.match(downloads, /UniversalDeviceToolkitCompatibilitySetup-6\.1\.4\.exe.*bundled Chromium.*self-contained \.NET Host/)
  assert.match(downloads, /native NSIS installation does not require WebView2 Runtime/)
  assert.equal(downloads.match(/UniversalDeviceToolkitCompatibilitySetup-/g)?.length, 1)
})

test('dated releases retain their recorded date and version-specific changes', async context => {
  const notes = await generateNotes(context, candidate, {
    version: '6.1.3', assets: ['UniversalDeviceToolkit_v6.1.3_Full_Setup.exe']
  })
  assert.match(notes, /Release date: 2026-10-01/)
  assert.match(notes, /Historical changes stay in their own version/)
  assert.doesNotMatch(notes, /Unreleased candidate|Candidate highlight|CompatibilitySetup-/)
})

test('historical releases without a changelog retain the existing fallback', async context => {
  const notes = await generateNotes(context, candidate, {
    version: '4.2.0', assets: ['UniversalDeviceToolkitSetup.exe'], releaseDate: '2025-06-01'
  })
  assert.match(notes, /Release date: 2025-06-01/)
  assert.match(notes, /No changelog section is available for this historical version/)
  assert.match(notes, /`UniversalDeviceToolkitSetup\.exe` - installer package/)
  assert.doesNotMatch(notes, /Status: Unreleased candidate/)
})

test('compatibility-only asset lists the offline package without inventing a primary asset', async context => {
  const notes = await generateNotes(context, candidate, {
    assets: ['UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe']
  })
  assert.match(notes, /CompatibilitySetup-6\.1\.4\.exe.*Electron compatibility installer/)
  assert.doesNotMatch(notes, /WebView2Setup-|No release assets are currently attached/)
})

test('candidate notes saved to a file remain UTF-8 without a BOM', async context => {
  const notes = await generateNotes(context, candidate, { writeOutput: true })
  assert.ok(notes.startsWith('Status: Unreleased candidate\n'))
  assert.match(notes, /Candidate fixes are retained/)
  assert.notEqual(notes.charCodeAt(0), 0xfeff)
})
