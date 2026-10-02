# Universal Device Toolkit Architecture

## Overview

Universal Device Toolkit (UDT, formerly Lenovo Legion Toolkit) is a lightweight Windows-first desktop application with an Electron UI and a headless .NET backend: full Lenovo hardware control on supported Windows machines, official Host features plus in-tree brand providers, and safe basic-mode workflows on other Windows PCs. macOS and Linux have experimental portable Host, Electron-shell, and diagnostics-CLI surfaces; they are not a shipped product. The application follows a modular architecture pattern with clear separation of concerns. New hardware support lands in the official Host and brand providers (see [DEVICE_PROVIDERS.md](./DEVICE_PROVIDERS.md)); the plugin system was retired in 6.1 and is not an extension path.

## Quick Start

### For Users

1. **Download** the latest release from [GitHub Releases](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases)
2. **Install** the application by running the installer
3. **Launch** UDT and configure your preferred settings
4. **Use** supported hardware controls or basic-mode Host and system tools

### For Developers

1. **Prerequisites**: Install .NET 10 SDK, Visual Studio 2022 and Node.js 20+
2. **Clone** the repository: `git clone https://github.com/SSC-STUDIO/UniversalDeviceToolkit.git`
3. **Build** the solution: `dotnet build UniversalDeviceToolkit.sln`
4. **Run** tests: see [TEST_DIAGNOSTICS.md](./TEST_DIAGNOSTICS.md) (`Tests.Contracts` → `Fast.Tests` → `Tests` → `Tests.Stateful`)
5. **Start the UI (Electron)**: `cd UniversalDeviceToolkit.Electron && npm ci && npm run dev`
   In Visual Studio, set the `UniversalDeviceToolkit.Electron` launcher project as
   the startup project and press F5 (its "Electron (npm run dev)" launch profile
   runs `npm run dev`). Do **not** set `UniversalDeviceToolkit.Host` as the startup
   project — it is a headless backend spawned automatically by Electron.
6. **Start** developing! See [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, test, and culture-naming rules.

## System Architecture

The client UI is an **Electron app** (Node.js + electron-vite + React) in
`UniversalDeviceToolkit.Electron/`. It talks over JSON-RPC (newline-delimited,
stdio) to the **UniversalDeviceToolkit.Host** process — a headless .NET backend
that hosts all business logic (hardware control, sensors, settings, brand providers).
Electron's main process only owns the UI shell (window, tray, OSD, dialogs);
it forwards every other `bridge:invoke` call to the Host. The Host is spawned
automatically by Electron (dev: `bin/x64/Debug/.../Host.exe`; packaged:
`resources/host/`); it never shows a window.

```
+-----------------------------------------------------------------------+
|                        Universal Device Toolkit                          |
+-----------------------------------------------------------------------+
| Presentation Layer (Electron renderer: React + Ant Design + ECharts)    |
| +--------------------------------------------------------------------+ |
| | UniversalDeviceToolkit.Electron/src/renderer                    |  |  |
| | +- Pages, Components, Stores (Zustand), i18n                    |  |  |
| | +- api/*: typed bridge.invoke wrappers                          |  |  |
| +--------------------------------------------------------------------+ |
| Electron main process (window, tray, OSD, single-instance, dialogs)    |
|   └─ bridge:invoke ──► Host (JSON-RPC over stdio)                      |
| +--------------------------------------------------------------------+ |
| Host Layer (UniversalDeviceToolkit.Host: Rpc/Handlers/*)               |
| | UniversalDeviceToolkit.Host       (headless JSON-RPC server)      |  |
| | UniversalDeviceToolkit.CLI       | Automation     | Macro         |  |
| | UniversalDeviceToolkit.Lib.Automation | Toolkit.Lib.Macro         |  |
| +--------------------------------------------------------------------+ |
| Core Library Layer                                                      |
| +--------------------------------------------------------------------+ |
| | UniversalDeviceToolkit.Lib (assembly: UniversalDeviceToolkit.Lib)  |  |
| | +- Hardware Controllers (34 modules)                          |  |  |
| | +- Services (Settings, Messaging, IoC)                        |  |  |
| | +- Game Detection System                                      |  |  |
| | +- Brand providers / device-support catalog                   |  |  |
| | +- Native Interop (WMI, ACPI, USB/HID)                      |  |  |
| +--------------------------------------------------------------------+ |
+-----------------------------------------------------------------------+
| Infrastructure                                                          |
| +- Autofac (Dependency Injection)                                    |
| +- HID Sharp (Hardware Interface)                                    |
| +- LibreHardwareMonitorLib (System Monitoring)                        |
| +- Native Windows APIs (WMI, Power, etc.)                           |
+-----------------------------------------------------------------------+
```

## Performance & Optimization Principles

UDT breaks the common misconception that Electron desktop apps are inherently bloated or resource-heavy. By combining Electron with a headless .NET 10 core, UDT achieves top-tier responsiveness, sub-400ms page transitions, and negligible background resource consumption.

### 1. Zero-Memory Tray Sleeping
- Unlike standard desktop applications that keep invisible Chromium renderer processes and full DOM trees resident in memory when minimized, UDT **destroys the main window and renderer DOM tree completely** upon minimizing or closing to the tray.
- The tray popup is rendered via a compact HTML window that auto-unloads on idle (`scheduleIdleDestroy`).
- While resident in the tray, background memory footprint is pruned to the absolute minimum, ensuring zero interference with gaming or heavy workloads.

### 2. Hot-Path Zero Allocation & Incremental Telemetry
- High-frequency sensor polling loops (1 Hz) stream incremental diffs.
- Static chart options, themes, and metric mapping tables are memoized via `useMemo` and module-level constants.
- Polling timers and background monitors automatically suspend when the UI window is hidden or when navigating away from sensor dashboards.

### 3. Tree-Shaking and Sub-second Ready Latency
- All page modules are lazily loaded via dynamic imports.
- The 7,000+ Fluent UI icon catalog is trimmed down to individual used glyphs via graph-based build optimization in `electron-vite`.
- Median UI ready latency across all views stays within **≤ 400ms**; see [UI_PERFORMANCE.md](./UI_PERFORMANCE.md) for how to profile it.

### 4. Zero Persistent Services & Zero Telemetry
- UDT installs no persistent Windows services or background daemons.
- No analytics or user telemetry data is tracked or uploaded.

## Platform Notes

The supported product is Windows. The Electron UI shell contains
platform-specific chrome for macOS and Linux, but those paths are
**experimental**: `Release.yml` publishes only Windows NSIS installers with a
win-x64 Host. There is no official macOS/Linux Electron release.

Implementation map (all under `UniversalDeviceToolkit.Electron/src/main/`).
macOS/Linux rows describe existing shell code, not a shipped product:

| Surface | Windows | macOS | Linux | Implementation |
|---|---|---|---|---|
| Title bar | Frameless custom title bar with right-aligned window buttons (Mica background material) | Native title bar with traffic lights (hiddenInset) + vibrancy | Frameless custom title bar with right-aligned window buttons | `index.ts` `createWindow()` (`frame: false` / `titleBarStyle: 'hiddenInset'` branch); renderer `TitleBar.tsx` hides its buttons on `darwin` |
| Menu bar | Auto-hidden (frameless) | Native system menu bar (App/File/Edit/View/Window/Help roles) | Auto-hidden (frameless) | `menu.ts` `installApplicationMenu()` — macOS only; `hasNativeMenuBar()` |
| Tray | Tray icon + custom flyout (navigation, quick actions, open/close) | Tray icon + custom flyout | Tray icon + custom flyout | `tray.ts` `initTray()` — all platforms |
| OSD overlay | Transparent always-on-top window fed by Host sensor data | Same window; no meaningful sensor data in basic mode | Same window; no meaningful sensor data in basic mode | `osd-window.ts` |
| System power actions (restart/shutdown/sleep) | Via `shutdown.exe` | Unavailable (spawn fails) | Unavailable (spawn fails) | `system-power.ts` |
| Windows power plans | Via `powercfg` | Unavailable | Unavailable | `power-plans.ts` |
| App lifecycle | Tray-only background: destroy main/status/tray-popup (OSD only if hidden); do not quit while the tray is alive. Restore recreates the main window. | Same destroy/recreate; Dock `activate` restores | Same as Windows when minimize-to-tray is on; otherwise quit on last window | `index.ts` `enterBackground()` / `restoreMainWindow()` / `window-all-closed` |
| Start on login | Host scheduled task (`app.setAutorun`) launching the Electron shell via `UDT_SHELL_PATH` | Electron login item (`app.setLoginItemSettings`) | XDG autostart `.desktop` | Settings page picks the channel by `bridge.platform` |

The shipping Host backend (`.NET`) is Windows-first: it targets the Windows TFM
`net10.0-windows10.0.26100.0` and drives hardware through WMI/registry/vendor
drivers. Official releases embed the self-contained `win-x64` publish output.
A portable `net10.0` Host (`UDTWindows=false` / `UDT_PLATFORM=linux|macos`)
exists for experimental macOS/Linux work and registers Windows-only RPC names
as `-32099`. Official Host and brand providers target Windows TFMs. Per-platform Host publish
details are in [DEPLOYMENT.md](DEPLOYMENT.md).

### Shell exceptions (stay in Electron main)

These `bridge:invoke` methods are answered by the Electron main process, not
Host JSON-RPC. They are OS-shell or installer concerns and are **not** migrated
to Host in this phase:

| Method | Owner |
|---|---|
| `powerPlans.getList` / `powerPlans.setActive` | `power-plans.ts` (`powercfg`, Windows only) |
| `power.restart` / `power.shutdown` / `power.sleep` | `system-power.ts` |
| `update.getRelease` / `update.download` / `update.launchInstaller` | `update-downloader.ts` (GitHub release + installer launch) |
| `device.info` | Main-process device snapshot (falls back to `system.info` in the UI) |
| `dialog:*`, `log.open-folder`, `status-window.show` | Native dialogs, folders, tray status popup |

Non-Windows Host builds register the Windows-only RPC names as `-32099`
(`Not supported on this platform.`) so the renderer never waits on unknown-method
errors. The plugin system was retired in 6.1; those marketplace method names
are no longer part of the Host RPC surface.

Host JSON-RPC errors keep their numeric code in the message as `[UDT:<code>]`
so the UI can map `-1006` (elevation), `-1010` (missing NetworkProxy), `-1011`
(Hosts mode refused), `-1012` (start refused), and `-32099`.

## Core Components

### 1. UniversalDeviceToolkit.Electron (Presentation Layer)

The Electron client implementing the React UI and the window shell:

- **`src/renderer/`**: Pages (Dashboard, Actions, Keyboard, Tools, Settings), Components, Zustand stores, `api/*` typed bridge wrappers, `i18n/locales/*` (TS modules). Actions contains the automation and macro workspaces; Tools contains cleanup, network, driver, system, and pointer tasks. Live sensor panels live in `components/dashboard/`; feature cards and GPU extras live in `components/dashboard-parity/` (WPF dashboard-control parity, not a second app).
- **`src/main/`**: Main process shell — window creation (`index.ts`), tray (`tray.ts`), OSD (`osd-window.ts`), macOS menu (`menu.ts`), single-instance, dialogs, host client (`host-client.ts`), path/URL and power-action guards
- **`src/preload/`**: Context-isolated bridge (`index.ts`)

### 2. UniversalDeviceToolkit.Lib (Core Library; assembly `UniversalDeviceToolkit.Lib`)

The heart of the application containing:

#### Controllers (34 hardware modules)
- `PowerModeController`: Power mode management
- `FanController`: Fan speed control and curves
- `RGBController`: Keyboard and lighting control
- `GPUController`: GPU mode switching (dGPU, Hybrid, iGPU)
- `MacroController`: Macro key handling
- `CameraController`: Camera power management
- And 29 more specialized controllers

#### Services
- `SettingsService`: Persistent configuration storage
- `UpdateService`: Application updates
- `PackageDownloader`: Driver/firmware updates
- `GameDetectionService`: Active game detection

#### Features
- `IAutomationFeature`: Automated actions based on triggers
- `ITriggerFeature`: Event-driven automation

#### Native Interop
- `Native.cs`: P/Invoke declarations for Windows APIs
- WMI integration for hardware queries
- ACPI communication for firmware access

### 3. UniversalDeviceToolkit.Lib.Automation

Automation system implementing a rule-based engine:

- **Triggers**: Application launch, game detection, AC plugged/unplugged
- **Conditions**: Time-based, power state, user presence
- **Actions**: Power mode change, fan curve, RGB profile, macro activation

### 4. UniversalDeviceToolkit.Lib.Macro

Macro recording and playback system:

- Key sequence recording
- Macro storage and management
- Integration with hardware macro keys

### 5. UniversalDeviceToolkit.CLI

Command-line interface for headless operation:

- Power mode queries and changes
- Status monitoring
- Automation rule management

## Renderer Security Boundary

The plugin system was retired in 6.1. The shipping UI is a sandboxed Chromium
renderer with `contextIsolation` and no Node.js integration. Privileged work
reaches the main process only through the preload bridge:

- IPC handlers accept requests from the current main window's main frame only
- `window.open` and unexpected top-level navigation are denied
- External URLs must be HTTP(S); renderer-supplied paths cannot open executables or scripts
- Power actions are rate limited in the main process

Former plugin capabilities now live as built-in Host features (cursor and
pointer controls on the Mouse page; network acceleration under System
Optimization). Legacy `%LOCALAPPDATA%\UniversalDeviceToolkit\plugins` data is
not loaded.

## Data Flow

### Power Mode Change Flow

```
User Action (UI)
      -> Renderer api/ bridge.invoke('feature.setPowerMode', ...)
      -> Electron main (bridge:invoke) -> Host JSON-RPC
      -> PowerModeController.SetModeAsync() (Host)
      -> WMI Call (\\ROOT\WMI\Lenovo_Path)
      -> ACPI Communication
      -> Hardware Response
      -> Windows Power Plan Sync
      -> State Update Broadcast (bridge:event)
      -> UI Refresh
```

### Game Detection Flow

```
GameDetectionService (Background Monitor)
      -> Window Title / Process Matching
      -> Host event broadcast
      -> Automation Rules Evaluation
      -> Automatic Actions Execution
```

### Bridge RPC error codes

Error codes are defined once in `UniversalDeviceToolkit.Host/Rpc/BridgeErrorCodes.cs`
and mapped to localized messages by the renderer (`src/renderer/src/api/bridge.ts`).

- `-32601` unknown method, `-32602` invalid params, `-32603` internal error,
  `-32800` request cancelled (JSON-RPC protocol range, produced by
  `BridgeRpcServer` and handler argument validation).
- `-32099` platform not supported: whole Windows-only domain on a portable
  host. The method list lives in `Rpc/RpcMethodNames.cs` - the single source
  shared by the Windows registration check (`Program.VerifyRpcSurface`) and
  the portable stubs, so the two surfaces cannot drift.
- `-32001` God Mode not supported by the device generation.
- `-1001` feature not supported, `-1002` AC power required, `-1004` undefined
  state, `-1005` macro hooks failed, `-1006` elevation required,
  `-1010` NetworkProxy.exe missing, `-1011` hosts mode refused,
  `-1012` network start refused (application-level conditions).

## Technology Stack

| Layer | Technology/Framework |
|-------|---------------------|
| UI Framework | Electron 43 + React 19 (electron-vite, Ant Design, ECharts) |
| UI Logic | React components + Zustand stores; `api/*` typed bridge wrappers |
| Backend | .NET 10 headless Host (`UniversalDeviceToolkit.Host`) over JSON-RPC (stdio) |
| Architecture | Clean Architecture (UI shell ↔ Host ↔ Core Lib) |
| DI Container | Autofac (Host) |
| Hardware Access | WMI, ACPI, Windows native APIs (Windows only) |
| Monitoring | Built-in sensors and controller queries |
| Settings | JSON file storage |
| Updates | GitHub Releases API |
| Localization | Crowdin + Electron i18n TS modules + `.resx` satellites |

## Namespace and assembly naming

User-facing product names use **Universal Device Toolkit**. The plugin host
assembly (`UniversalDeviceToolkit.Lib.Plugins`) was removed in 6.1.

| Surface | Primary identity |
| --- | --- |
| Product / Electron process | Universal Device Toolkit |
| Core Lib assembly / namespaces | `UniversalDeviceToolkit.Lib` |
| Windows IPC CLI executable | `udt.exe` (`AssemblyName` = `udt`; `udt-cli.exe` one-train alias) |
| Cross-platform diagnostics CLI | `udt` (`UniversalDeviceToolkit.CrossPlatform`, framework-dependent `udt.dll` + `udt`/`udt.cmd`) |

Phase 3 hard cutover from `LenovoLegionToolkit.Lib*` is **complete**. Remaining LLT tokens (legacy IPC pipe `LenovoLegionToolkit-IPC-0`, `BrandCompatibility.Legacy*`, dual-written `LLT_*` env keys, packaging IDs) are deliberate compatibility surfaces — not the primary ABI. Plugin load prefixes were removed with the plugin system in 6.1.

See **[NamespaceMigration.md](./NamespaceMigration.md)** for the RootNamespace/AssemblyName inventory, completed Phases 0–3, and remaining legacy compat notes.

## Key Design Decisions

1. **No Background Service**: Application runs only when user is logged in
2. **No Telemetry**: Complete user privacy
3. **Lightweight**: Minimal resource footprint
4. **Official Host + brand providers**: New device workflows land in Host RPC and in-tree brand providers, not third-party modules (see [DEVICE_PROVIDERS.md](./DEVICE_PROVIDERS.md))
5. **Catalog-backed Device Support**: Data-driven hardware/basic-mode profiles across Lenovo families and common PC vendors
6. **Primary ABI is UDT-named**: Core Lib assemblies are `UniversalDeviceToolkit.Lib*`; the Host still accepts selected dual pipes during transition (see [NamespaceMigration.md](./NamespaceMigration.md))

## Platform Compatibility

- **Windows**: 10 (1809+), 11 (x64 only) — supported product (full hardware control + basic mode)
- **macOS / Linux**: experimental only (portable Host, Electron shell, CrossPlatform CLI). No official Electron release. Hardware control is Windows-only. Official Host and brand providers are Windows TFMs.
- **Hardware (code-driven detection)**:
  - Hardware-control profiles: Legion 5/Slim 5/Pro 5, Legion 7/Pro 7/9, Legion Go, LOQ, IdeaPad Gaming, ThinkBook, YOGA, Lenovo Slim, selected legacy Lenovo gaming families
  - Basic-mode profiles: ThinkPad, ThinkCentre, ThinkStation, IdeaCentre, Legion desktop, XiaoXin, V series, Motorola, ASUS, MECHREVO/Mechanical Revolution, Dell, HP, Acer, MSI, Microsoft Surface, GIGABYTE/AORUS, Razer, Samsung, HUAWEI, Xiaomi/Redmi, HONOR, LG, Framework, Panasonic, Dynabook/Toshiba, Fujitsu, VAIO, MEDION, XMG/SCHENKER, System76, Star Labs, Slimbook, Clevo/Tongfang, and generic PCs
  - v4.0 adds a local device-support simulation matrix for ASUS, MECHREVO, HP, Dell, Acer, Xiaomi, and Huawei machine profiles, plus generic CPU/GPU sensor fallback for non-Lenovo basic mode
  - Chinese model naming variants are recognized where hardware control is supported (for example `R7000`, `R9000`, `Y7000`, `Y9000`)
  - Vendor matching normalizes common BIOS/DMI formatting differences so punctuation, casing, spacing, diacritics, and company suffix variants do not block a basic-mode match
  - Detection source: `UniversalDeviceToolkit.Lib/DeviceSupport/CatalogDeviceSupportProvider.cs` and `UniversalDeviceToolkit.Lib/DeviceSupport/LenovoDeviceSupportProvider.cs`
- **Dependencies**: .NET 10.0 Desktop Runtime; Lenovo drivers are required only for Lenovo hardware-specific controls

## Performance Characteristics

- **Memory Usage**: about 400MB typical with the dashboard and sensors running (Electron UI + .NET Host). Tray-only idle is lower (about 250-350MB) because auxiliary windows are destroyed and sensor/GPU polling stops.
- **CPU Usage**: <1% (tray idle), <5% (active monitoring)
- **Startup Time**: <2 seconds
- **Power Impact**: Electron uses EcoQoS when every window is hidden; the Host stays at Normal priority so hotkeys and automation stay responsive.
- **Installers**: Windows ships a Full offline installer and an Electron Online installer that downloads `*_Online_win-x64.zip` from the GitHub Release (the retired nsis-web `*.nsis.7z` payload is no longer published). Host publish output is pruned (`Scripts/Prune-ShippingFootprint.ps1`). In-app updates from 6.0.0 spawn the setup exe with `/S`; new installer EXEs stay `asInvoker` and self-elevate so that CreateProcess still works. Changing installer shape later must keep that 6.0.0 launch contract or ship a compatible stub.

## Security Considerations

- Local-only operation (no cloud dependencies)
- Hardware-level access (requires admin for some features)
- Renderer sandbox (`contextIsolation`, no Node.js) with Host-only privileged work
- Secure update mechanism (signature verification)

## Future Architecture Goals

- [ ] Web-based management interface (optional)
- Mobile and Android companion apps are out of scope and are not supported.
- [ ] Cloud sync for settings (privacy-first design)
- [ ] Enhanced telemetry option (opt-in only)
