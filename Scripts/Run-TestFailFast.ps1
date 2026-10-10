#Requires -Version 5.1
<#
.SYNOPSIS
  Runs the same fast test layers as Ci-tests.yml (Windows).

.DESCRIPTION
  Order: Contracts (Guard/Security) -> Fast unit tests.
  The parallel unit and stateful suites are intentionally left to the full CI command.
##>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$tfm = 'net10.0-windows10.0.26100.0'
$contractsProject = 'Tests/Contracts/UniversalDeviceToolkit.Tests.Contracts.csproj'
$fastProject = 'Tests/Fast/UniversalDeviceToolkit.Fast.Tests.csproj'
$common = @('--framework', $tfm, '--configuration', $Configuration)
if ($NoBuild) { $common += '--no-build' }

function Invoke-TestLayer {
    param(
        [string] $Name,
        [string] $Project,
        [string[]] $Arguments,
        [string] $Trx
    )

    Write-Host "==> $Name" -ForegroundColor Cyan
    & dotnet test $Project @Arguments --logger "trx;LogFileName=$Trx"
    if ($LASTEXITCODE -ne 0) {
        throw "Test layer failed: $Name (exit $LASTEXITCODE)"
    }
}

if (-not $NoBuild) {
    Write-Host '==> Build solution (serial)' -ForegroundColor Cyan
    $env:MSBUILDDISABLENODEREUSE = '1'
    & dotnet build UniversalDeviceToolkit.sln --configuration $Configuration -m:1 --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }
    $common = @('--framework', $tfm, '--configuration', $Configuration, '--no-build')
}

Invoke-TestLayer -Name 'Contracts' -Project $contractsProject -Arguments $common -Trx 'UniversalDeviceToolkit.Tests.Contracts.trx'
Invoke-TestLayer -Name 'Fast unit tests' -Project $fastProject -Arguments $common -Trx 'UniversalDeviceToolkit.Fast.Tests.trx'

Write-Host 'Fast test layers passed.' -ForegroundColor Green
