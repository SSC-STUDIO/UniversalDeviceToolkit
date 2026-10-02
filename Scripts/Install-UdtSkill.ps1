#Requires -Version 5.1
<#
.SYNOPSIS
  Install the udt-hardware-cli skill to known AI-agent skill directories.

.DESCRIPTION
  Copies Docs/Skills/udt-hardware-cli to every detected agent skill location:
  Cursor, Claude Code, Codex, opencode. The repo copy remains the source of truth.
  Safe to re-run after git pull.

.PARAMETER All
  Install to all known locations even if the parent directory does not yet exist (creates it).

.PARAMETER DryRun
  Print what would be done without copying.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Scripts/Install-UdtSkill.ps1
  powershell -ExecutionPolicy Bypass -File Scripts/Install-UdtSkill.ps1 -DryRun
#>
[CmdletBinding()]
param(
  [switch]$All,
  [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repoRoot "Docs/Skills/udt-hardware-cli"

if (-not (Test-Path -LiteralPath $source)) {
  Write-Error "Source skill not found: $source"
  exit 1
}

$targets = @(
  @{ Name = "Cursor";      Path = Join-Path $env:USERPROFILE ".cursor/skills/udt-hardware-cli" },
  @{ Name = "Claude Code"; Path = Join-Path $env:USERPROFILE ".claude/skills/udt-hardware-cli" },
  @{ Name = "Codex";       Path = Join-Path $env:USERPROFILE ".codex/skills/udt-hardware-cli" },
  @{ Name = "opencode";    Path = Join-Path $env:USERPROFILE ".config/opencode/skills/udt-hardware-cli" }
)

$didAny = $false
foreach ($t in $targets) {
  $parent = Split-Path -Parent $t.Path
  $parentExists = Test-Path -LiteralPath $parent
  if (-not $parentExists -and -not $All) {
    Write-Host ("[skip] {0}: parent not found ({1}) - use -All to create" -f $t.Name, $parent) -ForegroundColor DarkGray
    continue
  }

  if ($DryRun) {
    Write-Host ("[dry-run] {0} -> {1}" -f $t.Name, $t.Path)
    $didAny = $true
    continue
  }

  try {
    New-Item -ItemType Directory -Force -Path $t.Path | Out-Null
    Copy-Item -Path (Join-Path $source "*") -Destination $t.Path -Recurse -Force
    Write-Host ("[ok] {0} -> {1}" -f $t.Name, $t.Path) -ForegroundColor Green
    $didAny = $true
  } catch {
    Write-Host ("[fail] {0}: {1}" -f $t.Name, $_.Exception.Message) -ForegroundColor Red
  }
}

if (-not $didAny) {
  Write-Host ""
  Write-Host "No skill directory was written." -ForegroundColor Yellow
  Write-Host "Create one of the parent directories first, or run with -All:"
  Write-Host "  powershell -ExecutionPolicy Bypass -File Scripts/Install-UdtSkill.ps1 -All"
  Write-Host ""
  Write-Host "Manual copy:"
  Write-Host "  Copy-Item -Path Docs/Skills/udt-hardware-cli/* -Destination <agent-skill-dir>/udt-hardware-cli -Recurse -Force"
  exit 0
}

Write-Host ""
Write-Host "Source: $source"
Write-Host "Verify with your agent, e.g.:"
Write-Host "  udt doctor --json   # udt-cli doctor --json still works via shim"
