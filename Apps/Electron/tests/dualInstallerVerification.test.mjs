import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { createHash } from 'node:crypto'
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
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

test('installer verification reports incomplete after cancellation or an early return and requires successful recovery', {
  skip: process.platform !== 'win32'
}, async context => {
  const work = await mkdtemp(join(tmpdir(), 'udt-verification-completion-'))
  context.after(() => rm(work, { recursive: true, force: true }))
  const script = fileURLToPath(new URL('../../../Scripts/Test-WindowsDualInstallers.ps1', import.meta.url))
  const command = `
$ErrorActionPreference = 'Stop'
$errors = $null
$tokens = $null
$scriptAst = [Management.Automation.Language.Parser]::ParseFile($env:UDT_VERIFICATION_SCRIPT, [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw ($errors.Message -join '; ') }
$scenarioAst = @($scriptAst.EndBlock.Statements | Where-Object {
    $_ -is [Management.Automation.Language.TryStatementAst] -and $_.Body.Extent.Text.Contains('Test-LegacyElectronMigration')
})
$completionAst = @($scriptAst.EndBlock.Statements | Where-Object {
    $_ -is [Management.Automation.Language.AssignmentStatementAst] -and $_.Left.Extent.Text -eq '$scenarioComplete'
})
if ($scenarioAst.Count -ne 1 -or $completionAst.Count -ne 1) { throw 'The production scenario and completion initialization could not be located.' }
$scenarioText = $scenarioAst[0].Extent.Text
# Insert an early return in the real orchestration block; its real finally still writes the report.
$bodyOffset = $scenarioAst[0].Body.Extent.StartOffset - $scenarioAst[0].Extent.StartOffset + 1
$scenarioText = $scenarioText.Insert($bodyOffset, '\nif ($fixtureScenario -eq "early-return") { return }\n')
$fixtureHeader = @'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$fixtureScenario = $env:UDT_VERIFICATION_SCENARIO
$utf8 = New-Object System.Text.UTF8Encoding($false)
$report = Join-Path $env:UDT_VERIFICATION_FIXTURE $fixtureScenario
$null = New-Item -ItemType Directory -Path $report -Force
$temporaryRoot = Join-Path $report 'isolated installation'
$installation = $temporaryRoot
$appdata = Join-Path $temporaryRoot 'data'
$primary = @{ Name = 'fixture-webview2'; Path = 'unused' }
$compatibility = @{ Name = 'fixture-electron'; Path = 'unused' }
$previousOverride = [Environment]::GetEnvironmentVariable('UDT_APPDATA_OVERRIDE', 'Process')
$results = New-Object 'System.Collections.Generic.List[object]'
$recoveryFailures = New-Object 'System.Collections.Generic.List[string]'
$failure = $null
$registryBackup = @()
$shortcutBackup = @()
function Invoke-Installer {
    if ($fixtureScenario -eq 'cancelled') {
        [IO.File]::WriteAllText((Join-Path $report 'started.txt'), 'blocked', $utf8)
        while ($true) { Start-Sleep -Milliseconds 25 }
    }
    if ($fixtureScenario -eq 'failure') { throw 'Isolated scenario failed.' }
}
function Assert-Installation { }
function Assert-PreservedData { }
function Test-RegistrationRollback { }
function Invoke-VerifiedUninstall { }
function Test-LegacyElectronMigration { }
function Stop-VerificationProcesses {
    [IO.File]::WriteAllText((Join-Path $report 'cleanup.txt'), 'completed', $utf8)
}
function Restore-UninstallBackup {
    if ($fixtureScenario -eq 'recovery-failure') { throw 'Isolated recovery failed.' }
}
function Restore-ShortcutBackup { }
'@
$runner = [PowerShell]::Create()
try {
    $null = $runner.AddScript($fixtureHeader + [Environment]::NewLine + $completionAst[0].Extent.Text + [Environment]::NewLine + $scenarioText)
    $execution = $runner.BeginInvoke()
    if ($env:UDT_VERIFICATION_SCENARIO -eq 'cancelled') {
        $started = Join-Path (Join-Path $env:UDT_VERIFICATION_FIXTURE 'cancelled') 'started.txt'
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not [IO.File]::Exists($started) -and -not $execution.IsCompleted -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 25
        }
        if (-not [IO.File]::Exists($started)) { throw 'The isolated scenario did not enter its cancellation point.' }
        $runner.Stop()
    }
    try { $null = $runner.EndInvoke($execution) }
    catch [Management.Automation.PipelineStoppedException] {
        if ($env:UDT_VERIFICATION_SCENARIO -ne 'cancelled') { throw }
    }
    if ($runner.Streams.Error.Count -gt 0) { throw ($runner.Streams.Error | Out-String) }
}
finally { $runner.Dispose() }
Write-Output 'Production scenario completion fixture passed.'
`
  for (const scenario of ['completed', 'early-return', 'cancelled', 'failure', 'recovery-failure']) {
    const result = await executePowerShell(command, {
      UDT_VERIFICATION_SCRIPT: script,
      UDT_VERIFICATION_FIXTURE: work,
      UDT_VERIFICATION_SCENARIO: scenario
    })
    assert.equal(result.code, 0, `${scenario}: ${result.diagnostic}`)
    const report = JSON.parse(await readFile(join(work, scenario, 'verification.json'), 'utf8'))
    assert.equal(report.ScenarioComplete, scenario === 'completed' || scenario === 'recovery-failure', scenario)
    assert.equal(report.Complete, scenario === 'completed', scenario)
    assert.equal(await readFile(join(work, scenario, 'cleanup.txt'), 'utf8'), 'completed', scenario)
    assert.equal(report.RecoveryFailures.length, scenario === 'recovery-failure' ? 1 : 0, scenario)
    assert.equal(report.Steps.some(step => step.Step === 'failure'), scenario === 'failure', scenario)
  }
})

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

test('legacy-layout fixture changes only owned isolated metadata and rejects unsafe destinations before writing', {
  skip: process.platform !== 'win32'
}, async context => {
  const work = await mkdtemp(join(tmpdir(), 'udt-verification-legacy-layout-'))
  context.after(() => rm(work, { recursive: true, force: true }))
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
$utf8 = New-Object System.Text.UTF8Encoding($false)
$root = Get-CanonicalPath (Join-Path $env:UDT_VERIFICATION_FIXTURE 'isolated-root')
function New-LegacyLayoutFixture([string]$Destination, [string[]]$OwnedMetadata) {
    $resources = Join-Path $Destination 'resources'
    [IO.Directory]::CreateDirectory($resources) | Out-Null
    [IO.File]::WriteAllText((Join-Path $resources 'install-channel'), 'electron-compatibility', $utf8)
    [IO.File]::WriteAllText((Join-Path $resources 'install-files.json'), ($OwnedMetadata | ConvertTo-Json), $utf8)
    [IO.File]::WriteAllText((Join-Path $resources 'install-files.txt'), 'owned uninstall metadata', $utf8)
    [IO.File]::WriteAllText((Join-Path $resources 'app.asar'), 'retained Chromium renderer', $utf8)
    [IO.File]::WriteAllText((Join-Path $Destination 'user-file.txt'), 'retained user file', $utf8)
}
function Assert-FixtureUnchanged([string]$Destination, [string]$ManifestBefore) {
    Assert-Condition ([IO.File]::ReadAllText((Join-Path $Destination 'resources/install-channel')) -eq 'electron-compatibility') 'An unsafe fixture changed its channel.'
    Assert-Condition ([IO.File]::ReadAllText((Join-Path $Destination 'resources/install-files.json')) -eq $ManifestBefore) 'An unsafe fixture changed its ownership manifest.'
    Assert-Condition ([IO.File]::ReadAllText((Join-Path $Destination 'resources/install-files.txt')) -eq 'owned uninstall metadata') 'An unsafe fixture changed its uninstall manifest.'
    Assert-Condition ([IO.File]::ReadAllText((Join-Path $Destination 'user-file.txt')) -eq 'retained user file') 'An unsafe fixture changed a user file.'
}
$metadata = @('resources/install-channel', 'resources/install-files.json', 'resources/install-files.txt')
$manifests = @(
    @{ Name = 'slash'; Owned = $metadata },
    @{ Name = 'windows'; Owned = @('resources\\install-channel', 'resources\\install-files.json', 'resources\\install-files.txt') },
    @{ Name = 'packaged'; Owned = @('resources\\install-channel', 'resources/install-files.json', 'resources/install-files.txt') }
)
foreach ($fixture in $manifests) {
    $valid = Join-Path $root ($fixture.Name + ' installation with spaces')
    New-LegacyLayoutFixture $valid $fixture.Owned
    ConvertTo-LegacyElectronFixture $valid $root
    Assert-Condition ([IO.File]::ReadAllText((Join-Path $valid 'resources/install-channel')) -eq 'full') 'The isolated legacy channel was not written.'
    Assert-Condition (-not [IO.File]::Exists((Join-Path $valid 'resources/install-files.json'))) 'The owned JSON fixture manifest remained.'
    Assert-Condition (-not [IO.File]::Exists((Join-Path $valid 'resources/install-files.txt'))) 'The owned uninstall fixture manifest remained.'
    Assert-Condition ([IO.File]::ReadAllText((Join-Path $valid 'resources/app.asar')) -eq 'retained Chromium renderer') 'The legacy fixture changed the renderer.'
    Assert-Condition ([IO.File]::ReadAllText((Join-Path $valid 'user-file.txt')) -eq 'retained user file') 'The legacy fixture changed a user file.'
}
$unowned = Join-Path $root 'unowned-metadata'
New-LegacyLayoutFixture $unowned @('resources/install-channel', 'resources/install-files.json')
$before = [IO.File]::ReadAllText((Join-Path $unowned 'resources/install-files.json'))
$rejected = $false
try { ConvertTo-LegacyElectronFixture $unowned $root } catch { $rejected = $true }
Assert-Condition $rejected 'Unowned fixture metadata was accepted.'
Assert-FixtureUnchanged $unowned $before
$outside = $root + '-outside'
New-LegacyLayoutFixture $outside $metadata
$before = [IO.File]::ReadAllText((Join-Path $outside 'resources/install-files.json'))
$rejected = $false
try { ConvertTo-LegacyElectronFixture $outside $root } catch { $rejected = $true }
Assert-Condition $rejected 'A sibling with the same isolation prefix was accepted.'
Assert-FixtureUnchanged $outside $before
New-LegacyLayoutFixture $root $metadata
$before = [IO.File]::ReadAllText((Join-Path $root 'resources/install-files.json'))
$rejected = $false
try { ConvertTo-LegacyElectronFixture $root $root } catch { $rejected = $true }
Assert-Condition $rejected 'The isolation root itself was accepted as an installation.'
Assert-FixtureUnchanged $root $before
Write-Output 'Legacy fixture ownership and isolation passed.'
`
  const result = await executePowerShell(command, { UDT_VERIFICATION_SCRIPT: script, UDT_VERIFICATION_FIXTURE: work })
  assert.equal(result.code, 0, result.diagnostic)
  assert.match(result.diagnostic, /Legacy fixture ownership and isolation passed/)
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
