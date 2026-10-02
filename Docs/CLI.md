# udt contract

`udt.exe` is the Windows IPC client for local hardware control. People and agents share the same surface. It does not start Host or Electron.

The CrossPlatform `udt` binary (asset `*_CLI_cross-platform.zip` containing `udt.dll` + launchers `udt`/`udt.cmd`) is diagnostics only. Do not use it for Legion/LOQ hardware.

## Preconditions

1. Universal Device Toolkit is running (tray is enough).
2. Settings → Integrations → CLI is on. Default is off. The toggle starts the named pipe immediately; do not edit `integrations.json`.
3. Same Windows user as the running app.

`udt doctor` (and `udt doctor --json`) inspects those facts **without IPC**.

Compatibility: `udt-cli.exe` / `udt-cli.dll` remain as a one-train shim (copy of `udt.exe` / `udt.dll`) so old scripts keep working.

## `--json`

Global flag. Success:

```json
{"ok":true,"command":"feature.get","name":"power-mode","value":"quiet"}
```

Failure (non-zero exit, still stdout JSON):

```json
{"ok":false,"code":"connect","message":"Failed to connect. ..."}
```

`--json` shortens the legacy named-pipe retry so agents do not wait tens of seconds.

## Commands agents may use

| Command | Notes |
| --- | --- |
| `doctor` | No IPC. Reads `%LOCALAPPDATA%\UniversalDeviceToolkit\integrations.json` and probes pipes. |
| `status` | Running app status. |
| `feature --list` / `feature get` / `feature set` | Kebab-case names. Always `--list` on this machine first. |
| `spectrum profile` / `spectrum brightness` | Spectrum RGB. |
| `rgb get` / `rgb set` | 4-zone RGB. |
| `quickAction --list` / `quickAction <name>` | User-defined names, not stable IDs. |

`network` and `shell` exist but are **not** part of the default agent allow-list.

Not on CLI: sensors, God Mode, fan curves, GPU overclock.

Kebab vs RPC camelCase: [Skills/udt-hardware-cli/reference.md](Skills/udt-hardware-cli/reference.md).

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success |
| -1 | Could not connect (`IpcConnectException`) |
| -2 | IPC error |
| -99 | Unexpected exception |
