# Network Acceleration (built-in)

Independent UDT implementation inspired by Watt Toolkit *behavior* only.
**No GPL source** from Watt Toolkit / SteamTools is copied into this repository.

## Architecture

```
WebView2 / Electron (shared Network & acceleration page)
  └─ Shared .NET Host NetworkAccelerationHandlers (JSON-RPC)
        └─ UniversalDeviceToolkit.NetworkProxy.exe (isolated worker)
              ├─ Named pipe IPC (current-user ACL + random session token)
              └─ Loopback-only HTTP + CONNECT proxy (127.0.0.1 / ::1)
```

- **Default**: acceleration **OFF**. App launch never auto-starts proxy, Hosts edits, or certificates.
- **Worker location**: Host looks for `UniversalDeviceToolkit.NetworkProxy.exe` plus `.runtimeconfig.json` / `.deps.json` beside Host (`Folders.Program` / `AppContext.BaseDirectory`: the WebView2 installation root or Electron `resources/host`, with Debug copy-on-build), then in the sibling `Apps/NetworkProxy` `bin/` output. `npm run dev` / VS F5 does not need a full installer.
- **Crash isolation**: the proxy runs as a separate worker; failures must not tear down the GUI.
- **Owner exit**: the worker monitors its recorded Host PID and start time, closes its own listener and exits when that Host terminates. Snapshot recovery restores remaining system changes at the next normal recovery or maintenance operation.
- **IPC**: named pipe, random session token per run, ACL limited to the current user (+ Administrators).
- **Bind**: loopback only — never `0.0.0.0` / `::`.
- **Startup recovery**: restore only the previous snapshot's UDT-owned proxy / Hosts changes, without replaying acceleration. An orphaned worker can be stopped only when its recorded PID, start time and executable path match and its recorded owner has exited. Workers are never killed solely by process name.
- **Shutdown**: main app stops the worker and restores snapshot before exit.

## Modes

| Mode | Intent |
|---|---|
| `Off` | Default. No mutations. |
| `SystemProxy` | Point Windows system proxy / **PAC** at the local worker (user-started). **Requires ≥1 enabled domain** — never falls back to full-loopback system proxy when the domain list is empty. |
| `Hosts` | **Reserved / disabled in UI and Start refused** (safety): mapping domains to `127.0.0.1` without a local TLS origin breaks HTTPS. Not listed in the mode selector. Marked-block helpers (`# BEGIN/END UDT-NETWORK-ACCELERATION`) remain for a future redesign with a local origin. If an older config still has `Mode=Hosts`, the UI shows a disabled note, selects SystemProxy in the combo without silently rewriting config until Start/Save, and Start coerces to SystemProxy. |
| `DiagnosticsOnly` | Inspect / preview without changing system network state. |

### Safety gates (Start)

- **Default remains OFF** — never auto-starts on application launch.
- **SystemProxy**: `StartAsync` returns `false` (and does not mutate system proxy) when no enabled domains are present. Empty list does **not** apply `CreateLoopbackProxy`.
- **Hosts**: `StartAsync` returns `false` with a warning until a local TLS origin exists. UI omits Hosts from selectable modes; use SystemProxy (PAC) or DiagnosticsOnly.
- **DiagnosticsOnly**: still allowed without domains (no system mutations).

## Domain groups

Built-in audited groups (disabled by default):

- **Steam** — steampowered.com, steamcommunity.com, steamstatic.com, …
- **GitHub** — github.com, githubusercontent.com, ghcr.io, …
- **Custom** — user-defined list (empty by default)

### Selection bar (Watt Toolkit-style UX)

Bottom floating bar over the domain tiles (behavior inspired by Watt Toolkit; no GPL code):

| Action | Behavior |
|---|---|
| **Click tile** | Multi-select / deselect |
| **Double-click tile** | Toggle group enabled for PAC (system proxy) |
| **★ Favorite** | Pin/unpin selected groups (`IsFavorite`); favorites sort first |
| **▶ Start selected** | Enable selected groups, turn on acceleration (SystemProxy if needed), start worker |

No third-party accelerator SDKs, no remote script injection, no unreviewed online rules store.

## Recovery

- Snapshot file: `%LOCALAPPDATA%\UniversalDeviceToolkit\network_state_snapshot.json` (via `Folders.AppData`, or the effective `UDT_APPDATA_OVERRIDE` directory).
- Captures: system proxy fields, UDT hosts block, PAC path/contents metadata, and owner/worker PID, start time and executable path.
- The independent Windows Host command `UniversalDeviceToolkit.Host.exe --restore-network-state` restores only the saved network snapshot and prints its report. It runs before hardware or IoC initialization, starts no worker, does not reset external arguments, and exits with `0` on success or `1` on refusal/failure. Use this entry for command-line snapshot recovery; the older Electron `--reset-network-state` flag does not invoke Host maintenance.
- Both native uninstallers stop their selected shell and owned processes, then use the independent recovery entry before deleting the Host. Both installers also run it through the incoming package's Host before replacing an older installation. Recovery refusal or failure preserves the installation for retry, including when another same-user session owns the network lease.
- UI: **Force restore network state**.
- Start, Stop and Restore share a serialized lifecycle gate, so their worker and system-state changes cannot overlap.
- Network operations also acquire a lease shared by all UDT sessions for the same Windows user. Its path is `%LOCALAPPDATA%\UniversalDeviceToolkit\network-acceleration.lease`, independent of `UDT_APPDATA_OVERRIDE`, because the Windows proxy is shared across those sessions. An active session retains the lease while its worker, applied system changes or unresolved snapshot remain; another session or uninstaller refuses the operation until the lease is released.
- With a snapshot present, a live owner from another session, an owner whose identity cannot be verified, or an unidentified/foreign active worker blocks saving or recovery and leaves the snapshot intact. Even the current owner must check for foreign workers: only its recorded worker with the same PID, start time and executable path is excluded. A legacy snapshot without an owner record is accepted only when no NetworkProxy worker is active.
- Recovery stops only a verified recorded orphan before restoring its snapshot. An inaccessible process or an executable-path mismatch is a refusal, not permission to stop another worker.
- A missing snapshot is an **idempotent success** only when no system mutation has been applied. If the current session already changed system state, a missing snapshot is reported as a recovery failure and the applied-state flag is retained for retry.
- An existing empty, malformed or JSON `null` snapshot is a failure, never a missing-snapshot success. Saving and recovery refuse it without overwriting or consuming the original file; applied-state and pending-recovery tracking remain available for retry. Recovery also rejects an unsupported snapshot schema.
- Partial failures are reported item-by-item; other steps still run.

## HTTPS / local CA (planned / optional)

- Selective HTTPS decryption requires explicit user consent.
- Local CA is generated in **CurrentUser** store only; private key protected with DPAPI.
- Not written to computer-level root store by default.
- Current worker ships CONNECT tunneling without MITM by default.

## Status

| Piece | Status |
|---|---|
| `Apps/NetworkProxy` worker + IPC | Done (HTTP + CONNECT, loopback) |
| Lib interfaces + config + hosts/PAC helpers | Done |
| Snapshot restore + startup heal + shutdown stop | Done |
| System proxy / PAC apply on user Start (domains required; no full-loopback fallback) | Done |
| Hosts mode Start | **Refused** until local TLS origin; helpers kept |
| Hosts mode in selector | **Omitted** (reserved); legacy config shows disabled note |
| Electron page (enable, start/stop, restore, diagnostics) | Done |
| Mode selector (SystemProxy / DiagnosticsOnly) | Done |
| Domain group toggles (Steam / GitHub / Custom) | Done |
| Multi-select bar (favorite pin + start selected) | Done |
| Compact CardControl layout (matches Settings) | Done |
| Built-in Steam/GitHub domain groups (off by default) | Done |
| YARP MITM / DPAPI CA UI | Optional follow-up |
| Continuous background sampling when page hidden | **Not used** (by design) |

## Former plugin capabilities

The plugin system was retired in 6.1. Network Acceleration is a built-in Host
feature under System Optimization. Cursor and pointer controls live on the
Mouse page. See [CHANGELOG.md](../CHANGELOG.md).
