# Test map

Host tests are split by project so Test Explorer, the solution, and CI use the same layers. Electron UI contracts run with `npm test`; the plugin system and its test projects were retired in 6.1.

## Projects

| Project | What it contains | How CI runs it |
| --- | --- | --- |
| `Tests/Contracts` | Guard and Security contracts (layout, CI YAML, signatures, path safety) | Fail-fast, no category filter |
| `Tests/Fast` | Isolation-free unit tests (network proxy IPC) | After Contracts |
| `Tests/Unit` | Parallel unit tests (no process-wide shared state) | Main parallel layer |
| `Tests/Stateful` | `[Collection(Localization/Settings/ProcessState)]` and PowerMode cache tests | Last; collection parallelism off |
| `Tests/CrossPlatform` | Portable diagnostics CLI | `cross-platform-cli` job (Ubuntu / macOS / Windows matrix) |
| `Apps/Electron/tests/*.mjs` | Electron/Host RPC, renderer, installer, and security contracts | `npm test` (with lint and typecheck) |

`TestCategories` (`Security`, `Guard`, `Unit`) is at most one trait per class. After the project split, CI selects by project; Category is optional documentation. Do not add `Coverage`, `Plugin`, `Utils`, `Controller`, or `Smoke`.

Host tests keep namespaces under `UniversalDeviceToolkit.Tests.*` (folder = namespace). Explorer grouping is by project.

## How to run

```bash
# Fail-fast contracts + Fast.Tests (same order as Scripts/Run-TestFailFast.ps1)
dotnet test Tests/Contracts/UniversalDeviceToolkit.Tests.Contracts.csproj -c Release
dotnet test Tests/Fast/UniversalDeviceToolkit.Fast.Tests.csproj -c Release

# Parallel unit, then stateful
dotnet test Tests/Unit/UniversalDeviceToolkit.Tests.csproj -c Release
dotnet test Tests/Stateful/UniversalDeviceToolkit.Tests.Stateful.csproj -c Release

# Cross-platform diagnostics
dotnet test Tests/CrossPlatform/UniversalDeviceToolkit.CrossPlatform.Tests.csproj -c Release

# Electron UI contracts
cd Apps/Electron
npm run lint
npm run typecheck
npm test
```

xUnit: Contracts and Unit set `parallelizeTestCollections: true`. Stateful sets `false` and uses `[CollectionDefinition(..., DisableParallelization = true)]` for Localization / Settings / ProcessState.

## CI ladder

Windows job `build-test-and-smoke` in `.github/workflows/Ci-tests.yml`:

1. Contracts
2. Fast.Tests
3. Unit (coverage)
4. Stateful (coverage)

The same workflow also runs `electron-ui-tests` (`npm run lint`, `npm run typecheck`, then `npm test`).

Release.yml runs the same four Host projects after the solution build.

## testhost file locks

`dotnet test` can leave `testhost.exe` holding output DLLs on Windows, so a later build fails with MSB3021 (file in use). The assembly name `UniversalDeviceToolkit.Lib.dll` is unrelated to the test project folder name.

Workarounds:

```bash
dotnet build Tests/Unit/UniversalDeviceToolkit.Tests.csproj -c Release
dotnet test Tests/Unit/UniversalDeviceToolkit.Tests.csproj -c Release --no-build
```

```powershell
Get-Process testhost -ErrorAction SilentlyContinue | Stop-Process -Force
```

```bash
dotnet build Tests/Unit/UniversalDeviceToolkit.Tests.csproj -c Release -o _test_out
dotnet test _test_out/UniversalDeviceToolkit.Tests.dll -c Release --no-build
```

Close Visual Studio Test Explorer / Live Unit Testing if the default `bin` path stays locked. CI always builds once, then tests with `--no-build`.

## Related

- Unicode: `node Tools/CheckSourceUnicode/check-unicode.mjs`
- Logs: `%LOCALAPPDATA%\UniversalDeviceToolkit\logs` (`main.log`, `renderer.log`, `host.log`)
- Host Debug build if `Host.exe` is locked: `-o %TEMP%\udt-host-build`

## 2026-09-12 lightweight refactor verification

Baseline: `2a43ce7d022f08aef78acb22ca77d14ea2f441ce`. Validation ran on Windows with .NET SDK 10.0.302. Implementation commits below exclude concurrent, unrelated promotion work. No changes were pushed.

### Behavior and compatibility

- Projects now live under `Apps`, `Libraries`, `Platforms`, `Tests` and `Tools`. All 23 solution project GUIDs, project filenames and assembly identities remain stable; no old source directories or forwarding copies remain.
- Renderer domains own their UI, API and stores. Network polling is isolated from optimization and rejects responses from earlier sessions. Shared bridge validation and byte conversion retain macro injection/recording validation and each caller's formatting semantics.
- Host tools separate network, cleanup and system optimization. Telemetry separates snapshot composition, subscriptions, FPS and settings. Device models now live beside their domains; 122 original enum/struct declarations retain their bodies, values and namespaces (apart from whitespace).
- Removed retired AnimationTiming, unused backend accent presets, duplicate startup tests and the unused automation step editor. ReleaseTagPolicy retains historical catalog-tag filtering. IExtensionProvider and its signed fan-extension loading boundary remain.
- Regression coverage includes missing sensor fields, provider failures, real zeros, subscription/background lifecycles, macro cancellation, concurrent settings saves, network failures/rollback, installation selection and compatibility route redirects.

### Validation results

| Check | Result |
| --- | --- |
| Windows solution build | Passed; 0 warnings, 0 errors |
| Contracts | 364 passed |
| Fast | 14 passed |
| Unit | 2756 passed, 17 skipped |
| Stateful | 458 passed |
| CrossPlatform | 180 passed |
| Electron tests | 194 passed, none skipped |
| Electron typecheck | Passed |
| ESLint | Passed with `--max-warnings 0`; no rules disabled |
| Electron production build | Passed; largest JavaScript chunk 449.57 kB |
| Windows Host Release publish | Passed with test hooks disabled |
| Development and published Host | ping and app.quit passed |
| Portable Host on Windows | net10.0 build: 0 warnings/errors; ping and app.quit passed |
| Cross-platform CLI on Windows | Current solution output starts and prints --help |
| Electron Windows unpacked package | Passed existing footprint budgets |
| Embedded Host audit | Passed shipping artifact/marker checks; embedded Host ping and app.quit passed |
| Unicode | 2143 files scanned, no violations |
| Paths and dependency structure | All project references resolve; no retired source-directory references; renderer module/domain cycles and shared-to-feature imports absent |
| Final Windows dependency restore | Passed in locked mode after portable validation |

The 17 Unit skips are pre-existing environment/manual cases covering Explorer, process-token privileges and a machine-dependent battery no-data path. No Linux/macOS machine or physical hardware-control workflow was exercised. Portable compilation on Windows is not Linux/macOS hardware validation. The local package is unsigned and unpacked; signed installer generation and an interactive installation were not performed. Existing installer-selection tests passed. npm still reports its pre-existing `electron_mirror` configuration deprecation; ESLint itself reports no warnings.

The published and embedded Host trees contain 366 byte-identical files. Host and NetworkProxy each have their executable, assembly, runtimeconfig and deps files. The package has no PDBs or app.asar node_modules. A stale 400 KiB retired plugin DLL in the previous local publish directory was removed; the shipping gate now rejects that assembly, as verified with a failing fixture. Test-assembly filename exclusions were also repaired and verified with a failing fixture.

### Size and dependency measurements

Source sizes use Git blob bytes for `.cs`, `.ts`, `.tsx`, `.mjs` and `.css`, including tests and tools. Build-output sizes use actual files. Domain splitting adds files and declaration headers; this change reduces duplicated responsibilities and coupling, not the framework/runtime footprint.

| Measurement | Before | After | Change |
| --- | ---: | ---: | ---: |
| Source files | 1,264 | 1,303 | +39 |
| Source bytes | 12,012,223 | 12,017,526 | +5,303 |
| Electron build-output bytes | 6,850,994 | 6,853,259 | +2,265 |
| Runtime dependency additions | - | 0 | .NET central packages and Electron dependency maps unchanged |
| app.asar | Not captured | 6,908,647 bytes (6.59 MiB) | No baseline comparison |
| Shipping Host | Not captured | 136,182,917 bytes (129.87 MiB) | Within 130 MiB budget |
| Unpacked application | Not captured | 477,200,808 bytes (455.09 MiB) | Within 470 MiB budget |

Source bytes changed by +0.044%; Electron output changed by +0.033%. No shipping baseline was captured, so no shipping-size reduction is claimed.

### Reproduction notes

Run .NET builds serially with `--disable-build-servers -m:1`. Build the root solution with `-p:EnableUdtTestHooks=true`, then run Contracts, Fast, Unit, Stateful and CrossPlatform in that order. Use `-p:Platform=x64` for CrossPlatform when reusing the root solution's x64 output; its default AnyCPU output can otherwise refer to stale local binaries.

```powershell
dotnet build UniversalDeviceToolkit.sln --disable-build-servers -m:1 -p:EnableUdtTestHooks=true
dotnet test Tests/CrossPlatform/UniversalDeviceToolkit.CrossPlatform.Tests.csproj --no-build --no-restore --disable-build-servers -m:1 -p:Platform=x64
dotnet publish Apps/Host/UniversalDeviceToolkit.Host.csproj -c Release -r win-x64 --self-contained true -p:EnableUdtTestHooks=false -p:DebugType=None --disable-build-servers -m:1 -o Apps/Host/publish/win-x64
powershell -NoProfile -File Scripts/Assert-ShippingPayload.ps1 -PayloadPath Apps/Host/publish/win-x64
node Apps/Electron/scripts/smoke-host.mjs Apps/Host/publish/win-x64/UniversalDeviceToolkit.Host.exe
```

For portable verification, use `-p:UDTWindows=false -p:NuGetLockFilePath=obj/portable.packages.lock.json -p:RestoreLockedMode=false -p:EnableUdtTestHooks=false` and a separate output directory. This keeps the portable lockfile out of source control. Restore the Windows solution afterward. The ordinary Windows publish can add RID entries to local lockfiles; restore only those generated local differences before the final locked restore.

From `Apps/Electron`, run `npm run typecheck`, `npm test`, `npm run lint -- --max-warnings 0`, and `npm run build`. Local unpacked validation used `npx --no-install electron-builder --config electron-builder.yml --win --x64 --dir --publish never --config.win.signAndEditExecutable=false`; production signing/resource options were not changed in repository configuration.

### Implementation commits

- `123cc436c` refactor: remove retired UI helpers and duplicate startup tests
- `f81da9b25` refactor: group projects by application library and platform
- `c2bbd1943` refactor(electron): organize renderer by feature domain
- `7a7c8736d` refactor(tools): separate network cleanup and driver state
- `1ed71baf9` refactor(host): group handlers and consolidate tool operations
- `12932dea4` refactor(electron): simplify editor and subscription lifecycles
- `0882dcb77` refactor(electron): share bridge validation and byte conversion
- `30c90beec` refactor(electron): remove feature dependency cycles
- `a7df1fd23` refactor(automation): remove unused duplicate step editor
- `183dff583` refactor(sensors): separate snapshots subscriptions fps and settings
- `dc7d64b4f` fix(electron): retain cached editors and capability fallbacks
- `506de52d3` fix(network): discard traffic responses after session changes
- `8c45964c6` fix(packaging): preserve test assembly name exclusions
- `e0751bd06` refactor(device): group sensors cooling lighting and update models
- `77617d62c` refactor(host): trim handler imports and redundant async wrapper
- `e2d117e65` test(cross-platform): locate CLI sources in application directory
- `67bf59913` fix(packaging): reject retired plugin assembly remnants

## Interface and offline compatibility follow-up (2026-09-12)

The trigger picker now uses the theme's surface/foreground tokens, fixing black
text on dark fallback cards in light Neo-Brutalism. RGB/Spectrum controls follow
the same theme, and the Spectrum preview fits narrow containers. Browser checks
used the actual React components with mocked hardware: light/dark trigger
contrast, RGB, a 332 px Spectrum preview, and Traditional Chinese zone labels.
These checks do not exercise physical keyboard lighting.

Settings and Tools retain visited tabs with React Activity. Hidden tabs stop
their effects while keeping local state; an input draft survived A-B-A switching
with only one active child effect. The main window is retained in the tray.
Forced cache clearing, explicit garbage collection, working-set trimming and
the 96 MB V8 old-space cap were removed. Disk cache is 32 MB and temporary
surface retention is five minutes. High-frequency subscriptions still pause
when no application surface is visible.

Localization changed 5,942 existing values (including the English style
description), with 2,361 previously English Traditional Chinese entries, and
added the RGB zone-number key to all 25 languages. New translations were
generated locally, checked for placeholders, names, numbers and formatting,
and sampled for meaning. Suspicious results retained English fallback; this is
not a claim of native-speaker review for all languages. The existing residual
English heuristic now reports 0 for Traditional Chinese and 0-4 per other
language. It excludes technical terms and some short/parameterized strings.

Validation passed: 200 frontend tests, TypeScript checks, production build,
ESLint with zero warnings, and the repository Unicode scan. The Windows Host
was published serially with `--disable-build-servers -m:1`; shipping-payload
validation and Host RPC startup/shutdown checks passed. Portable platforms
were not retested in this follow-up because their implementation did not change.

`npm run dist:win:compat` produced
`UniversalDeviceToolkitCompatibilitySetup-6.1.1.exe`: 132,842,307 bytes
(132.84 decimal MB, 126.69 MiB). Its NSIS wrapper contains one application
archive and a native uninstaller, without another Electron installer shell.
All 410 files extracted from the final installer matched the staged payload
by SHA256. The extracted Host passed the RPC startup/shutdown check. Measured
payload: 477,369,112 bytes; Host: 136,182,917 bytes; app.asar: 6,969,431 bytes.
No runtime dependencies were added. The installer and UDT executables are
unsigned local builds; a fresh-machine interactive install/uninstall and
physical hardware controls have not been verified.

At this stage the 40 MB lightweight target remained unmet. Diagnostic maximum-compression
archives measured 58,361,806 bytes for Electron's executable alone and
33,085,914 bytes for the self-contained Host tree. These component archives
are diagnostic measurements, not independently usable installers. A system
WebView2 shell would need an explicit prerequisite; the compatibility artifact
includes its browser and .NET runtimes for offline installation.

## Lightweight window startup repair (2026-09-13)

The previous lightweight package opened a blank window. Reproducing from its
CAB showed that the Host and React application started, but the WebView2
controller's initial `IsVisible` was false. Setting it explicitly restored the
dashboard and live readings. The shell now synchronizes controller visibility,
renderer polling and Host UI activity on minimize, restore and tray hide/show.
It keeps the existing renderer and caches alive while hidden.

The new `--diagnose-ui` runs the packaged renderer with a temporary browser
profile and safe-start Host, verifies visible page content and a JavaScript
bridge round trip, then exercises minimize/restore and hide/restore. This check
now gates lightweight packaging. It passed on the staged payload and again on
files extracted from the final NSIS installer. A deliberately empty renderer
failed with exit code 1. The older `--diagnose` passed on the broken package
because it only checked WebView2 availability and Host RPC startup.
All 462 payload files extracted from the EXE matched the CAB by SHA256.

The initial visibility repair produced a 34,791,335-byte installer, SHA256
`0d5b8c821defcce1984de61b8a1094d12e8d21d3bdd5e9e89581cf263bd26cd8`.
The CAB is 39,949,156 bytes, SHA256
`4c41bb4717e3a1d39eeddb8b835ed2d274115cbea8aaccbe6852651090ea6981`.
Both remain below 40,000,000 bytes. The lightweight edition requires system
WebView2; the fully offline Chromium compatibility installer is unchanged.

Validation: Windows shell Release build with zero warnings/errors, frontend
typecheck, ESLint with zero warnings, the nine native bridge and packaging
tests, production renderer build, and repository Unicode scan passed. NSIS
at that point reported 12 unused MUI variable warnings. This validates startup and
window lifecycle, not physical hardware writes or a fresh-machine install.

The lightweight packaging gate also runs `--diagnose-ui --minimized` to verify
hidden autostart and restoration. Three Fast tests cover activation before the
listener starts, repeated launches and restarting after the primary exits.
Secondary launches now restore the existing window; minimized bounds are never
saved as the next normal window position.

Window geometry checks now verify that the renderer fills the native window,
the top-left resize target remains available, and maximization fits the monitor
work area without covering the taskbar. The shell uses a centered 1180 x 780
logical-pixel default, scales for monitor DPI, and clamps restored bounds to the
available display. The exact old unscaled default is migrated; custom bounds
are retained with DPI metadata. Six geometry cases and three activation cases
passed in Fast tests, followed by the real WebView UI lifecycle check.

Visited dashboard, settings, actions, keyboard, tools and about pages now retain
their React state and DOM through Activity. Unvisited pages stay lazy; hidden
pages suspend effects and retain their own route/query context. Settings and
tools also retain their existing tab caches. No heap cap, forced collection or
working-set trimming was added. Runtime memory may grow as pages are visited.
The real WebView check follows the old optimization redirect, selects a tools
tab, leaves the page, verifies the hidden DOM remains connected, and returns
to the same tab and dashboard DOM. Frontend typecheck, zero-warning ESLint,
203 tests and the production build passed.

The native shell now extends DWM glass across the client area and supplies a
black background brush with horizontal/vertical redraw styles. This fixes the
material being confined to the native top strip and uninitialized resize areas.
If native material is unavailable, WebView uses an opaque light/dark background.
UI diagnostics resize the tools page through 68 steps across mica, off and
acrylic, check renderer/native viewport agreement and background alpha, and
leave an enlarged window for optional visual inspection. Set
`UDT_UI_INSPECTION_SECONDS=60` with `--diagnose-ui` to inspect that window before
automatic exit. A native screenshot after enlargement showed the tinted
navigation/content background with no black/gray stripe corruption.

Cursor fallback installation now copies validated assets to persistent user
data before writing registry paths. Three Unit cases verify that cursors survive
removal of the source installation, incomplete assets leave the active cache
intact, and updates leave no temporary files. The affected machine's old scheme
referenced the deleted pre-migration Host directory; its registry scheme was
backed up and repaired, then the system cursor refresh restored the pointer.

The final follow-up installers were rebuilt on 2026-09-13. The lightweight EXE
at 08:52:42 local time is 34,778,689 bytes (34.78 decimal MB), SHA256
`38aa7f7831fe50c80d96b9031490f9655964036ccd2b73550485053169d0b1b7`.
Its CAB is 39,958,101 bytes, SHA256
`f8231a30de309799442fb16690b65ac975cabc5ccc81fa3e10090415a122a5ee`.
Both remain below 40,000,000 bytes. All 461 final EXE payload files match the
CAB by SHA256, and the packaged Device library matches the newly published
cursor fix. The final extracted EXE passed the full UI diagnostic, including
page retention and resize/material checks; staged normal and minimized startup
also passed. Removing an unused MUI include eliminates the prior NSIS warnings.

The rebuilt offline Chromium compatibility EXE is 132,844,847 bytes, SHA256
`888362d000967083bc45b1f97544fe00b5b000bdc8d092b3a023158d89358f6a`.
It includes the shared cursor and page-cache changes. Lightweight still uses
system WebView2. The final compatibility installer was extracted to verify
renderer, Device library, .NET runtime and Chromium resource hashes against the
audited unpacked payload. Exit an already-running installed version before installing
the replacement; these checks did not overwrite the user's installed program.

The WebView2 tray now uses the executable's UDT icons and returns actual native
menu command IDs. Its menu restores power states, navigation, triggerless quick
actions, battery status and Open/Exit, respecting installer, capability and
navigation visibility gates. Both shells share 25 languages extracted from the
renderer catalog; `tests/nativeLocales.test.mjs` rejects stale translations.
Twelve focused Fast cases cover command dispatch, failure fallback, language
aliases and real Win32 submenu handles. Frontend typecheck, zero-warning ESLint
and 206 tests pass. Hardware-changing commands are tested with fake RPC delegates.

The startup window reproduced a wide white rim that the enlarged-window check
missed. The native shell now suppresses legacy non-client painting while keeping
activation and resize hit targets. UI diagnostics also check the client origin;
`UDT_UI_INSPECTION_PHASE=startup` pauses at the initial window instead of after
resizing. The final candidate passed automatic startup/resize/restore checks;
final visual confirmation was deferred when the user requested fewer test runs.

The lightweight setup now hosts the existing `Apps/Electron/installer` pages in
WebView2. Location, language, device mode, optional features and progress use the
same UI assets as Electron. The native bridge copies a packaged file manifest,
persists `installer-selection.ini`, omits disabled NetworkProxy files, and uses a
small NSIS helper for shortcuts and uninstall registration. The uninstaller
deletes only known payload paths, leaving unrelated files alone. Preview mode
cannot install or launch an arbitrary executable. Silent `/S` updates retain
existing selections, and `/D=` still selects the destination.

The build checks all four installer pages through the real WebView2 bridge in
preview mode. Three focused Fast tests cover file copying, feature/selection
compatibility and rejected destinations/manifests; eight existing installer UI
and selection tests pass. Native build and script lint pass with zero warnings.
`--skip-app-check` can avoid repeating the full application-window diagnostics
when they already passed in the same session; the new installer preview check
still runs. No live installation/uninstallation was performed on the user's
program. Installer-only pages and registration tools ship in the setup EXE;
the CAB remains the independently deployable application payload.

The restored-wizard WebView2 installer built at 2026-09-13 09:42:26 local time
is 34,878,195 bytes (34.88 decimal MB), SHA256
`2020b7d70eaf5dce19e47da53d8fc9f6e3e5a11ba5446af3ed696f6845084bef`.
The application CAB is 39,973,633 bytes; both meet the 40,000,000-byte budget.
Extracting the final EXE confirmed the original five installer UI assets match
the source, the native DLL matches the checked build, the registration helper
is present, and all 461 manifest files exist. This installer still uses system
WebView2; the separately delivered offline Chromium edition is unchanged.
