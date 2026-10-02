---
name: udt-hardware-cli
description: Control Universal Device Toolkit hardware via udt (laptop power mode, Fn+Q, RGB, battery care, UDT CLI). Use when the user mentions UDT, udt, Fn+Q, Legion/LOQ power modes, Spectrum/RGB, or local hardware toggles. Not a general Windows optimizer.
---

# UDT hardware CLI

UDT is trusted local hardware control. Humans use the tray / Fn+Q. Agents use `udt`.

## Install

This skill lives in the repo at `Docs/Skills/udt-hardware-cli/`. Copy it to your agent's skill directory. Do not put it in this repo's `.opencode/` or `.cursor/` (those are for repo-local automation).

**One-line installer (PowerShell, Windows):**

```powershell
powershell -ExecutionPolicy Bypass -File Scripts/Install-UdtSkill.ps1
```

It copies the skill to every detected location and prints what it did. Re-run after `git pull` to update.

**Manual copy - pick your agent:**

| Agent | Target directory |
| --- | --- |
| Cursor | `%USERPROFILE%\.cursor\skills\udt-hardware-cli\` |
| Claude Code | `%USERPROFILE%\.claude\skills\udt-hardware-cli\` |
| Codex | `%USERPROFILE%\.codex\skills\udt-hardware-cli\` |
| opencode | `%USERPROFILE%\.config\opencode\skills\udt-hardware-cli\` |
| Generic / other | Copy this folder to the path your agent documents for skills |

Example (Cursor, PowerShell):

```powershell
$src = "Docs/Skills/udt-hardware-cli"
$dst = "$env:USERPROFILE\.cursor\skills\udt-hardware-cli"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item -Path "$src\*" -Destination $dst -Recurse -Force
```

## Procedure

1. Run `udt doctor --json`.
2. If `ready` is false, **stop**. Tell the user to open Settings → Integrations → CLI and keep UDT in the tray. Do **not** edit `integrations.json`.
3. Run `udt feature --list --json`. Never invent a feature name.
4. Get or set only listed features. Prefer `--json`.

Compatibility: `udt-cli` is an alias of `udt` (shim). Prefer `udt` in new scripts and docs.

## Allow

`doctor`, `status`, `feature`, `spectrum`, `rgb`, `quickAction`.

## Refuse

- Editing `integrations.json` or any settings file
- Installing plugins
- `network` start/stop (acceleration)
- `shell install` / uninstall
- Sensors, God Mode, fan curves, GPU overclock (not on CLI)
- Treating CrossPlatform `udt` diagnostics asset (`*_CLI_cross-platform.zip`) as hardware control
- Acting as a generic Windows cleaner/optimizer

Feature name table: [reference.md](reference.md). Human contract: [../../CLI.md](../../CLI.md).
