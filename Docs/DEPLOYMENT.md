# Universal Device Toolkit Deployment Guide

## Supported product and candidate status

WebView2 is the default Windows shell. Its installer and portable ZIP contain the complete React application, all supported languages and a self-contained win-x64 .NET Host. Microsoft Edge WebView2 Runtime must be installed on the system; a separate .NET Desktop Runtime is unnecessary.

The independent Electron compatibility installer includes Chromium and the same self-contained Host. Its native NSIS installation pages run without WebView2. Use it for unresolved Runtime or WebView2 initialization compatibility issues. Both shells use the same installation directory, shortcuts, uninstall registration and settings directory. Updating keeps the installed shell channel; a missing matching asset is an error, not an automatic channel switch.

Stable release: **6.1.3**. Candidate: **6.1.4, not released**. This candidate adds the separate compatibility asset. Local builds and CI artifacts are release preparation; creating a tag or publishing a Release requires a separate release action.

macOS/Linux Electron packaging and the portable Host are experimental. The CrossPlatform diagnostics CLI has Windows, Linux and macOS CI coverage. Windows-only hardware controls remain unavailable in the portable Host.

## Prerequisites

- Windows 10 (1809+) or Windows 11 for the full Windows solution and packaging.
- .NET 10 SDK and an IDE that supports it.
- Node.js 22 and the committed npm lock file.
- Microsoft Edge WebView2 Runtime for native-shell UI checks and WebView2 installer preparation.
- Git; Git Bash or WSL for `build.sh` when using that entry point.

```powershell
dotnet --list-sdks
dotnet --info
node --version
npm --version
```

NSIS comes from the electron-builder toolchain. Install frontend build dependencies with `npm ci` in `Apps/Electron`.

## Repository and build configuration

- `Apps/Windows`: native Win32/WebView2 shell, recovery and transactional installation helpers.
- `Apps/Electron`: shared React renderer, Electron compatibility shell, packaging scripts and Visual Studio launcher.
- `Apps/Host`: headless .NET backend using JSON-RPC over stdio.
- `Apps/CLI`, `Apps/CrossPlatformCLI`, `Apps/NetworkProxy`: Windows IPC CLI, portable diagnostics CLI and network helper.
- `Libraries` and `Platforms`: business logic, contracts and platform adapters.
- `Tests`: Contracts, Fast, Unit, Stateful, CrossPlatform and shared infrastructure.
- `Tools`: development-only validation, Unicode and localization tools; never shipped.

`Directory.Build.props` sets the version train, nullable analysis and warnings-as-errors policy. Windows projects use `net10.0-windows10.0.26100.0` and the shipping RID `win-x64`. Portable projects opt out of forced x64 through `DisableUdtForceX64`.

## Build and validation

From the repository root on Windows:

```powershell
dotnet restore UniversalDeviceToolkit.sln --locked-mode
dotnet build UniversalDeviceToolkit.sln --configuration Release --no-restore --disable-build-servers -m:1
```

Keep shared .NET builds serial to avoid intermediate-file locks. CI uses locked restore; intentional dependency changes must regenerate and commit all affected lock files before that check can pass.

Restore Electron tooling with `npm ci` in `Apps/Electron`. Electron binaries use their official download endpoint by default. If a local network requires a mirror, set the supported environment variable before restoring:

```powershell
$env:ELECTRON_MIRROR = 'https://npmmirror.com/mirrors/electron/'
npm --prefix Apps/Electron ci
```

Do not put `electron_mirror` in `.npmrc`; recent npm versions warn that this custom key will stop working.

Run the test ladder described in [TEST_DIAGNOSTICS.md](TEST_DIAGNOSTICS.md): Contracts, Fast, Unit, Stateful and CrossPlatform. Compile with zero warnings and errors. Frontend checks:

```powershell
npm --prefix Apps/Electron run lint
npm --prefix Apps/Electron run typecheck
npm --prefix Apps/Electron test
npm --prefix Apps/Electron run build
node Tools/CheckSourceUnicode/check-unicode.mjs
```

`npm run dev` in `Apps/Electron` uses Electron for hot reload. `npm run dev:web` connects a browser preview to the real Host. These are development views; native WebView2 parity must be checked with the packaged Windows shell.

For UI inspection, use the packaged `--diagnose-ui` path. It creates isolated data and browser directories, marks the Host diagnostic, and supplies `--no-hardware`. Such sessions register configuration services without hardware controllers or auto-activated listeners; hardware RPC returns disabled/unavailable results and lazy native sensor/EC/AMD driver access is blocked. These previews verify presentation and bridge behavior, not live hardware capabilities.

The diagnostic shell checks that its Host has the same source revision before starting it. Rebuild both together when using `--host`; an older Host may ignore the diagnostic marker and start hardware probes despite `--no-hardware`. The shell refuses mismatched or unidentified diagnostic builds.

Ordinary basic installations also use `--no-hardware`, but retain optimization, cleanup, network, driver downloads, macros, CLI and system automation. Hardware controls and hardware event triggers remain unavailable. This differs from diagnostic inspection, which disables system actions as well.

## Windows dual-package preparation

### Publish the shared Host

Both Windows shells spawn the same self-contained Host. Publish and prune it before packaging:

```powershell
dotnet publish Apps/Host/UniversalDeviceToolkit.Host.csproj -c Release -r win-x64 --self-contained true --disable-build-servers -m:1 -o Apps/Host/publish/win-x64
.\Scripts\Prune-ShippingFootprint.ps1 -PayloadPath Apps/Host/publish/win-x64 -RuntimeIdentifier win-x64 -AllowedCultures 'ar;bg;cs;de;el;en;es;fr;hu;it;ja;lv;nl-NL;pl;pt;pt-BR;ro;ru;sk;tr;uk;uz-Latn-UZ;vi;zh-Hans;zh-Hant'
```

### Local unsigned candidate

```powershell
.\Scripts\Build-WebView2Installer.ps1 -Version 6.1.4
.\Scripts\Build-CompatibilityInstaller.ps1 -Version 6.1.4
```

The equivalent npm entries are `dist:win` and `dist:win:compatibility` in `Apps/Electron`. PowerShell wrappers place the installers under `BuildInstaller`; npm defaults use `Apps/Electron/dist/windows` and `Apps/Electron/dist/compatibility`.

Expected named installers:

- `UniversalDeviceToolkitWebView2Setup-6.1.4.exe`
- `UniversalDeviceToolkitCompatibilitySetup-6.1.4.exe`

WebView2 additionally produces a portable ZIP and legacy Full/Online aliases. Full and Online are identical complete WebView2 payloads; they do not download the application or .NET runtime during installation. Their historical names preserve updater compatibility.

### Sign before final hashes

Release preparation uses the two-phase wrappers so signatures are inside the final containers:

```powershell
.\Scripts\Build-WebView2Installer.ps1 -Version 6.1.4 -PreparePayloadsOnly
.\Scripts\Build-CompatibilityInstaller.ps1 -Version 6.1.4 -PreparePayloadsOnly
# Sign and verify all EXE/DLL files under BuildInstallerPayload.
.\Scripts\Build-WebView2Installer.ps1 -Version 6.1.4 -PackagePreparedPayloads
.\Scripts\Build-CompatibilityInstaller.ps1 -Version 6.1.4 -PackagePreparedPayloads
# Sign and verify the final installers, then finalize release assets.
```

`Release.yml` signs Host and release payloads, prepares both shell payloads, signs and verifies the whole staged payload root, packages both installers, signs and verifies their final containers, reruns footprint gates, then invokes `Build-LanguageAssets.ps1 -FinalizeOnly`. Pass `CompatibilityInstallerPath` as well as the Full/Online installer and ZIP paths to include both channels in the final SHA256 manifest. Never reuse a pre-sign installer hash for a signed file.

Signing requires the configured Azure Trusted Signing credentials. The workflow fails its signing prerequisites unless an explicitly approved manual recovery run sets `skip_signing`. Local builds are unsigned; do not describe them as signed releases.

### Footprint gates

`scripts/package-footprint.mjs` is the source of truth for Electron budgets. Current gates are:

- WebView2 installer: **40,000,000 bytes**.
- Electron compatibility installer and standard distributables: **185 MiB**.
- Electron `app.asar`: **15 MiB**; Chromium locales: **20 MiB**, exact configured locale set.
- Electron Windows Host: **131 MiB**; unpacked Windows application: **470 MiB**.
- Experimental Linux/macOS Host: **92/100 MiB**; unpacked application: **450/500 MiB**.

The final post-sign check applies the exact byte budget to the WebView2 installer and the compatibility budget to the separate Electron installer. `Assert-ShippingPayload.ps1` rejects test/tool/PDB remnants. Keep Chromium licenses and required GPU/SwiftShader files; both Hosts stay self-contained.

The 6.1.4 Windows CI measurement is 136,323,865 bytes (130.01 MiB) for the complete self-contained Host. Its directory budget rounds up to 131 MiB; the WebView2 installer limit remains 40,000,000 bytes. Evidence: [Package Footprint run 37610312133](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/actions/runs/37610312133), artifact `package-footprint-win-x64-37610312133`.

No startup-time or memory comparison is claimed here. Record measurements with machine, build and shell variant using [UI_PERFORMANCE.md](UI_PERFORMANCE.md).

## Installation and update behavior

The WebView2 installer stays `asInvoker` and self-elevates, preserving silent `/S` launches by older in-app updaters. The Electron compatibility wizard requests administrator rights through native NSIS and uses native installation pages; its updater supports the ShellExecute elevation fallback. WebView2 startup and setup recovery use native localized dialogs when Runtime is missing or initialization fails.

Replacement stages the new owned payload beside the target directory and backs up existing owned files before writing. File ownership comes from `resources/install-files.json`; cleanup is limited to the previous manifest's files. When that manifest exists, an existing file at a new payload path must also be owned by the previous manifest; an unowned same-path collision refuses installation before replacement. Registration and shortcut failures roll back the transaction. Unrelated files and application data are retained. The new ownership list records the selected features, so uninstall also preserves files at paths belonging to omitted features.

The same destination can be used again after uninstall leaves user files there. When neither an ownership manifest nor a legacy application exists, installation preserves those files and refuses any collision with a new payload or installation metadata path before writing.

Before replacing files, both installers stop the previous shell only when its executable matches the destination's root `UniversalDeviceToolkit.exe`. They then run `--restore-network-state` with the incoming package's Host, so recovery does not depend on an older Host supporting that command. A refused or failed recovery aborts replacement and retains the existing installation. Maintenance timeout stops its launched process before reporting failure, preventing that process from modifying network state after replacement has been aborted.

Older Electron installations without an ownership manifest keep unidentified files that the new package does not replace, including stale Chromium files after migration to WebView2. Existing files that must be replaced are moved to a separate `.udt-backup-<guid>` directory beside the installation, and that backup is retained after successful migration. The installation progress warning includes its full path, including in the native compatibility wizard's details. The installer does not run a legacy uninstaller that could recursively remove user files.

The installed channel marker is `webview2` or `electron-compatibility`. Updates select only the matching installer asset, check the named SHA256 entry, and recheck the downloaded file before launch. Missing channel assets, corrupted hashes, incomplete downloads and launch errors must be visible.

Both shells use `%LOCALAPPDATA%\UniversalDeviceToolkit` for settings and logs. Before deleting files, uninstall stops the selected shell first to prevent automatic Host restart, then stops other running executables whose exact paths are in both the packaged executable list and the selected installation manifest; it excludes the uninstaller itself. Same-name processes outside the installation and user executables omitted from that manifest are retained. A stop failure reports the native Windows error and preserves registration and ownership records.

The native uninstaller then invokes the owned Host with `--restore-network-state`, from the WebView2 installation root or Electron `resources/host` layout. This independent command runs before hardware or IoC initialization, restores only the saved network snapshot and prints the result in native installation details. It starts no proxy worker and does not reset `args.txt`. A same-user lease prevents interference with another active UDT session even when that session uses a different data-directory override. A snapshot's current owner still checks for foreign workers; its own worker is excluded only by matching PID, start time and executable path. Unknown ownership, an active foreign worker, an unavailable lease or a damaged snapshot refuses recovery. The uninstaller then preserves installed files, ownership manifests, itself and registration so recovery can be retried. A missing snapshot gives this independent command no saved state to restore and is a no-op; an existing empty, malformed or JSON `null` file is a failure and remains untouched. During a live service session, a missing snapshot after tracked system changes remains a recovery failure.

Only successful process stopping and network recovery permit removal of owned installation files and registration. Settings are preserved. For isolated testing, use the supported `UDT_APPDATA_OVERRIDE` path and record the effective data directory; that override does not isolate the Windows system proxy or its same-user lease.

## Candidate acceptance

Save the build/check output and screenshots in the verification record linked from [TEST_DIAGNOSTICS.md](TEST_DIAGNOSTICS.md). Validate both actual installers on an interactive Windows desktop:

- Clean installation and WebView2 Runtime repair/retry behavior.
- Old Electron to WebView2 upgrade and replacement in both directions.
- Same-channel updates and explicit failure when the matching asset is absent.
- Wrong SHA256, interrupted download, failed installation and rollback.
- Shared shortcuts, uninstall record, preserved unrelated files and settings after uninstall.
- Main window, tray, native/compatibility OSD, drag/lock/position/hotkey, DPI and language switching including RTL.
- Keyboard page, file picker, zoom, settings-page navigation and restored scrolling.
- Navigation failure, renderer crash recovery and separate Host failure diagnostics.

Hardware controls require observed read/write/readback evidence on the actual machine. UI, unit and emulator checks do not prove support for every model. Record untested hardware and manual cases explicitly.

## CI and release procedures

`Build.yml` invokes `Make.bat` and uploads `release-assets`. `Ci-tests.yml` runs locked restore, solution build, .NET suites, Unicode/frontend checks, and the cross-platform CLI matrix. `package-footprint.yml` builds both Windows packages and experimental non-Windows packages and audits their size. `CodeQL.yml` supplies static security analysis.

`Release.yml` is the official Windows release pipeline; its tag/manual release action publishes assets. Preparing 6.1.4 does **not** authorize creating its tag or triggering this workflow. Before a formal release, review candidate evidence, final signed hashes and CI, then obtain the release decision.

Version values must agree in `Directory.Build.props`, root/Electron package manifests and installer display text. Maintain the candidate changelog separately from the last stable release.

`Scripts/New-ReleaseNotes.ps1` accepts the versioned `Unreleased candidate` changelog heading for preparation. Generated notes list both installers and retain an explicit unreleased status without assigning a release date. Generating this local document does not publish assets or create a tag.

## Cross-platform builds

The full solution contains Windows-only projects. On macOS/Linux, build portable libraries and the diagnostics CLI:

```bash
./build.sh Release
./build.sh Release linux-x64
dotnet test Tests/CrossPlatform/UniversalDeviceToolkit.CrossPlatform.Tests.csproj --configuration Release

# Experimental portable Host, not an official desktop product:
UDT_PLATFORM=linux ./build.sh host
UDT_PLATFORM=macos ./build.sh host
```

Equivalent portable Host publishes need `-p:UDTWindows=false`; do not use the Windows target framework with Linux/macOS RIDs. Portable Host returns `-32099` for unsupported Windows-only RPC.

`npm run dist:mac` and `npm run dist:linux` require a previously published portable Host under `Apps/Host/publish/osx-*` or `linux-x64`. macOS DMGs need explicitly configured signing and notarization for those services; Linux AppImage/DEB assets are unsigned.

The manually dispatched `experimental-packages.yml` works against an existing release tag. With `attach_to_release=false`, it uploads workflow artifacts only; explicit `true` attaches experimental assets and their separate hash manifest. It never creates a tag or Release or replaces official Windows assets.

## Localization and screenshots

Translations are configured in `crowdin.yml`. After fetching translations, audit missing/extra keys, placeholders and RTL; run frontend checks and the full Windows build. Renderer text lives under `Apps/Electron/src/renderer/src/shared/i18n/locales`; native recovery text lives in `Apps/Electron/src/shared/i18n/recovery-catalog.json`.

README screenshots use a 1300 by 850 logical main window; record the DPI scale and shell used. Capture English and Simplified Chinese dark-theme windows for `Assets/Screenshot_main.png` and `Assets/Screenshot_zh-hans.png`. Record the actual refresh date in the changelog. Brand assets live in `Assets`; trailers must show the actual application rather than an illustrative mockup.

## Distribution channels

GitHub Releases is the maintained download source. winget's 6.x ID is `SSC-STUDIO.UniversalDeviceToolkit`; it remains pending until its upstream manifest is accepted. The Scoop bucket has not yet been published. Do not advertise working package-manager installation commands before those channels exist.

After publishing a stable release and final hashes, use `Packaging/Prepare-PackageManifests.ps1` and `Packaging/Test-PackageManifests.ps1` to prepare and validate the new identities. Submit winget to `microsoft/winget-pkgs`; the planned Scoop manifest is `universaldevicetoolkit`. Legacy package IDs do not upgrade in place. Package-manager and promotional publication require their own release decision.

## Troubleshooting and rollback

Build failures: verify .NET/Node versions, run locked restore, inspect the failed project's logs, and serialize .NET operations. After an intentional dependency update, refresh the affected locks through restore and inspect the diff.

Installer failures: verify the pruned self-contained Host, Runtime for the primary shell, the correct shell marker, final hashes and local setup logs. Runtime failure offers repair/retry or compatibility download; backend faults have separate diagnostics. Never relabel an unverified package as functional.

For a release regression, preserve the report and local logs, identify the affected artifact, and prepare a reviewed fix from the appropriate stable state. Deleting a Release does not automatically downgrade installed clients; publish an approved corrective version and matching hashes through the normal release process.
