import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { createHash } from 'node:crypto'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

function executePowerShell(command, env) {
  return new Promise((resolve, reject) => {
    const child = spawn('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', command], {
      env: { ...process.env, ...env,
        PSModulePath: join(process.env.SystemRoot ?? 'C:/Windows', 'System32/WindowsPowerShell/v1.0/Modules') }, windowsHide: true
    })
    let diagnostic = ''
    child.stdout.on('data', chunk => { diagnostic += chunk })
    child.stderr.on('data', chunk => { diagnostic += chunk })
    child.once('error', reject)
    child.once('exit', code => resolve({ code, diagnostic }))
  })
}

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
$missingShortcut = Join-Path $env:UDT_VERIFICATION_FIXTURE 'removed-parent\\missing.lnk'
Restore-ShortcutBackup @(@{ Path = $missingShortcut; Bytes = $null; ParentExisted = $false })
if ([IO.Directory]::Exists([IO.Path]::GetDirectoryName($missingShortcut))) { throw 'Recovery recreated an absent shortcut parent' }
$savedShortcut = Join-Path $env:UDT_VERIFICATION_FIXTURE 'restore-parent\\saved.lnk'
Restore-ShortcutBackup @(@{ Path = $savedShortcut; Bytes = [byte[]](1,2,3); ParentExisted = $true })
if (([IO.File]::ReadAllBytes($savedShortcut) -join ',') -ne '1,2,3') { throw 'Shortcut bytes were not restored' }
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
  if ((Get-RegistryFingerprint $snapshot) -ne (Get-RegistryFingerprint (Get-RegistrySnapshot $key))) { throw 'Registry fingerprint changed after restoring equivalent values' }
}
finally {
  $key.Dispose()
  [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($registryPath)
}
Write-Output 'Isolated verification fixtures passed.'
`
  const result = await executePowerShell(command, { UDT_VERIFICATION_SCRIPT: script, UDT_VERIFICATION_FIXTURE: work })
  assert.equal(result.code, 0, result.diagnostic)
  assert.match(result.diagnostic, /Isolated verification fixtures passed/)
})

test('verification timeout stops a launched child after its parent exits and leaves outside same-name processes running', {
  skip: process.platform !== 'win32'
}, async context => {
  const work = await mkdtemp(join(tmpdir(), 'udt-verification-owned-processes-'))
  context.after(() => rm(work, { recursive: true, force: true }))
  await writeFile(join(work, 'child.ps1'), 'Start-Sleep -Seconds 60\n')
  await writeFile(join(work, 'parent.ps1'), `
$child = Start-Process -FilePath powershell.exe -ArgumentList ('-NoProfile -NonInteractive -File "' + $env:UDT_VERIFICATION_FIXTURE + '\\child.ps1"') -PassThru -WindowStyle Hidden
[IO.File]::WriteAllText((Join-Path $env:UDT_VERIFICATION_FIXTURE 'child.pid'), $child.Id.ToString())
$child.Dispose()
`)
  const script = fileURLToPath(new URL('../../../Scripts/Test-WindowsDualInstallers.ps1', import.meta.url))
  const command = `
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$errors = $null
$tokens = $null
$scriptAst = [Management.Automation.Language.Parser]::ParseFile($env:UDT_VERIFICATION_SCRIPT, [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw ($errors.Message -join '; ') }
foreach ($function in $scriptAst.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
    Invoke-Expression $function.Extent.Text
}
$verificationProcesses = New-Object 'System.Collections.Generic.List[object]'
$outside = Start-Process -FilePath powershell.exe -ArgumentList ('-NoProfile -NonInteractive -File "' + $env:UDT_VERIFICATION_FIXTURE + '\\child.ps1"') -PassThru -WindowStyle Hidden
$ownedChild = $null
try {
    $owner = Start-VerificationProcess (Join-Path $PSHOME 'powershell.exe') ('-NoProfile -NonInteractive -File "' + $env:UDT_VERIFICATION_FIXTURE + '\\parent.ps1"')
    $pidPath = Join-Path $env:UDT_VERIFICATION_FIXTURE 'child.pid'
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not [IO.File]::Exists($pidPath) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 25 }
    Assert-Condition ([IO.File]::Exists($pidPath)) 'The owned child did not start.'
    $ownedChild = [Diagnostics.Process]::GetProcessById([int][IO.File]::ReadAllText($pidPath))
    $pinnedHandle = $ownedChild.Handle
    Assert-Condition (-not $owner.WaitForCompletion(200)) 'An active child must keep its invocation incomplete.'
    Stop-VerificationProcesses
    Assert-Condition ($ownedChild.WaitForExit(1000)) 'The owned child was still active at recovery.'
    Assert-Condition (-not $outside.HasExited) 'An outside same-name process was stopped.'
    [IO.File]::WriteAllText((Join-Path $env:UDT_VERIFICATION_FIXTURE 'recovered.txt'), 'safe to restore')
    Write-Output 'Owned timeout recovery passed.'
}
finally {
    Stop-VerificationProcesses
    if ($null -ne $ownedChild) { $ownedChild.Dispose() }
    if (-not $outside.HasExited) { $outside.Kill(); $outside.WaitForExit() }
    $outside.Dispose()
}
`
  const result = await executePowerShell(command, { UDT_VERIFICATION_SCRIPT: script, UDT_VERIFICATION_FIXTURE: work })
  assert.equal(result.code, 0, result.diagnostic)
  assert.match(result.diagnostic, /Owned timeout recovery passed/)
})
