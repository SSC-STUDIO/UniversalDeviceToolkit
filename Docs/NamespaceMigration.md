# Namespace and Assembly Migration

This document records the **completed Phase 3 hard cutover** from the Lenovo Legion Toolkit (LLT) binary/namespace ABI to **Universal Device Toolkit (UDT)**, plus the **remaining legacy compatibility surfaces** that intentionally still contain LLT / Lenovo tokens.

> [!NOTE]
> The plugin surface that was part of this migration (`UniversalDeviceToolkit.Lib.Plugins`, plugin assembly prefixes, SDK dual-stage naming) was retired in 6.1 together with the plugin system. It appears below only in the historical phase notes.

Related: CHANGELOG brand/ABI cutover notes.

---

## Status summary

| Phase | Scope | Status |
| --- | --- | --- |
| **0** | User-facing brand (product name, Electron process, installer, AppData path migration) | **Done** |
| **1** | Conventions for new non-ABI host code → `UniversalDeviceToolkit.*` | **Done** |
| **2** | Non-breaking dual surfaces (IPC pipes, `BrandCompatibility`, automation env aliases) | **Done** |
| **3** | Hard cutover: Lib `AssemblyName` + C# namespaces + Windows CLI exe name | **Done** |

Phase 3 is **complete in this repository**. New host code uses `UniversalDeviceToolkit.*` identities. Do **not** reintroduce `LenovoLegionToolkit.Lib*` as the primary assembly or namespace contract.

---

## Current state (from `.csproj`)

Values below are explicit `RootNamespace` / `AssemblyName` when set. If `AssemblyName` is omitted, the MSBuild default is the project file name. The full project tree is in [DEPLOYMENT.md](./DEPLOYMENT.md) ("Solution Structure").

| Project folder | RootNamespace | AssemblyName (if set) | Notes |
| --- | --- | --- | --- |
| `UniversalDeviceToolkit.Electron` | `UniversalDeviceToolkit.Electron.Launcher` (VS launcher stub only) | Electron UI shell (Node) | Shipping UI |
| `UniversalDeviceToolkit.Host` | `UniversalDeviceToolkit.Host` | `UniversalDeviceToolkit.Host` | Headless JSON-RPC backend |
| `UniversalDeviceToolkit.Lib` | `UniversalDeviceToolkit.Lib` | `UniversalDeviceToolkit.Lib` | Core library and host type identity |
| `UniversalDeviceToolkit.Lib.Abstractions` / `.Lib.Shared` | `UniversalDeviceToolkit.Abstractions` / `UniversalDeviceToolkit.Shared` | *(project default)* | Portable `net10.0` |
| `UniversalDeviceToolkit.Lib.Automation` / `.Lib.Macro` | matching folder name | *(project default)* | Host-only feature libraries |
| `UniversalDeviceToolkit.Platform.Windows` / `.Windows.Core` / `.Linux` / `.MacOS` | matching folder name | *(project default)* | Platform adapters |
| `UniversalDeviceToolkit.CLI` | `UniversalDeviceToolkit.CLI` | **`udt`** | Windows CLI → `udt.exe` (`udt-cli.exe` one-train alias; was `llt`) |
| `UniversalDeviceToolkit.CLI.Lib` | `UniversalDeviceToolkit.CLI.Lib` | *(project default)* | Shared CLI IPC models |
| `UniversalDeviceToolkit.NetworkProxy` | `UniversalDeviceToolkit.NetworkProxy` | `UniversalDeviceToolkit.NetworkProxy` | Fully UDT-named; pipe base name `udt-network-proxy` |
| `UniversalDeviceToolkit.CrossPlatform` | `UniversalDeviceToolkit.CrossPlatform` | **`udt`** | Cross-platform diagnostics CLI (`udt.dll` + launchers) |
| `UniversalDeviceToolkit.Tests` / `.Tests.Contracts` / `.Tests.Stateful` / `.Tests.Infrastructure` | `UniversalDeviceToolkit.Tests` (shared root) | *(project default)* | Host test ladder |
| `UniversalDeviceToolkit.Fast.Tests` / `.CrossPlatform.Tests` | matching folder name | *(project default)* | Tests |
| `UniversalDeviceToolkit.SpectrumTester` | `UniversalDeviceToolkit.SpectrumTester` | `SpectrumTester` | Dev tool, not shipped |
| `Tools/HardwareValidation` | `HardwareValidation` | *(project default)* | Dev tool, not shipped |

---

## What Phase 3 changed

| Surface | Before (LLT) | After (UDT primary) |
| --- | --- | --- |
| Lib `RootNamespace` / `AssemblyName` | `LenovoLegionToolkit.Lib` | `UniversalDeviceToolkit.Lib` |
| C# namespaces in Lib* | `LenovoLegionToolkit.Lib*` | `UniversalDeviceToolkit.Lib*` |
| Windows CLI `AssemblyName` | `llt` → `llt.exe` | `udt` → `udt.exe` (`udt-cli.exe` alias kept for one train) |
| Preferred CLI IPC pipe | (introduced in Phase 2) | `UniversalDeviceToolkit-IPC-0` |
| Legacy CLI IPC pipe | `LenovoLegionToolkit-IPC-0` | **Still accepted** (compat) |
| winget `PackageIdentifier` | `SSC-STUDIO.LenovoLegionToolkit` | `SSC-STUDIO.UniversalDeviceToolkit` (new identity in 6.x; no in-place upgrade from the legacy id) |
| Installer asset names | `LenovoLegionToolkit_v*_Setup.exe` | `UniversalDeviceToolkit_v*_Full_Setup.exe` / `_Online_Setup.exe` (no legacy alias is published) |

**Impact:** Tools compiled only against pre-cutover `LenovoLegionToolkit.Lib*` assembly/type identities **will not bind** to `UniversalDeviceToolkit.Lib.dll` without a rebuild. There is **no TypeForwardedTo shim**.

---

## Remaining legacy compatibility surfaces

Phase 3 did **not** erase every Lenovo / LLT token. The following remain on purpose. **Do not delete them in drive-by cleanups** without a separate cutover plan.

### 1. Dual IPC named pipes (host ↔ CLI)

| Constant | Value | Role |
| --- | --- | --- |
| `Constants.PREFERRED_PIPE_NAME` | `UniversalDeviceToolkit-IPC-0` | Preferred / client-first |
| `Constants.DEFAULT_PIPE_NAME` | `LenovoLegionToolkit-IPC-0` | Legacy; still listened for older CLI / tooling |

- **Server (`IpcServer`)**: accept loops on both names (`Constants.GetServerPipeNames`).
- **Client (`IpcClient`)**: try preferred UDT first, then fall back to legacy (`GetClientPipeNames`).
- Isolation-path hashing suffixes **both** names the same way.

### 2. `BrandCompatibility` legacy constants

`UniversalDeviceToolkit.Lib/Branding/BrandCompatibility.cs` (namespace `UniversalDeviceToolkit.Lib.Branding`):

| Constant | Value | Role |
| --- | --- | --- |
| `ProductDisplayName` / `ProductCompactName` | Universal Device Toolkit / UniversalDeviceToolkit | Current brand |
| `LegacyProductDisplayName` / `LegacyProductCompactName` | Lenovo Legion Toolkit / LenovoLegionToolkit | Migration messaging, AppData migration, docs |
| `PreferredAssemblyLib` | `UniversalDeviceToolkit.Lib` | **Current** primary assembly simple name |
| `LegacyAssemblyLib` | `LenovoLegionToolkit.Lib` | Messaging / detection only — **not** a runtime bind target |

Also see `AppIdentity` legacy display/compact/repository tokens used for AppData migration and historical links.

### 3. Automation environment variables (dual-write)

`AutomationEnvironment` dual-writes each automation value under **primary `UDT_*`** and **alias `LLT_*`**. Scripts may read either prefix.

| Legacy (`LLT_*`) | UDT alias | Context |
| --- | --- | --- |
| `LLT_IS_AC_ADAPTER_CONNECTED` | `UDT_IS_AC_ADAPTER_CONNECTED` | Automation trigger env |
| `LLT_IS_AC_ADAPTER_LOW_POWER` | `UDT_IS_AC_ADAPTER_LOW_POWER` | Automation trigger env |
| `LLT_IS_DISPLAY_ON` | `UDT_IS_DISPLAY_ON` | Automation trigger env |
| `LLT_IS_EXTERNAL_DISPLAY_CONNECTED` | `UDT_IS_EXTERNAL_DISPLAY_CONNECTED` | Automation trigger env |
| `LLT_IS_GAME_RUNNING` | `UDT_IS_GAME_RUNNING` | Automation trigger env |
| `LLT_IS_HDR_ON` | `UDT_IS_HDR_ON` | Automation trigger env |
| `LLT_IS_LID_OPEN` | `UDT_IS_LID_OPEN` | Automation trigger env |
| `LLT_STARTUP` | `UDT_STARTUP` | Automation trigger env |
| `LLT_RESUME` | `UDT_RESUME` | Automation trigger env |
| `LLT_POWER_MODE` | `UDT_POWER_MODE` | Automation trigger env |
| `LLT_POWER_MODE_NAME` | `UDT_POWER_MODE_NAME` | Automation trigger env |
| `LLT_PROCESSES_STARTED` | `UDT_PROCESSES_STARTED` | Automation trigger env |
| `LLT_PROCESSES` | `UDT_PROCESSES` | Automation trigger env |
| `LLT_DEVICE_CONNECTED` | `UDT_DEVICE_CONNECTED` | Automation trigger env |
| `LLT_DEVICE_INSTANCE_IDS` | `UDT_DEVICE_INSTANCE_IDS` | Automation trigger env |
| `LLT_IS_SUNSET` | `UDT_IS_SUNSET` | Automation trigger env |
| `LLT_IS_SUNRISE` | `UDT_IS_SUNRISE` | Automation trigger env |
| `LLT_TIME` | `UDT_TIME` | Automation trigger env |
| `LLT_DAYS` | `UDT_DAYS` | Automation trigger env |
| `LLT_PERIOD` | `UDT_PERIOD` | Automation trigger env |
| `LLT_IS_USER_ACTIVE` | `UDT_IS_USER_ACTIVE` | Automation trigger env |
| `LLT_WIFI_CONNECTED` | `UDT_WIFI_CONNECTED` | Automation trigger env |
| `LLT_WIFI_SSID` | `UDT_WIFI_SSID` | Automation trigger env |
| `LLT_SESSION_LOCKED` | `UDT_SESSION_LOCKED` | Automation trigger env |
| `LLT_LOG_PATH` | `UDT_LOG_PATH` | Log folder (`%LOCALAPPDATA%\UniversalDeviceToolkit\logs`). Host sets both at startup. |

Do **not** remove `LLT_*` keys without a dedicated script-migration notice.

### 4. Packaging / distribution identifiers

- **winget**: 6.x publishes under the new identity `SSC-STUDIO.UniversalDeviceToolkit` (see `Packaging/winget/README.md`). Pre-6.x `SSC-STUDIO.LenovoLegionToolkit` manifests were removed from the tree and live only in git history.
- **Scoop**: the planned manifest name is `universaldevicetoolkit` (see `Packaging/scoop/README.md`); the bucket is not published yet.
- Crowdin project URLs and some external links may still say `llt`.

### 5. Other intentional Lenovo references

Device-support catalogs, hardware series enums (e.g. Legion families), driver/package names, and user-facing copy about Lenovo hardware are **product features**, not brand/ABI leftovers.

---

## Historical phases

### Phase 0 — Done: user-facing brand UDT

Product name, Electron shell, Host process, assets, packaging Full/Online names, AppData path migration, env/docs brand copy.

### Phase 1 — Done: conventions for new code

New host modules use `UniversalDeviceToolkit.*` namespaces and project naming.

### Phase 2 — Done: dual surfaces without hard ABI rename

| Dual surface | Status | Notes |
| --- | --- | --- |
| Dual IPC named pipes | **Shipped** | Preferred `UniversalDeviceToolkit-IPC-0` + legacy `LenovoLegionToolkit-IPC-0` |
| `BrandCompatibility` constants | **Shipped** | Display names + preferred/legacy assembly simple names |
| Automation env vars `LLT_*` / `UDT_*` | **Shipped (aliases)** | Dual-write |

### Phase 3 — Done: hard cutover

- `AssemblyName` / `RootNamespace` for Lib (and, at the time, Lib.Plugins) → `UniversalDeviceToolkit.Lib*`
- Source namespaces `LenovoLegionToolkit.Lib*` → `UniversalDeviceToolkit.Lib*`
- Windows CLI `AssemblyName` `llt` → `udt-cli` → `udt` (current: `udt.exe`; `udt-cli.exe` one-train alias)
- Legacy pipes remain as **compat**, not primary ABI

The plugin host assembly, plugin prefixes (`UniversalDeviceToolkit.Plugins.*` / `LenovoLegionToolkit.Plugins.*`), and the SDK dual-stage naming that Phase 3 also covered were removed in 6.1 with the plugin system.

---

## CLI and IPC compatibility notes

| Concern | Current value | Guidance |
| --- | --- | --- |
| CLI `AssemblyName` | `udt` (`UniversalDeviceToolkit.CLI`) | Ship/docs use `udt.exe` (alias `udt-cli.exe`); CrossPlatform `udt` is framework-dependent diagnostics (`udt.dll` + `udt`/`udt.cmd` launchers) |
| Named pipe — **preferred UDT** | `UniversalDeviceToolkit-IPC-0` (`PREFERRED_PIPE_NAME`) | Client-preferred; host dual-listens |
| Named pipe — **legacy** | `LenovoLegionToolkit-IPC-0` (`DEFAULT_PIPE_NAME`) | Older CLI / tooling; keep until a deliberate pipe-only cutover |
| Automation env vars | `LLT_*` + `UDT_*` dual-write | Compatibility surface for user scripts |
| Brand / assembly dual constants | `BrandCompatibility` | Preferred = current UDT assembly; `Legacy*` = pre-Phase 3 LLT names |
| Network proxy pipe | `udt-network-proxy` (and session-suffixed variants) | UDT-native; separate from CLI IPC |

---

## Conventions for contributors and agents

1. **User-visible brand** → Universal Device Toolkit / UDT.
2. **Primary host ABI** → `UniversalDeviceToolkit.Lib` (assembly **and** namespaces).
3. **New code** → `UniversalDeviceToolkit.*` namespaces; do not introduce new `LenovoLegionToolkit.*` type namespaces.
4. **Legacy compat only** → keep dual pipes, `BrandCompatibility.Legacy*`, and `LLT_*` env dual-write until a **documented** removal pass.
5. **Do not claim "zero Lenovo tokens"** — residual compat strings are expected.

---

## Quick reference: primary vs legacy

```
Repo / solution:       UniversalDeviceToolkit.*
Electron UI:           UniversalDeviceToolkit.Electron
Host process:          UniversalDeviceToolkit.Host
Core Lib DLL:          UniversalDeviceToolkit.Lib.dll          (was LenovoLegionToolkit.Lib.dll)
CLI exe (Windows):     udt.exe   (was llt.exe / udt-cli.exe; udt-cli.exe alias kept one train)
CLI IPC pipes:         UniversalDeviceToolkit-IPC-0 (preferred)
                       LenovoLegionToolkit-IPC-0    (legacy)
Automation env:        LLT_* + UDT_* (dual-write)
Brand constants:       BrandCompatibility — Preferred* current; Legacy* LLT names
NetworkProxy:          UniversalDeviceToolkit.NetworkProxy (fully UDT)
winget:                SSC-STUDIO.UniversalDeviceToolkit (6.x identity)
```
