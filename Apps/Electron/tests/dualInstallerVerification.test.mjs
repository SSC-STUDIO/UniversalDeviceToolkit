import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { createHash } from 'node:crypto'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

test('dual-package verification safely parses, rejects corrupt packages and restores registry value types', {
  skip: process.platform !== 'win32'
}, async context => {
  const work = await mkdtemp(join(tmpdir(), 'udt-verification-functions-'))
  context.after(() => rm(work, { recursive: true, force: true }))
  const packageName = 'UniversalDeviceToolkitWebView2Setup-6.1.4.exe'
  const packagePath = join(work, packageName)
  const manifest = join(work, 'SHA256.txt')
  await writeFile(packagePath, 'fixture')
  const hash = createHash('sha256').update('fixture').digest('hex')
  await writeFile(manifest, `${hash}  ${packageName}\n`)
  const script = fileURLToPath(new URL('../../../Scripts/Test-WindowsDualInstallers.ps1', import.meta.url))
  const command = `
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$errors = $null
$tokens = $null
$scriptAst = [Management.Automation.Language.Parser]::ParseFile($env:UDT_VERIFICATION_SCRIPT, [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw ($errors.Message -join '; ') }
$functions = $scriptAst.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)
foreach ($function in $functions) { Invoke-Expression $function.Extent.Text }
$package = Join-Path $env:UDT_VERIFICATION_FIXTURE '${packageName}'
$manifest = Join-Path $env:UDT_VERIFICATION_FIXTURE 'SHA256.txt'
$verified = Get-VerifiedPackage $package '^UniversalDeviceToolkitWebView2Setup-.+\\.exe$' $manifest
if ($verified.Hash -ne '${hash}') { throw 'Hash fixture failed' }
Assert-NoLinks $package
[IO.File]::WriteAllText($package, 'tampered')
$rejected = $false
try { Get-VerifiedPackage $package '^UniversalDeviceToolkitWebView2Setup-.+\\.exe$' $manifest | Out-Null }
catch { $rejected = $true }
if (-not $rejected) { throw 'Tampered package accepted' }
$registryPath = 'Software\\UniversalDeviceToolkit.Tests\\' + [Guid]::NewGuid().ToString('N')
$key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($registryPath)
try {
  $key.SetValue('Raw', '%TEMP%\\original', [Microsoft.Win32.RegistryValueKind]::ExpandString)
  $key.SetValue('Bytes', [byte[]](1,2,3), [Microsoft.Win32.RegistryValueKind]::Binary)
  $child = $key.CreateSubKey('Nested')
  $child.SetValue('Flags', [string[]]('a','b'), [Microsoft.Win32.RegistryValueKind]::MultiString)
  $child.Dispose()
  $snapshot = Get-RegistrySnapshot $key
  $key.DeleteValue('Raw')
  $key.DeleteSubKeyTree('Nested')
  Write-RegistrySnapshot $key $snapshot
  if ($key.GetValue('Raw', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) -ne '%TEMP%\\original') { throw 'Raw registry snapshot failed' }
  if (($key.GetValue('Bytes') -join ',') -ne '1,2,3') { throw 'Binary registry snapshot failed' }
  $child = $key.OpenSubKey('Nested')
  try { if (($child.GetValue('Flags') -join ',') -ne 'a,b') { throw 'Nested registry snapshot failed' } }
  finally { $child.Dispose() }
}
finally {
  $key.Dispose()
  [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($registryPath)
}
Write-Output 'Isolated verification fixtures passed.'
`
  const result = await new Promise((resolve, reject) => {
    const child = spawn('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', command], {
      env: { ...process.env, UDT_VERIFICATION_SCRIPT: script, UDT_VERIFICATION_FIXTURE: work,
        PSModulePath: join(process.env.SystemRoot ?? 'C:/Windows', 'System32/WindowsPowerShell/v1.0/Modules') }, windowsHide: true
    })
    let diagnostic = ''
    child.stdout.on('data', chunk => { diagnostic += chunk })
    child.stderr.on('data', chunk => { diagnostic += chunk })
    child.once('error', reject)
    child.once('exit', code => resolve({ code, diagnostic }))
  })
  assert.equal(result.code, 0, result.diagnostic)
  assert.match(result.diagnostic, /Isolated verification fixtures passed/)
})
