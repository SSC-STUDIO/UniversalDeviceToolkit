# Changelog / 更新日志

All notable changes to this project will be documented in this file.
此项目的所有重要更改都将在此文件中记录。

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
格式基于 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)，
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
并遵循 [语义化版本](https://semver.org/spec/v2.0.0.html)。

## [Unreleased]

## [6.1.4] - Unreleased candidate

### Added / 新增
- Independent Electron compatibility installer, `UniversalDeviceToolkitCompatibilitySetup-<version>.exe`, with bundled Chromium, self-contained Host and native NSIS installation pages. WebView2 remains the default Windows download; both shells share one installation and settings directory.
- Native WebView2 OSD with shared layout/value formatting, sensor and FPS data, opacity, always-on-top, dragging, locking, saved position and toggle hotkey.
- Localized native Runtime repair/compatibility download and interface retry dialogs. About and diagnostic output expose the readonly shell variant; Host startup faults keep separate diagnostics.

### Fixed / 修复
- Updates follow the installed shell channel and require the matching named SHA256 entry. Missing assets, interrupted downloads and integrity failures remain visible.
- Installation stages the new payload and backs up owned files before replacement; failed registration restores the prior installation. Cleanup is limited to the previous ownership manifest and preserves unrelated files and settings.
- Failed installation registration restores the prior metadata and owned files; uninstall failures preserve registration and ownership records for retry. Registry rollback preserves untouched records, and ownership manifests reflect the optional modules selected for the installed package.
- An existing ownership manifest prevents replacement of unowned files at new payload paths. Legacy Electron migrations retain replaced files in a separate backup and show its location in installation details.
- Reinstallation succeeds when uninstall leaves user files in the chosen directory. Without a previous ownership manifest or legacy application, payload paths that collide with retained user files refuse installation before any files change.
- Network Start, Stop and Restore serialize their lifecycle changes and share a lease across sessions of the same Windows user, including sessions using different data-directory overrides. Proxy workers stop when their verified owning Host exits.
- Network snapshot recovery verifies the owner and worker PID, start time and executable path before stopping a recorded orphan. Active or unverifiable foreign ownership blocks recovery; empty, malformed, JSON `null` and unsupported snapshots remain intact and report failure.
- Shell replacement restores the previous network snapshot through the new package's independent Host before replacing files. Native uninstall stops selected owned executables, runs independent Host recovery before deletion and preserves the installation when recovery fails; unrelated same-name processes remain running.
- WebView2 bridge replies stay bound to their originating document, so delayed replies after interface recovery cannot complete unrelated requests. Same-document hash routing keeps pending requests valid, and browser recovery retains the original startup arguments.
- Electron OSD restores sensor and FPS subscriptions after Host restart, applies FPS item and refresh-interval changes while visible, and cancels unfinished show requests when hidden. Native OSD preserves reachable saved monitor positions; FPS subscriptions accept omitted optional parameters.
- Dashboard sensor startup continues with cached display settings or defaults when settings loading fails. The existing retry reloads settings and sensor data, and late completions after unmount do not update the page.
- Windows target-framework/RID dependency locks and missing permission translations have been corrected.
- Diagnostic sessions isolate Host settings, logs and browser profiles, skip legacy user-data migration and global network recovery, and avoid hardware writes during shutdown. Electron data-directory overrides also cover its profile, cache and external arguments.
- Diagnostic sessions now exclude auto-activated hardware listeners and controllers from the Host container, reject hardware and system actions through RPC, and block lazy sensor, EC and AMD native-driver access. Configuration stays available while readings explicitly report unavailable hardware.
- Isolated UI sessions show a persistent localized diagnostic banner explaining unavailable hardware cards and system actions. Navigation, action tabs and both trays consistently honor installation choices, saved visibility and explicit Host capabilities.
- Basic mode preserves existing optimization, cleanup, network, driver downloads, macros and system automation without registering vendor hardware. Saved hardware steps load normally and report unavailable hardware when executed.
- Device matching recognizes ten additional official Lenovo business-model MTMs in basic mode, corrects five existing gaming-series mappings and normalizes CTO/SKU identities. Ambiguous vendor, family and placeholder matches retain generic safety; these additions do not claim physical hardware-control validation.
- Hardware state writes recheck feature support after validating the requested state. Unsupported features and failed support probes return `NOT_SUPPORTED` without calling the setter.

### Changed / 变更
- Focus now uses distinct matte light/dark instrument panels, ruled navigation, separate sensor panels, compact corners and flat controls. Accent colors mark selection and actions; Mica/Acrylic no longer override its solid neutral surfaces.
- Navigation arrows and content spacing follow RTL languages, including live language changes while the navigation is collapsed.
- Windows packaging prepares and audits both installers, signs payloads before packaging, signs final installers before generating final hashes, and retains the 40,000,000-byte WebView2 installer budget.
- Hidden pages keep their cache while polling pauses; a visible OSD retains its own subscription. README, architecture and deployment guidance now describe WebView2 as primary and Electron as compatibility fallback without unverified performance claims.
- Source version train is **6.1.4**. This is release preparation; **v6.1.3 remains the current stable release** and no 6.1.4 Release or tag has been published.
- macOS support is paused: its source and configuration are retained, while macOS builds, packaging and CI validation are inactive. Windows remains primary and Linux remains experimental.

## [6.1.3] - 2026-10-03

### Fixed / 修复
- The setup window is 760 by 520 and keeps Windows 11 rounded corners.
- The console scrolls again after leaving Settings. A cached settings page stayed mounted, so `:has()` still matched it and locked the main pane to `overflow: hidden`, which clipped the dashboard.
- A low-power adapter warning no longer appears when Lenovo WMI returns an empty `IsACFitForOC` or `GetPowerChargeMode`. An empty read is checked through CIM. The warning is shown only when a real value is not 1. If both reads fail, the connected adapter is left alone.

### Changed / 变更
- Source version train is **6.1.3** (`Directory.Build.props`). Official ship tag is `v6.1.3`.

## [6.1.2] - 2026-10-02

### Fixed / 修复
- Dashboard trend charts stay inside the sensor card. The drawing surface had been taller than the gray well, so the battery rate and temperature were clipped off the bottom. A 0 W charge rate now stays visible just above the baseline, and battery temperature uses the 60 C scale.
- GPU readings prefer a live GPU Core temperature and D3D 3D utilization. A later empty sensor (GPU Memory Junction, or D3D VR at 0%) no longer replaces them.
- When NVAPI does not return a GPU temperature, the reading falls back to nvidia-smi, including the copy installed under System32.

### Changed / 变更
- Launching from Visual Studio without administrator rights now asks Windows to restart the app elevated. If that prompt is declined, the dashboard says that CPU temperature and fan speeds need administrator permission.
- Source version train is **6.1.2** (`Directory.Build.props`). Official ship tag is `v6.1.2`.

## [6.1.1] - 2026-09-04

### Added / 新增
- Startup "update available" status banner now has an Update action (opens the same dialog as Settings → Check for updates) and a dismiss X.

### Fixed / 修复
- In-app updates from 6.0.0 failed with "The installer could not be started" because 6.1 portable/online setup EXEs requested `requireAdministrator`, so `spawn(setup.exe, /S)` from the unelevated Electron process never started. 6.1.1 installer builds stay `asInvoker`, honor `/S` by self-elevating then installing silently, and current clients also fall back to `Start-Process -Verb RunAs` after clearing the download Mark-of-the-Web.

### Changed / 变更
- Neo-brutalism skin now covers every control family: buttons (custom + Ant Design solid variants) get hard plates with a press-into-the-page active shift, and inputs, selects, sliders, dropdown menus, modals, message toasts, notifications, status banners, and keyboard-page cards all take the heavy-stroke / hard-offset-shadow chrome in both light and dark combos.
- Dashboard sensor metric and VRAM-clock bars animate via GPU-composited `transform: scaleX()` instead of `width`, removing per-second relayout during 1 Hz sensor polling.
- The Settings "UI Animations" toggle now also stops page entrances, list stagger, tab fades, and other decorative keyframes (previously only windows/dialogs); duplicate conflicting button press rules and hardcoded status-banner colors/durations were consolidated onto the shared design tokens.
- Source version train is **6.1.1** (`Directory.Build.props`). Official ship tag is `v6.1.1`.

## [6.1.0] - 2026-08-31

### Added / 新增
- **Cursor & Pointer (built-in)**: Former CustomMouse plugin capabilities are now native Host features — custom high-DPI cursor themes (`W11-CC-V2.2-HDPI` shipped with the host), per-UI-scale cursor sizing, and pointer speed controls. Existing settings from the retired plugin's `config.json` are imported automatically.
- Renderer capability store and platform-adaptive navigation so pages that need Windows-only Host features stay hidden on experimental macOS/Linux shells.
- Windows installer welcome flow: one-click quick install, resizable window, UI scale toggle, and an Electron-based Online installer with multi-mirror payload download.
- New manually dispatched `experimental-packages.yml` workflow builds the experimental macOS DMGs (arm64 + x64) and Linux AppImage/DEB (x64) for an existing release tag, audits them against the package footprint budgets, and can attach them to that GitHub release with a dedicated `*_Experimental_SHA256.txt` manifest. These assets stay experimental (unsigned, no auto-update, no hardware control); `Release.yml` remains the only official Windows release pipeline.

### Changed / 变更
- Electron ships a native Mouse (cursor & pointer) page in System Optimization, including navigation, i18n copy for 25 locales, and installer feature flags without the former `pluginExtensions` option.
- The Windows CLI IPC binary is `udt` (`udt-cli` remains a compatibility shim). The IPC server keeps the legacy shell-integration operation codes as compatibility stubs: status checks report not-installed and install/uninstall requests fail with "Shell integration is no longer available."
- Electron renderer sandbox is on for the main window. Unexpected top-level navigation and new windows are denied. Main-process handlers validate the current main frame, restrict `open-external` to HTTP(S), block executable/script paths from `open-path`, and rate-limit power actions.
- Sensor trend updates stay immutable, and page timers are cleaned up on unmount so dashboard polling cannot leak intervals.
- CI `electron-ui-tests` now runs `npm run lint` before typecheck and tests. Plugin authoring docs under `Docs/Plugins/` are gone; see `Docs/README.md`.
- Source version train is **6.1.0** (`Directory.Build.props`). Official ship tag is `v6.1.0`.

### Removed / 移除
- **The entire plugin system is retired** (~15k lines of infrastructure):
  - Deleted `UniversalDeviceToolkit.Lib.Plugins`, the Plugins source tree (`Plugins/`) with official CustomMouse/ShellIntegration/ViVeTool sources and tests, the plugins SDK tooling, the plugins issue templates, and the `plugins-build` / `plugins-validate` / `plugins-release` CI workflows.
  - Deleted the Electron Plugin Extensions page, plugin stores/views/IPC webview plumbing, plus all plugin RPC handlers (`plugins.*`, `plugin.*`).
  - Pruned `--no-plugins` host flag, plugin settings/navigation keys, optimization-category extension pipeline, plugin catalog tags from update filtering docs, crowdin/i18n references to plugin resources, and coverage/tooling lists referencing retired projects.
  - Legacy `%LOCALAPPDATA%\UniversalDeviceToolkit\plugins` data stays on disk but is no longer loaded; the `plugin-catalog` / `plugin-catalog-preview` release tags are historical archives only.
- Store entry stubs mark `custom-mouse`, `shell-integration`, and `vive-tool` as `Removed`; `shell-integration` was previously delisted from the store.

### Fixed / 修复
- Automation and Macro pages no longer crash when a capability probe fails (Rules of Hooks: capability early-return ran before later hooks).
- Vendor WMI searchers are disposed after use, with a short TTL cache on soft-fail probes, and Acer/HP WMI facades are disposed with the Autofac lifetime. Vendor sensor fallbacks no longer block on `.Wait` / `.Result`.
- Sensor publish, LibreHardwareMonitor reset, and GameBoost teardown wait for in-flight work instead of racing dispose against a live poll.
- Unit tests that mutate `Compatibility`'s static machine-information cache are serialized so parallel testhosts cannot flake `IGPUModeCapabilityFeature` (and related) assertions.

## [6.0.0] - 2026-08-24

### Added / 新增
- **Complete Electron Desktop Modernization**: Brand-new high-performance UI shell built with React 19, Vite, Ant Design 6, Fluent UI tokens, and ECharts.
- **Interface Font Customization (界面字体自选)**: 6 curated typography presets (System Default, Microsoft YaHei UI, Segoe UI Variable, Noto Sans SC, HarmonyOS Sans, Geek Monospace) with zero FOUT and live hot-switching across 25 locales.
- **Deep Engine Trimming & Extreme Low Memory**: Field-measured working set is **30–300 MB peak** depending on pages and plugins; tray idle stays in the **30–60 MB** range after the main renderer is destroyed (0 MB DOM residency). Instant cold start on par with VS Code.
- **Multi-Brand Universal Hardware Platform**: Full sensor monitoring and feature controls for Lenovo Legion/Xiaoxin, ASUS ROG/TUF, MSI, Tongfang/Mechrevo, and Clevo chassis.
- **Advanced Utilities Matrix**: Integrated system cache cleaning (DirectX/Vulkan shaders, chat caches, dev tool temps), battery health analytics & discharge telemetry, and automated macro workflows.

- Official plugins (CustomMouse, ShellIntegration, ViveTool) ship real Electron `contributes.webPage` settings UIs, backed by Host `plugin.customMouse.*` / `plugin.shell.*` / `plugin.vive.*` RPC rather than plugin-directory `config.json`.
- README trailers (`Assets/UDT_Promo_en.mp4`, `Assets/UDT_Promo_zh.mp4`) with separate English and Chinese voiceovers, plus console screenshots (`Assets/Screenshot_main.png`, `Assets/Screenshot_zh-hans.png`) using UDT's own accent instead of Windows-blue card chrome. `Assets/UDT_Promo.mp4` remains as a copy of the English cut. Promo recording kit lives under `Docs/promo/`. Ready-to-post EN/ZH copy is in `Docs/PROMOTION_*.md`.
- Linux Host now starts on Linux and returns live sysfs/proc sensors instead of a stub.
- Windows installer can omit optional modules; omitted pages stay hidden in the shell.
- Unified log directory `%LOCALAPPDATA%\UniversalDeviceToolkit\logs` (`main.log`, `renderer.log`, `host.log`); Host forwards Serilog lines as `host.log` events and wires `SharedLog` to Serilog. `UDT_LOG_PATH` is the current env alias; `LLT_LOG_PATH` remains a compatibility alias.
- Dual plugin catalogs: **v5.0.2** still reads rolling `plugin-catalog` (1.x). Stable **v6.0.0** hosts (no hyphen in InformationalVersion) read the same `plugin-catalog` for official **2.0.0** packages. Preview hosts (`v6.0.0-preview.N`, InformationalVersion contains `-`) still read `plugin-catalog-preview`. `IncludePrereleaseUpdates` stays an application-update switch and does not change the plugin catalog.

### Changed

- Shipping Host/CLI/NetworkProxy now use Workstation GC with `GCConserveMemory=5` and DATAS, strip debugger/hot-reload/HTTP-activity IL from Release publishes, and prune `createdump`, DAC/DBI, DiaSymReader, Mono.Posix helpers, and sibling documentation XML from the payload.
- Host sensor producers pause while Electron reports no visible UI surface (`app.setUiActive`), so LibreHardwareMonitor / vendor WMI are not polled into a tray-only session.
- Electron disables unused Chromium features (translate, media router, autofill, spare renderer, breakpad), turns off spellcheck, loads Ant Design locales on demand, and splits vendor/icon/chart chunks from the actual module graph instead of barrel package entries.
- Electron release packaging now keeps renderer-only packages out of production dependencies, limits application Chromium locales to the supported 24-locale set, removes shipping Host PDB files, and audits `app.asar`, locales, Host, unpacked directories, and final artifacts. Windows x64 unsigned local output: `app.asar` 10.08 MiB, Full Setup 181.99 MiB, Full ZIP 186.99 MiB, Online Setup 0.61 MiB, and Online ZIP 184.39 MiB (previous Full Setup/ZIP: 214.24/245.76 MiB).
- Source version train is **6.0.0** (`Directory.Build.props`). Official ship tag is `v6.0.0` (no hyphen = GitHub latest, not prerelease). Official plugins are **2.0.0** with `minHostVersion` **6.0.0**. Vendored plugin host baseline stays **5.0.2** until this release's v6 ZIP exists, then refresh in a follow-up.
- Host tests are split into `Tests.Contracts` (Guard/Security), `Tests` (parallel unit), `Tests.Stateful` (collection-bound), and existing `Fast.Tests`. Plugin tests run in `plugins-validate.yml`; Electron `npm test` runs with typecheck.
- `PluginManager.SynchronizeStateStore` persists Windows plugin state through `ApplicationSettings.SynchronizeStore` instead of recursing into itself.
- `plugins.list` always includes `directory` and `webPage` for installed plugins so the Electron shell can load local plugin pages. Settings opens the web page when `webPage` is present.

### Removed

- `UniversalDeviceToolkit.ViewModels` and its tests (WPF-era ViewModels unused by Electron).
- Orphan plugin XAML design tokens and unused `WPF-UI` package version. WPF-era tools already deleted from the tree stay deleted.

## [5.0.2] - 2026-08-05

### Added

- Consolidated the official plugin source, SDK, tooling, tests, documentation, and release automation into the main repository under `Plugins/`.
- Added the managed `plugin-catalog` release flow for publishing one catalog and the current official plugin packages without cluttering application releases.

### Changed

- Plugin discovery and package downloads now use the main repository's fixed catalog release; the application update checker ignores the rolling catalog tag.
- Preserved the legacy plugin loading and application update path needed for existing installations while directing upgraded clients to the main repository.

## [5.0.1] - 2026-07-23

### Added / 新增
- **Progress toast notifications** with determinate bars: plugin downloads and cleanup runs report live percentage into a persistent toast; toast cards get a severity accent pill, tinted icon chip, and unified Medium title weight / **进度通知**：插件下载与垃圾清理在常驻通知中实时更新百分比；通知卡片新增严重度色条、着色图标块，标题字重统一为 Medium
- **GitHub proxy-mirror fallback** for plugin / language-pack / resource downloads (gh-proxy.com, ghfast.top) so installs succeed where github.com release assets are unreachable / **GitHub 镜像回退下载**：插件、语言包与资源下载在 github.com 不可达时经 gh-proxy / ghfast 镜像完成
- **Online language packs**: 24 culture packs + catalog hosted in-repo under `resources/stable/` (served via jsdelivr with raw+mirror fallbacks), regenerated by `Tools/Publish-LanguageResources.ps1` / **在线语言包**：24 个语言包与目录托管于仓库 `resources/stable/`（jsdelivr 主路径 + raw/镜像回退），由发布脚本再生成
- `UDT_PLUGIN_SIGNATURE_MODE` environment override (require/development/disable) for local unsigned plugin development / 插件签名策略环境变量（本地未签名插件开发用）
- Skeleton blocks now breathe (opacity floor 0.82) in lockstep with a wider, softer shimmer sweep / 骨架屏骨块随更宽更柔的扫掠同步呼吸
- **[Brand / ABI Phase 3 hard cutover]** Primary host/plugin binary identity is now UDT-named: Lib and Lib.Plugins `AssemblyName` / `RootNamespace` / C# namespaces are `UniversalDeviceToolkit.Lib*`; Windows CLI `AssemblyName` is `udt` (`udt.exe`, alias `udt-cli.exe`, was `llt`). Preferred CLI IPC pipe is `UniversalDeviceToolkit-IPC-0`; legacy `LenovoLegionToolkit-IPC-0` remains dual-listened. Plugin load still accepts `UniversalDeviceToolkit.Plugins.*` and legacy `LenovoLegionToolkit.Plugins.*` during transition. Automation continues dual-writing `LLT_*` and `UDT_*` env keys. `BrandCompatibility.Legacy*` constants retain old product/assembly string tokens. Not a zero-Lenovo-token state (packaging/winget paths and script-facing keys may still use LLT names). Docs: `Docs/NamespaceMigration.md` rewritten as completed cutover. / **[品牌 / ABI 第三阶段硬切换]** 宿主与插件主 ABI 现为 UDT 命名：Lib / Lib.Plugins 的 `AssemblyName`、`RootNamespace` 与 C# 命名空间为 `UniversalDeviceToolkit.Lib*`；Windows CLI `AssemblyName` 为 `udt-cli`（`udt-cli.exe`，原 `llt`）。首选 CLI IPC 管道为 `UniversalDeviceToolkit-IPC-0`；兼容管道 `LenovoLegionToolkit-IPC-0` 仍双监听。插件加载过渡期同时接受 `UniversalDeviceToolkit.Plugins.*` 与旧前缀 `LenovoLegionToolkit.Plugins.*`。自动化环境变量继续双写 `LLT_*` 与 `UDT_*`。`BrandCompatibility.Legacy*` 保留旧产品/程序集字符串。并非零 Lenovo 残留（打包/winget 路径与脚本侧键名等仍可能含 LLT）。文档：`Docs/NamespaceMigration.md` 已改写为已完成切换说明。
- Multi-agent network acceleration diagnostics and compact status-chip layout work on the network/UI surface / 多代理协作完善网络加速诊断与状态芯片紧凑布局
- Dashboard sensors loading skeleton ownership (page-owned chrome, shared shimmer coordinator) and theme retints for notification glass / 仪表盘传感器骨架屏归属与共享 shimmer 协调，以及通知玻璃主题重着色

### Fixed / 修复
- **Plugin online install chain**: plugin item bindings qualified with `DataContext.` (download/uninstall buttons no longer co-exist or no-op); mirror URLs now unwrap to the inner GitHub URL for official-package trust so mirrored installs load; truncated bodies (proxy closing mid-body) detected and retried before failing over; integrity failure during candidate iteration falls through to the next mirror instead of aborting / **插件在线安装链路**：修复下载/卸载按钮共存与点击无反应（绑定缺 `DataContext.`）；镜像 URL 信任评估解包为内层 GitHub URL，镜像安装的包可正常加载；下载中途断流可被检测并重试换源；完整性校验失败不再终止安装而是切换下一候选
- **Download stalls**: `CheckCertificateRevocationList` no longer stalls TLS handshakes when CA revocation endpoints are unreachable; resource catalog candidates capped at 20s each / **下载卡顿**：吊销检查不再于端点不可达时卡死 TLS 握手；资源目录单项候选限制 20 秒
- Mica backdrop restored on the main window (broken remote-session check), content-surface divider shadow removed, divider stroke lightened / 恢复主窗口云母背景（修复远程会话检测），移除内容面板分隔阴影，分隔线颜色减淡
- **[Packaging post–Phase 3]** Restore winget package identity `SSC-STUDIO.LenovoLegionToolkit` and historical scoop/winget installer URLs (`LenovoLegionToolkit_v*_Setup.exe`); fix Release `LEGACY_SETUP_ASSET`, `udt.dll` smoke check, and winget validate path; ship `llt.exe`/`llt.dll` as one-release shims of `udt` (`udt-cli` alias retained one more train); dual-stage plugin SDK/Shared under both UDT and legacy filenames / **[打包 Phase 3 后修复]** 恢复 winget 包 ID 与历史 scoop/winget 安装包 URL；修正 Release 兼容别名、CLI smoke 与 winget 校验路径；发布期提供 `llt`→`udt` 兼容拷贝（`udt-cli` 别名再保留一代）；插件 SDK/Shared 双文件名落盘
- Plugin Extensions skeleton re-entry, SoftFadeIn opacity, and CardAction radius consistency / 插件扩展骨架屏重入、SoftFadeIn 透明度与 CardAction 圆角一致性

## [5.0.0] - 2026-07-14

### Added
- Detailed dashboard sensors loading skeleton (title/model, gauge, metrics, trend, legend) with page-owned loading chrome.
- Plugin Extensions **opt-in** navigation (default off) plus persistent status notice; one-time settings migration from legacy default-on.
- Fan speed multi-source read coordinator and faster Lenovo fan WMI probe path.
- Theme style presets retint **AppSurfaceCard**, chart wells, and **notification glass** (Official Cool / Midnight / Forest).
- Shared loading chrome ownership attribute and skeleton shimmer coordinator/behavior.

### Fixed
- Plugin Extensions skeleton only on first visit; re-entry now shows 流光 again.
- Status banner `Closed` no longer fires on initial Collapsed (false “user dismissed”).
- WMI `Invalid object` / 无效的对象 no longer rethrown through Task.Run for soft probe retries.
- Navigation SoftFadeIn could leave LoadingChrome pages invisible (Opacity 0).
- Settings **CardAction** radius mismatched **CardControl** (now `CornerRadiusCard`).
- Official Cool sensors/toast surfaces stayed neutral grey while chrome was blue-tinted.

### Changed
- Version train **5.0.0** (Directory.Build.props). Cross-platform CLI assets enabled for major ≥ 5 in `Make.bat`.
- Skeleton shimmer contrast and sweep tuned; network acceleration diagnostics status chip compact layout.

### Changed (carried from pre-5.0 unreleased train)
- **README screenshots refreshed (2026-07-12)**: Updated `Assets/Screenshot_main.png` (1300×850 home, dark) and `Assets/Screenshot_zh-hans.png` (zh-Hans UI). Brand binaries unified under repo-root `Assets/`.
- **Docs sync (EN / zh-Hans)**: Aligned download notes (winget pending), language-pack privacy, feature glance table, network acceleration legacy note, documentation index, screenshots & troubleshooting sections, and Star History block in `README_zh-hans.md`.
- **Code comments**: PerformanceTest console/report strings and remaining C# `//` comments are English-only.

### Fixed
- **Log.Shutdown/Dispose SemaphoreSlim handle leak (BUG-2026-07-09-004)**: Centralized all teardown into a single DisposeCoreAsync() to guarantee _logger and _emergencyLock are disposed exactly once regardless of whether Shutdown, ShutdownAsync, or Dispose fires first. Added regression tests verifying Shutdown-then-Dispose and concurrent ShutdownAsync/Dispose races do not throw double-dispose exceptions.

### Added / 新增
- FlaUI + WinRT OCR automated UI verification pipeline (Pillar C) base class `FlaUiTestBase`, collection definition, setup tests, and main window tests for automated UI validation / FlaUI + WinRT OCR 自动化 UI 验证管道（Pillar C）——基类 `FlaUiTestBase`、集合定义、安装测试、主窗口测试，用于自动化 UI 验证
- Admin test runner script (`run_flaui_tests_admin.ps1`) for easy FlaUI test execution with auto-elevation / 管理员测试运行脚本（`run_flaui_tests_admin.ps1`），支持自动提权，简化 FlaUI 测试执行
- FlaUI testing guide (`Docs/FlaUI_Testing.md`) with prerequisites, quick start, troubleshooting, and writing new tests / FlaUI 测试指南（`Docs/FlaUI_Testing.md`），包含先决条件、快速开始、故障排除和编写新测试

### Fixed / 修复

- [Build][Pillar 0] BUG-2026-07-09-001: UniversalDeviceToolkit.sln normalized for the 7 x64-only project GUIDs (DC01FDB3,4B902DDC,CB52B339,AC885CE1,656AC74B,2C7AB13C,BB54FD85); Debug|Any CPU = Debug|x64 / Release|Any CPU = Release|x64 mappings present, duplicate Debug|x64/Release|x64 pairs removed. Eliminates 7x MSB4121 warnings and duplicate MSBuild task lines. Verified: dotnet build -c Debug -m:1 --no-incremental -> 0 warnings, 0 errors, no MSB4121.

- [Localization][Pillar B] ColorPickerControl Hex label now binds `{x:Static resources:Resource.Color_Hex}` instead of a hardcoded `"Hex"` string (`ColorPickerControl.xaml` L60), so the label is localized across all 78+ locales / [本地化][支柱 B] ColorPickerControl 十六进制标签现绑定 `{x:Static resources:Resource.Color_Hex}`，替代硬编码 `"Hex"`（`ColorPickerControl.xaml` 第 60 行），使该标签在 78+ 语言下本地化
- [Localization][Pillar B] ColorPickerControl OK button now binds `{x:Static resources:Resource.OK}` instead of a hardcoded `"OK"` string (`ColorPickerControl.xaml` L131), so the button is localized across all 78+ locales / [本地化][支柱 B] ColorPickerControl 确定按钮现绑定 `{x:Static resources:Resource.OK}`，替代硬编码 `"OK"`（`ColorPickerControl.xaml` 第 131 行），使该按钮在 78+ 语言下本地化
- [UDT-007][Threading & WMI] WMI extension default timeouts were actually lowered `5000ms` to `2500ms` in both `ManagementObjectSearcherExtensions` and `ManagementEventWatcherExtensions` (the phantom UDT-001 archive `**Fixed**` claim had never landed in code, verified via `git show HEAD`) and a 3-space to 4-space indentation regression on the four declaration lines was corrected / [UDT-007][线程与 WMI] 两处 WMI 扩展的默认超时真正从 `5000ms` 下调至 `2500ms`（此前的 UDT-001 归档 `**Fixed**` 声明从未真正落地于代码，`git show HEAD` 核实），并修正了四处方法声明从 3 空格到 4 空格的缩进回退
- [UDT-008][UI & Localization] 3 GPU status-dot `Ellipse.Fill` literals in `StatusWindow.xaml` were hardcoded raw hex (`#FF8BC34A` / `#F2A541` / `#BF360C`); now bound to `{DynamicResource StatusSuccessBrush}` / `{DynamicResource StatusWarningBrush}` / `{DynamicResource StatusCriticalBrush}` so they follow light/dark theme swaps and align with the shared chart-keyed status-color vocabulary used across all dashboard indicators / [UDT-008][UI 与本地化] `StatusWindow.xaml` 中 3 个 GPU 状态圆点 `Ellipse.Fill` 原为硬编码十六进制颜色（`#FF8BC34A` / `#F2A541` / `#BF360C`）；现绑定 `{DynamicResource StatusSuccessBrush}` / `{DynamicResource StatusWarningBrush}` / `{DynamicResource StatusCriticalBrush}`，随浅/深色主题切换并与仪表盘共享状态色词汇对齐
- [Test Reliability] `SensorReadTimeoutSeconds` virtual seam added to `AbstractSensorsController` so the flaky detailed-sensor-read test can override the 2s production cap to 60s via a mock (`SensorReadTimeoutSeconds => 60`), restoring deterministic test timing without weakening the production timeout / [测试可靠性] 在 `AbstractSensorsController` 增加 `SensorReadTimeoutSeconds` 虚拟接缝，使易抖动的详细传感器读取测试可通过 mock 将生产 2s 上限覆盖为 60s，恢复确定性时序且不削弱生产超时
- [Brand Migration] Uzbek (`uz`) resource file user-facing brand string in `ExcludeRefreshRatesWindow_NoRefreshRatesFound_Message` (`Resource.uz.resx` L476) now reads `Universal Device Toolkit` instead of the legacy `Lenovo Legion Toolkit`, so the no-refresh-rate-found message uses the current product name in Uzbek / [品牌迁移] 乌兹别克语（`uz`）资源中 `ExcludeRefreshRatesWindow_NoRefreshRatesFound_Message` 的用户可见品牌字符串现为 `Universal Device Toolkit`，不再使用旧称 `Lenovo Legion Toolkit`
- [Brand Migration] PerformanceTest console boot banner and report title now display `Universal Device Toolkit` instead of the legacy `Lenovo Legion Toolkit` (`Program.cs` L21 boot banner, L339 report title) / [品牌迁移] PerformanceTest 控制台启动横幅与报告标题现显示 `Universal Device Toolkit`，不再使用旧称
- [Pre-existing Fix] `UniversalDeviceToolkit.PerformanceTest.csproj` XML declaration had a stray `"` between `?` and `>` (`<?xml ... ?>"?>`), making MSBuild unable to parse the project; byte-level corrected to `<?xml version="1.0" encoding="utf-8"?>` (this project was never in the main solution `UniversalDeviceToolkit.sln` and had never compiled before) / [预存修复] `UniversalDeviceToolkit.PerformanceTest.csproj` XML 声明在 `?` 与 `>` 之间多了一个 `"`（`<?xml ... ?>"?>`），导致 MSBuild 无法解析项目；已按字节级修正为 `<?xml version="1.0" encoding="utf-8"?>`（该项目从未包含在主解决方案 `UniversalDeviceToolkit.sln` 中，此前从未编译）
- [Pre-existing Fix] 9 corrupted Chinese string literals in `UniversalDeviceToolkit.PerformanceTest/Program.cs` whose last Chinese glyph + closing `"` had been replaced by `U+FFFD` + `?` (causing CS1010 newline-in-constant errors when the project was first compiled) reconstructed from context and restored: `L33 "`, `L35 "`, `L38 "`, `L45 "`, `L145 ""`, `L227 "`, `L240 "`, `L314 "`, `L357 "` / [预存修复] `UniversalDeviceToolkit.PerformanceTest/Program.cs` 中 9 处中文字符串字面量最后一个中文字与闭合引号 `"` 被替换为 `U+FFFD` + `?`（导致项目首次编译时抛出 CS1010 常量中有换行符），已根据上下文重建恢复
- [UDT-006][Runtime Stability] `MacroController.Stop()` no longer orphans the global `WH_KEYBOARD_LL` macro hook or fires macros twice: the hook handle is now cleared first and `UnhookWindowsHookEx` runs FIRST, with recorder/player teardown isolated in separate traced `try/catch` blocks (was a silent comment-only `catch { }` whose `finally` still zeroed the field, so a teardown throw skipped the unhook and the next `Start()` re-installed a second hook) / [UDT-006][运行时稳定性] `MacroController.Stop()` 不再遗弃全局 `WH_KEYBOARD_LL` 宏钩子或导致宏触发两次：钩子句柄先清空且 `UnhookWindowsHookEx` 优先执行，录制/播放清理分在可追踪的 `try/catch` 中隔离
- [UDT-005][Test Reliability] FlaUI main-window UI tests no longer hard-fail when the runner lacks an admin/desktop session: `FlaUIMainWindowTests` methods switched from `[Fact]` to `[SkippableFact]` (with `MainWindow!` null-forgiving on the two deref sites), so an unavailable elevation now yields a clean skip instead of a `SkipException` failure (previously 3 failures) / [UDT-005][测试可靠性] 受管理员权限/桌面会话限制时，FlaUI 主窗口测试不再硬失败：`FlaUIMainWindowTests` 方法由 `[Fact]` 改为 `[SkippableFact]`（两处解引用使用 `MainWindow!` 空抑制），缺少提权时现在为干净跳过而非 `SkipException` 失败（此前 3 个失败）
- [Brand Migration] FPS self-monitoring blacklist (`FpsSensorController`) now also excludes the renamed `Universal Device Toolkit` process alongside the legacy `Lenovo Legion Toolkit`, so the app own window is no longer counted as a monitored foreground app / [品牌迁移] FPS 自监控黑名单现同时排除更名后的 `Universal Device Toolkit` 与旧称进程，应用自身窗口不再被计入前台监控
- [Brand Migration] HWiNFO custom sensor group now registers under `Universal Device Toolkit`; on stop the legacy `Lenovo Legion Toolkit` registry group is cleaned too, so pre-rename sensors do not linger in HWiNFO / [品牌迁移] HWiNFO 自定义传感器组现以 `Universal Device Toolkit` 注册；停止时亦清理旧称注册表组，避免更名前传感器残留
- [Brand Migration] Plugin Workbench host resource resolver now matches both `Universal Device Toolkit` and `Lenovo Legion Toolkit` host assemblies (was legacy-only), restoring resource lookup under the renamed host / [品牌迁移] 插件工作台宿主资源解析器现同时匹配新旧宿主程序集名称，恢复更名后的资源查找
- [Brand Migration] Audit confirms the user-facing brand is fully migrated to `Universal Device Toolkit`; `UniversalDeviceToolkit.*` assembly/namespace identifiers are intentionally retained as cross-repository ABI contracts on which plugin loading depends / [品牌迁移] 审计确认用户可见品牌已全面迁移至 `Universal Device Toolkit`；`UniversalDeviceToolkit.*` 程序集/命名空间标识刻意保留为跨仓库 ABI 契约（插件加载依赖）
- [UDT-001][Threading & WMI] WMI helper extension default timeouts in `ManagementObjectSearcherExtensions` and `ManagementEventWatcherExtensions` lowered from `5000ms` to `2500ms` to respect the WMI timeout ceiling; default callers rely on a linked `CancellationToken` / [UDT-001][线程与 WMI] `ManagementObjectSearcherExtensions` 与 `ManagementEventWatcherExtensions` 的 WMI 助手默认超时由 `5000ms` 下调至 `2500ms`，遵守 WMI 超时上限；默认调用方使用联动的 `CancellationToken`
- [UDT-002][Threading & WMI] WMI method-invoke bound in `WMI.CallInternalAsync` lowered from a hardcoded `10000ms` to a `2500ms` constant (`WmiInvokeTimeoutMs`); a hung WMI method now fails fast instead of stalling the caller 10s / [UDT-002][线程与 WMI] `WMI.CallInternalAsync` 中的方法调用超时由硬编码 `10000ms` 降为 `2500ms` 常量（`WmiInvokeTimeoutMs`）；挂起的 WMI 方法会快速失败而不再阻塞调用方 10 秒
- [UDT-003][Threading & WMI] `AmdOverclockingController.FetchCommands` no longer runs synchronous `ManagementObject` construction and `WMI.InvokeMethodAndGetValue` on the calling thread; converted to `FetchCommandsAsync` wrapped in `Task.Run(...).WaitAsync(2500ms)` with `.ConfigureAwait(false)` so AMD overclocking no longer stalls the UI 5-10s / [UDT-003][线程与 WMI] `AmdOverclockingController.FetchCommands` 不再在调用线程同步执行 `ManagementObject` 构造与 `WMI.InvokeMethodAndGetValue`；已改为 `FetchCommandsAsync`，通过 `Task.Run(...).WaitAsync(2500ms)` 与 `.ConfigureAwait(false)` 包装，避免 AMD 超频功能阻塞 UI 5–10 秒
- [UDT-004][Runtime Stability & Telemetry] Empty `catch { }` in the `Registry` listener teardown at `Registry.cs` no longer silently swallows exceptions; it now traces via `Log.Instance.Trace(...)` matching the surrounding listener loop so cancelled/stuck listener tasks stay observable / [UDT-004][运行时稳定性与可观测性] `Registry.cs` 注册表监听器清理路径中的空 `catch { }` 不再静默吞掉异常；现已改为通过 `Log.Instance.Trace(...)` 打点（与周围监听循环一致），使被取消/卡住的监听任务可被观测
- [Threading] `SmartFnLockController` no longer floods the thread pool with a new fire-and-forget polling task on every enable cycle; polling is coalesced onto a single guarded long-running task / [线程] `SmartFnLockController` 不再每次启用都新建无等待轮询任务导致线程池洪水；轮询已合并到单个受保护的长周期任务上
- [Threading] `MacroController` global `WH_KEYBOARD_LL` macro-recording hook no longer drops keystrokes or leaks the hook; the hook thread now runs a Windows message pump so events keep being delivered and the hook is reliably unhooked on stop / [线程] `MacroController` 全局 `WH_KEYBOARD_LL` 宏录制钩子不再丢键或泄漏钩子；钩子线程现在运行 Windows 消息泵，确保事件持续派发，并在停止时可靠卸载钩子
- Dashboard layout customizations (groups, items order, sensors toggle) are now persisted across app restarts; `DashboardSettings` IoC registration changed from transient to `SingleInstance()` so EditDashboardWindow and DashboardPage share the same in-memory cache and `SynchronizeStore()` writes are immediately visible to all consumers / 控制台自定义布局（分组、项目顺序、传感器开关）现在可在应用重启后持久化保留；`DashboardSettings` 的 IoC 注册从瞬时改为 `SingleInstance()`，确保 EditDashboardWindow 与 DashboardPage 共享同一内存缓存，`SynchronizeStore()` 写入对所有消费者立即可见
- Update flow no longer falls back to GitHub because the installer file-name pattern required a hyphen that the actual installer never has; relaxed pattern matches `UniversalDeviceToolkitSetup*.exe` so in-app updates run again / 更新流程不再因安装包文件名匹配规则错误（要求连字符）而回退到 GitHub；现已改为匹配实际的 `UniversalDeviceToolkitSetup*.exe` 命名，恢复应用内自动更新
- Clicking external links (release page, device info link, etc.) no longer fails with `Win32Exception`; URL opening keeps `UseShellExecute=true` (required for protocol handlers) behind a strict HTTP/HTTPS scheme allow-list / 打开外部链接（发行页、设备信息链接等）不再因 `Win32Exception` 失败；URL 开启保留 `UseShellExecute=true`（协议处理器需要），并由严格的 HTTP/HTTPS 协议白名单保护
- Dashboard sensors card no longer shows overlapping shimmer and live gauge animations; removed the duplicate page-level sensor skeleton and keep only the layout-matched `SensorsControl` overlay, which now tracks compact/wide layout on resize / 控制台传感器卡片不再叠加两套 shimmer 与仪表动画；移除仪表盘页重复的传感器骨架层，仅保留与布局贴合的 `SensorsControl` 覆盖层，并在窗口缩放时同步紧凑/宽屏布局
- About Device window cards no longer show empty white blocks on the left; row content is placed in `CardControl.Header` per WPF-UI template / 关于设备窗口卡片左侧不再出现空白色块；行内容已按 WPF-UI 模板放入 `CardControl.Header`
- Windows Optimization checkboxes now surface apply failures (registry access denied, command exit codes, verification mismatch) via snackbar instead of silently reverting; unchecking uses rollback where available / Windows 优化勾选项在应用失败（注册表拒绝访问、命令非零退出、验证不匹配）时会通过 Snackbar 提示，不再静默回退；取消勾选时在支持的情况下执行回滚
- Windows Optimization Shell Integration category no longer shows raw resource keys (`WindowsOptimization_Category_NilesoftShell_*`); host resources now supply localized titles/descriptions and plugin lookups fall back to them / Windows 优化页 Shell Integration 分类不再显示未翻译的资源键（`WindowsOptimization_Category_NilesoftShell_*`），宿主资源已补全中英文文案并在插件资源缺失时回退读取
- Settings page no longer crashes on open when `ColorPicker.Models.dll` was missing from Release output; the dependency is now referenced explicitly / 修复设置页因 Release 输出缺少 `ColorPicker.Models.dll` 而无法打开的问题，现已显式引用该依赖
- `UniversalDeviceToolkit.WPF/Resources/Resource.sq.resx` now uses the same `at {0} ({1})` placeholder shape for `AutomationPipelineControl_SubtitlePart_AtTime` as the English resource (`në {0} ({1})`), so the automation-pipeline subtitle renders the time and timezone correctly instead of an arbitrary `HH:mm` colon-joined string / `UniversalDeviceToolkit.WPF/Resources/Resource.sq.resx` 中 `AutomationPipelineControl_SubtitlePart_AtTime` 的占位符格式已与英文资源 `at {0} ({1})` 对齐为 `në {0} ({1})`，自动化流水线副标题现在可正确显示时间与时区，而不是错误的 `HH:mm` 冒号拼接
- Backfilled simplified Chinese (`zh-Hans`) UI strings for Plugin Extensions, Windows Optimization, sensors, and large-file cleanup by syncing from `zh-Hant` and manually translating newly added keys, so Chinese UI no longer mixes English labels like `Available`, `Configure`, or `Preparing download...` / 从 `zh-Hant` 同步并补全新增键的简体中文翻译，修复插件扩展、Windows 优化、传感器与大文件清理等界面中英混排（如 `Available`、`Configure`、`Preparing download...`）
- Plugin Extensions install/configure/open/uninstall icon buttons now render explicit symbols and stay vertically centered in each card in Dark/Light themes / 插件扩展页安装/配置/打开/卸载图标按钮在深浅色主题下正确显示符号并在卡片内垂直居中
- Packages page readme/download/cancel action buttons no longer render as empty boxes in light mode; icons use explicit `SymbolIcon` foreground like Plugin Extensions / 驱动包列表页 readme/下载/取消操作按钮在浅色模式下不再显示为空白方框，图标改用与插件扩展页一致的显式 `SymbolIcon` 前景色
- Macro event card titles for mouse actions (MOVE, WHEEL DOWN/UP/LEFT/RIGHT, XBUTTON, LBUTTON, RBUTTON, MBUTTON) are now localized via `Lib.Macro.Resources.Resource` instead of hardcoded English strings / 宏事件卡片中鼠标操作的标题（MOVE、WHEEL DOWN/UP/LEFT/RIGHT、XBUTTON、LBUTTON、RBUTTON、MBUTTON）现通过 `Lib.Macro.Resources.Resource` 本地化，不再硬编码英文
- Fixed 7 pre-existing test failures (source-code structure tests outdated after refactors, WMI timeout in VantagePackageDownloaderTests); all 2343 tests now pass / 修复 7 个预存的测试失败（源代码结构测试在重构后未同步更新、VantagePackageDownloaderTests 中 WMI 超时），现在 2343 个测试全部通过
- Language selector and main window no longer pop up simultaneously on first launch; `LocalizationHelper.cs` changed `window.Show()` to `window.ShowDialog()` so language selection blocks until complete / 首次启动时语言选择窗口与主窗口不再同时弹出；`LocalizationHelper.cs` 将 `window.Show()` 改为 `window.ShowDialog()`，语言选择完成前阻塞后续流程
- Card subtitle text (Chinese localization) no longer overflows and bloats card height; `CardHeaderControl` now enforces `MaxHeight=60` (≈3 lines) on subtitle `TextBlock`, and 25+ overly long Chinese `Message`-suffixed resource strings in `Resource.zh-hans.resx` were shortened to ≤45 chars per line with intentional `\n` breaks / 卡片副标题文本（中文本地化）不再溢出并撑高卡片；`CardHeaderControl` 现在对副标题 `TextBlock` 强制 `MaxHeight=60`（约 3 行），`Resource.zh-hans.resx` 中 25+ 条过长的 `Message` 后缀中文资源字符串已缩短至每行 ≤45 字符并添加必要的 `\n` 换行
- System-wide stutter after closing the app is fixed; `NativeWindowsMessageListener` (global `WH_KEYBOARD_LL` hook) and `UserInactivityAutoListener` (global `WH_KEYBOARD_LL` + `WH_MOUSE_LL` hooks) were not stopped on app exit, causing Windows to keep calling into the dead process — `App.xaml.cs` `PerformShutdownAsync()` now properly stops both listeners, and `AppDomain.ProcessExit` handler added as defense-in-depth / 修复关闭应用后系统全局卡顿的问题；`NativeWindowsMessageListener`（全局 `WH_KEYBOARD_LL` 钩子）和 `UserInactivityAutoListener`（全局 `WH_KEYBOARD_LL` + `WH_MOUSE_LL` 钩子）在应用退出时未被停止，导致 Windows 持续向已死进程发起钩子回调——`App.xaml.cs` 的 `PerformShutdownAsync()` 现已正确停止这两个监听器，并新增 `AppDomain.ProcessExit` 处理程序作为深度防御

### Improved / 改进

- Automation page Quick Actions section title now uses the standard section typography tier so it reads smaller than the page title / 自动化页「快捷操作」区块标题改用标准区块字号，层级低于页面主标题
- Larger default body/caption typography with gentler high-DPI compensation; title-bar device model text is easier to read / 提升默认正文字号并减轻高 DPI 缩小，标题栏机型文字更易读
- About Device window uses compact grouped rows (label + value per line) instead of one card per field / 关于设备窗口改为分组紧凑行布局，不再为每个字段单独占一张卡片
- About Device window is wider with reduced side margins so long hardware values (e.g. GPU names) have more room / 关于设备窗口加宽并减小左右边距，长硬件信息（如 GPU 名称）有更多显示空间
- Theme is applied as soon as settings load so light/dark mode no longer flashes the App.xaml dark defaults on startup / 设置加载后立即应用主题，避免启动时先闪深色默认样式
- Unified Fluent shell polish: larger card/button corner radii, seamless light-mode navigation/content surfaces, skeleton loading on Packages and Keyboard Backlight pages, smoother page transitions and button press feedback / 统一 Fluent 主壳高级感：加大卡片与按钮圆角、修复浅色模式导航与内容区色差、为驱动包与键盘背光页增加骨架屏加载，并改进页面切换与按钮按压反馈
- README and promotion docs now lead with taglines, UDT-vs-Vantage comparison, audience positioning, and expanded EN/ZH social/release copy templates / README 与宣发文档新增标语、UDT 对比 Vantage 表格、受众定位，并扩充中英文发布与社交文案模板
- Promotion copy rewritten in conversational tone for forums and social posts (PROMOTION_*, COMMUNITY_OUTREACH) / 宣发文案改为口语化写法，适合论坛与社区真人发帖
- Simplified Plugin Extensions card visuals: removed heavy selection/install chrome, toolbar boxes, and left accent bar; install progress is a bottom accent line plus spinner / 简化插件扩展卡片视觉：移除选中/安装粗边框、工具条空框与左侧色条，安装进度改为底部细线加旋转指示
- Dashboard `SensorsControl` now shows a content-shaped skeleton overlay with shimmer animation while waiting for the first sensor reading; each of the CPU/Battery/GPU sections renders a 3-row gauge+bar+chart+legend placeholder that matches the real layout, then fades out via a 0.45s `EasingCubicOut` storyboard when data is ready (or whenever initial load is restarted) / 控制台 `SensorsControl` 现在在等待首次传感器数据时显示与内容形状一致的骨架屏覆盖层（带 shimmer 动画）；CPU/Battery/GPU 三段各自渲染与实际布局一致的 3 行（仪表+进度条+图表+图例）占位符，当数据就绪（或重新触发首次加载）时通过 0.45s `EasingCubicOut` Storyboard 淡出
- Replaced `CompositionTarget.Rendering` in `RadialGaugeControl` with a `DispatcherTimer` at ~60fps to reduce global render-event pressure when multiple gauges animate simultaneously; timer is cleaned up on `Unloaded` / 将 `RadialGaugeControl` 中的 `CompositionTarget.Rendering` 替换为 `DispatcherTimer`（~60fps），降低多仪表同时动画时的全局渲染事件压力；`Unloaded` 时清理定时器
- `TrendChartControl` and `TrendSeries` now cache `LinearGradientBrush` and `Pen` objects per series color instead of recreating them on every render pass / `TrendChartControl` 和 `TrendSeries` 现在按系列颜色缓存 `LinearGradientBrush` 和 `Pen` 对象，不再每次渲染都重新创建
- `SensorsControl` now caches `FindName()` results in a `Dictionary<string, FrameworkElement?>` to avoid repeated XAML name-scope tree walks in hot update paths / `SensorsControl` 现在将 `FindName()` 结果缓存到 `Dictionary<string, FrameworkElement?>` 中，避免在热更新路径中反复遍历 XAML 名称作用域树
- `DiscreteGPUControl` and `AbstractRefreshingControl` now unsubscribe from singleton events in `Unloaded` handlers to prevent memory leaks when controls are created and destroyed by page navigation / `DiscreteGPUControl` 和 `AbstractRefreshingControl` 现在在 `Unloaded` 处理程序中取消对单例事件的订阅，防止页面导航创建/销毁控件时的内存泄漏
- `FanCurveControl.DrawGraph` now caches the `Path` and `Polygon` canvas children and updates their `Data`/`Points` in-place instead of clearing and rebuilding on every slider change; `VerifyValues` uses direct `for` loops instead of LINQ `Take`/`Skip` to avoid enumerator allocation / `FanCurveControl.DrawGraph` 现在缓存画布子元素 `Path` 和 `Polygon`，每次滑块变化时就地更新 `Data`/`Points` 而非清空重建；`VerifyValues` 使用直接 `for` 循环替代 LINQ `Take`/`Skip`，避免枚举器分配
- `AutomationPage.GetSupportedAutomationStepsAsync` now runs all 30+ `IsSupportedAsync` calls in parallel via `Task.WhenAll` instead of sequentially, reducing page initialization time / `AutomationPage.GetSupportedAutomationStepsAsync` 现在通过 `Task.WhenAll` 并行运行 30+ 个 `IsSupportedAsync` 调用，替代原来的串行执行，减少页面初始化时间
- `ColorPickerControl` now reuses a single `SolidColorBrush` instance (updating `.Color`) instead of creating a new brush on every color change, reducing GC pressure during continuous drag / `ColorPickerControl` 现在复用单个 `SolidColorBrush` 实例（更新 `.Color`），不再每次颜色变化都创建新画刷，减少连续拖动时的 GC 压力
- Memory leak fixes: `AbstractToggleFeatureCardControl` and `AbstractComboBoxFeatureCardControl` now unsubscribe from `MessagingCenter` in `Unloaded`; 12 Dashboard controls (`WhiteKeyboardBacklightControl`, `PowerModeControl`, `OverclockDiscreteGPUControl`, `DpiScaleControl`, `RefreshRateControl`, `ResolutionControl`, `HDRControl`, `FnLockControl`, `MicrophoneControl`, `PanelLogoBacklightControl`, `PortsBacklightControl`, `TouchpadLockControl`, `WinKeyControl`) now unsubscribe from `_listener.Changed` in `Unloaded` / 内存泄漏修复：`AbstractToggleFeatureCardControl` 和 `AbstractComboBoxFeatureCardControl` 在 `Unloaded` 时取消 `MessagingCenter` 订阅；12 个仪表盘控件在 `Unloaded` 时取消 `_listener.Changed` 订阅
- `NativeLayeredWindow.UpdateLayeredWindow` now disposes the GDI `Bitmap` via `using` to prevent unmanaged handle leak / `NativeLayeredWindow.UpdateLayeredWindow` 现在通过 `using` 释放 GDI `Bitmap`，防止非托管句柄泄漏
- `RGBKeyboardBacklightControl` now unsubscribes from `MessagingCenter` and event handlers in `Unloaded` to prevent memory leaks / `RGBKeyboardBacklightControl` 现在在 `Unloaded` 时取消 `MessagingCenter` 和事件处理程序的订阅，防止内存泄漏
- `Application_Exit` now uses `async void` with `await` instead of blocking `GetAwaiter().GetResult()` to prevent UI thread deadlock during shutdown; `Dispatcher.Invoke` replaced with `BeginInvoke` / `Application_Exit` 现在使用 `async void` + `await` 替代阻塞式的 `GetAwaiter().GetResult()`，防止关闭时 UI 线程死锁；`Dispatcher.Invoke` 替换为 `BeginInvoke`
- 27 `Process.Start` calls across the codebase now wrap the returned `Process` in `using` to prevent native handle leaks / 代码库中 27 个 `Process.Start` 调用现在将返回的 `Process` 包装在 `using` 中，防止非托管句柄泄漏
- `LampArrayController` now properly disposes `_renderCts` and `_screenCaptureCts` via `Dispose(bool)` pattern to prevent CancellationTokenSource memory leaks / `LampArrayController` 现在通过 `Dispose(bool)` 模式正确释放 `_renderCts` 和 `_screenCaptureCts`，防止 CancellationTokenSource 内存泄漏
- All 16 bare `SemaphoreSlim.WaitAsync()` calls now pass a `CancellationToken` to prevent indefinite blocking / 所有 16 个裸 `SemaphoreSlim.WaitAsync()` 调用现在传递 `CancellationToken`，防止无限期阻塞
- Added `volatile` to double-checked locking backing fields in `PluginHostContext._resolved` and `HttpClientManager._sharedClient` to prevent torn reads on 32-bit targets / 为 `PluginHostContext._resolved` 和 `HttpClientManager._sharedClient` 的双重检查锁定后备字段添加 `volatile`，防止 32 位目标上的读取撕裂
- 35 empty `catch` blocks across the codebase now log exceptions at appropriate levels instead of silently swallowing them / 代码库中 35 个空 `catch` 块现在以适当级别记录异常，不再静默吞掉
- `GodModeSettingsWindow.SetDefaults` converted from `async void` to `async Task` with proper try/catch to prevent unobserved exceptions / `GodModeSettingsWindow.SetDefaults` 从 `async void` 转换为 `async Task` 并添加适当的 try/catch，防止未观察异常
- 8 singleton event subscriptions in controls/pages now unsubscribe in `Unloaded` handlers to prevent memory leaks when controls are navigated away: `PluginExtensionsPage`, `SettingsAppearanceControl`, `ResolutionAutomationStepControl`, `RefreshRateAutomationStepControl`, `HDRAutomationStepControl`, `DpiScaleAutomationStepControl`, `DeviceAutomationPipelineTriggerTabItemContent`, `MacroSequenceControl` / 8 个控件/页面中的单例事件订阅现在在 `Unloaded` 处理程序中取消订阅，防止导航离开时内存泄漏：`PluginExtensionsPage`、`SettingsAppearanceControl`、`ResolutionAutomationStepControl`、`RefreshRateAutomationStepControl`、`HDRAutomationStepControl`、`DpiScaleAutomationStepControl`、`DeviceAutomationPipelineTriggerTabItemContent`、`MacroSequenceControl`
- Hot-path string allocations reduced in `OsdWindowBase` (sensor tick loop, FPS callback) and `SensorsControl` (gauge updates, trend samples) by caching resource suffixes in static readonly fields and replacing interpolated strings with `string.Concat` / 减少 `OsdWindowBase`（传感器刷新循环、FPS 回调）和 `SensorsControl`（仪表更新、趋势采样）中的热路径字符串分配，将资源后缀缓存到静态只读字段并用 `string.Concat` 替代内插字符串
- `EnumToBoolConverter` now caches `Enum.Parse` results in a `ConcurrentDictionary` to avoid per-call allocations; `CollectionSplitConverters` replaced LINQ `Cast().ToList()` with manual enumeration and `Array.Empty<object>()` / `EnumToBoolConverter` 现在将 `Enum.Parse` 结果缓存到 `ConcurrentDictionary` 中，避免每次调用分配；`CollectionSplitConverters` 用手动枚举和 `Array.Empty<object>()` 替代 LINQ `Cast().ToList()`
- `SpectrumKeyboardBacklightControl`, `PowerModeControl`, `SensorsControl`, `PackageControl` now implement `IDisposable` and dispose their `CancellationTokenSource`, `ThrottleLastDispatcher`, and `Process` fields / `SpectrumKeyboardBacklightControl`、`PowerModeControl`、`SensorsControl`、`PackageControl` 现在实现 `IDisposable` 并释放其 `CancellationTokenSource`、`ThrottleLastDispatcher` 和 `Process` 字段
- Dynamic Panel children in `KeyboardBacklightPage`, `OsdSettingsWindow`, `SensorsControl`, `AutomationPipelineControl` now properly cleaned up in `Unloaded` handlers / `KeyboardBacklightPage`、`OsdSettingsWindow`、`SensorsControl`、`AutomationPipelineControl` 中的动态 Panel 子元素现在在 `Unloaded` 处理程序中正确清理
- 12 `ItemsControl`/`ListView`/`ListBox` elements across the app now have `VirtualizingStackPanel.IsVirtualizing="True"` and `VirtualizationMode="Recycling"` enabled for better scroll performance with large lists / 应用中 12 个 `ItemsControl`/`ListView`/`ListBox` 元素现在启用 `VirtualizingStackPanel.IsVirtualizing="True"` 和 `VirtualizationMode="Recycling"`，提升大列表滚动性能
- Added `.ConfigureAwait(false)` to all `await` calls across Lib (12), WPF Controls (200+), Pages (75), Windows (120), Utils/Extensions/ViewModels (64), CLI/Lib.Automation/Lib.Macro (0 already compliant) to reduce thread marshalling overhead / 为所有 `await` 调用添加 `.ConfigureAwait(false)`（Lib 12 处、WPF Controls 200+ 处、Pages 75 处、Windows 120 处、Utils/Extensions/ViewModels 64 处），减少线程编排开销
- Fixed 25 culture-sensitive string operations across Lib, WPF, and Plugins: added `StringComparison.Ordinal` to `StartsWith`/`EndsWith`/`Contains`/`IndexOf`, changed `ToUpper()`/`ToLower()` to invariant variants to prevent locale-dependent behavior / 修复跨 Lib、WPF 和 Plugins 的 25 处文化敏感字符串操作：为 `StartsWith`/`EndsWith`/`Contains`/`IndexOf` 添加 `StringComparison.Ordinal`，将 `ToUpper()`/`ToLower()` 改为不变量变体，防止区域设置依赖行为
- Changed 25 `DateTime.Now` to `DateTime.UtcNow` in logging, timestamps, file names, duration calculations, and event metadata across Lib.Plugins, OsdWindowBase, and Tools; kept `DateTime.Now` only for UI display and local-time scheduling logic / 将 Lib.Plugins、OsdWindowBase 和 Tools 中的日志、时间戳、文件名、持续时间计算和事件元数据中的 25 处 `DateTime.Now` 改为 `DateTime.UtcNow`；仅在 UI 显示和本地时间调度逻辑中保留 `DateTime.Now`
- `OsdWindowBase`: added `volatile` to `_fpsMonitoringStarted`; brush fields captured in locals before use in `OnFpsDataUpdated` to avoid disposed-object reads from background thread / `OsdWindowBase`：为 `_fpsMonitoringStarted` 添加 `volatile`；在 `OnFpsDataUpdated` 中将画刷字段捕获到局部变量，避免后台线程读取已释放对象
- `UpdateWindow` now disposes `CancellationTokenSource` after cancellation and on `Closing` to prevent CTS memory leak / `UpdateWindow` 在取消后和 `Closing` 时释放 `CancellationTokenSource`，防止 CTS 内存泄漏
- `SensorsControl.FormatCpuPowerBreakdown` now reuses a static `List<string>` via `Clear()` instead of allocating a new list on every call / `SensorsControl.FormatCpuPowerBreakdown` 改为复用静态 `List<string>`，通过 `Clear()` 避免每次调用分配新列表
- `IFeature<T>` interface and all abstract base classes (`AbstractWmiFeature`, `AbstractCapabilityFeature`, `AbstractDriverFeature`, `AbstractUEFIFeature`, `AbstractLenovoLightingFeature`, `AbstractCompositeFeature`) now accept a `CancellationToken` on every async method, and a new `InvalidateResolution()` method lets callers force composite features to re-resolve their delegate implementation on demand / `IFeature<T>` 接口及所有抽象基类（`AbstractWmiFeature`、`AbstractCapabilityFeature`、`AbstractDriverFeature`、`AbstractUEFIFeature`、`AbstractLenovoLightingFeature`、`AbstractCompositeFeature`）的异步方法现在接受 `CancellationToken`，新增的 `InvalidateResolution()` 方法允许调用方按需强制复合特性重新解析其委托实现
- Plugin lifecycle is now driven by an explicit `PluginLifecycleStateMachine`; the legal transitions between `NotInstalled` `Installed` `Enabled`/`Disabled` are enforced centrally and illegal attempts (e.g. `NotInstalled` `Enabled`, or `Error` `Enabled`) are logged and rejected. A richer `LifecycleStateChanged` event exposing old/new `PluginState` is now available alongside the existing boolean `PluginStateChanged` event / 插件生命周期现在由显式的 `PluginLifecycleStateMachine` 状态机驱动，`NotInstalled` → `Installed` → `Enabled`/`Disabled` 之间的合法转换被集中强制执行，非法尝试（如 `NotInstalled` → `Enabled`，或 `Error` → `Enabled`）会被记录并拒绝。除原有布尔型 `PluginStateChanged` 事件外，还提供暴露旧/新 `PluginState` 的 `LifecycleStateChanged` 事件
- `SDK_BOUNDARY.md` now documents which types in `UniversalDeviceToolkit.Lib.Plugins` are part of the public plugin SDK and which are host-internal; plugins must not reference host-internal types at compile time / `SDK_BOUNDARY.md` 现已明确记录 `UniversalDeviceToolkit.Lib.Plugins` 中哪些类型属于公开插件 SDK，哪些是宿主机内部类型；插件在编译时不得引用宿主机内部类型

### Fixed / 修复

- Fixed Plugin Extensions action buttons rendering as empty squares when only icons were shown; icons now use the WPF-UI `Icon` property with proper sizing / 修复插件扩展操作按钮在仅显示图标时渲染为空白方框的问题，改用 WPF-UI `Icon` 属性并调整尺寸
- `MessagingCenter.Publish<T>` no longer lets one bad subscriber's exception prevent other subscribers from receiving the message; failures are now logged at trace level / `MessagingCenter.Publish<T>` 不再因单个订阅者抛出异常而阻断其他订阅者，失败时改为以 trace 级别记录日志
- `ExternalDetectionRule` now validates the download host against a Lenovo allowlist, uses a safer command splitter, and refuses to launch missing executables so the external detection path can no longer pull from arbitrary hosts or run absent binaries / `ExternalDetectionRule` 现对下载主机名做白名单校验、改用更安全的命令切分方式，并在可执行文件缺失时拒绝执行，避免外部检测流程从任意来源下载或运行不存在的二进制

### Security / 安全

- IPC named pipe now requires per-message HMAC-SHA256 authentication via challenge-response handshake, preventing unauthorized same-user processes from sending commands / IPC 命名管道现在要求通过质询-响应握手进行每消息 HMAC-SHA256 身份验证，防止未经授权的同用户进程发送命令
- Plugin EXE entry points now require Authenticode signature validation before launch (debug builds allow unsigned override) / 插件 EXE 入口点现在需要经过 Authenticode 签名验证后才能启动（调试版本允许未签名覆盖）
- Added trusted repository owner allowlist to UpdateChecker; custom repo owners from settings are ignored in release builds / UpdateChecker 添加了受信任仓库所有者白名单；发布版本忽略设置中的自定义仓库
- SHA256 integrity check is now enforced for update packages; validation is no longer skipped when hash is missing / 更新包现在强制进行 SHA256 完整性校验，哈希缺失时不再跳过验证
- Plugin store cache (`plugin-store-cache.json`) now uses HMAC-SHA256 integrity verification to prevent tampering / 插件商店缓存现在使用 HMAC-SHA256 完整性验证防止篡改
- Trusted plugin package store (`trusted-plugin-packages.json`) now uses DPAPI encryption + HMAC integrity / 受信任插件包存储现在使用 DPAPI 加密 + HMAC 完整性保护
- TrustedPluginPackageStore HMAC key derivation strengthened: uses Windows SID binary (not MachineName string) with 16-byte random salt and PBKDF2 (100k iterations) / TrustedPluginPackageStore HMAC 密钥派生增强：使用 Windows SID 二进制（而非 MachineName 字符串）、16 字节随机盐和 PBKDF2（10 万次迭代）
- Download URL allowlist restricts plugin downloads to known trusted hosts (github.com, jsdelivr.net); file:// URLs rejected in production / 下载 URL 白名单仅允许受信任主机；生产环境拒绝 file:// URL
- UpdateChecker enforces SHA256 validation; missing hashes now throw in release builds / UpdateChecker 强制 SHA256 校验，缺失哈希时在发布版本中抛出异常

### Fixed / 修复

- Registry hive string keys no longer contain trailing spaces, fixing lookups for `HKEY_CLASSES_ROOT` and `HKEY_CURRENT_CONFIG` from the WPF host / Registry hive 字符串键不再包含尾随空格，修复 WPF 宿主中对 `HKEY_CLASSES_ROOT` 与 `HKEY_CURRENT_CONFIG` 的查询
- `SpectrumKeyboardBacklightController.GetDeviceHandleAsync` now retries up to 3 times (was 1) and disposes the previous `_deviceHandle` before reassignment, preventing the leaked handle on re-open / `SpectrumKeyboardBacklightController.GetDeviceHandleAsync` 重试次数从 1 提升到 3，并在重新分配前释放旧 `_deviceHandle`，避免重新打开时句柄泄漏
- `Registry.ObserveKey` and `DriverKeyListener` now wrap their `ManualResetEvent` in `using` to ensure native handle disposal on listener shutdown / `Registry.ObserveKey` 与 `DriverKeyListener` 现在将 `ManualResetEvent` 用 `using` 包裹，确保监听关闭时释放原生句柄
- `NotifyIcon` now tracks the HICON handed to the shell and calls `DestroyIcon` when the icon is replaced or the tray icon is disposed, fixing a tray-icon HICON leak / `NotifyIcon` 现在跟踪提交给 Shell 的 HICON，并在图标替换或托盘图标释放时调用 `DestroyIcon`，修复托盘图标 HICON 泄漏
- `GPUController` now exposes the process list as `IReadOnlyList<Process>` and rebuilds it on every refresh, eliminating the background-thread race when the UI reads the list / `GPUController` 现以 `IReadOnlyList<Process>` 暴露进程列表并每次刷新重建，消除 UI 读取时与后台线程的竞态
- Replaced `.ConfigureAwait(true)` with `.ConfigureAwait(false)` in `FpsSensorController`, `WindowsOptimizationPage`, `PluginExtensionsPage`, `StartupDeviceSetupCoordinator`, and `SettingsApplicationBehaviorControl` to avoid deadlocks and respect the project async guideline / 在 `FpsSensorController`、`WindowsOptimizationPage`、`PluginExtensionsPage`、`StartupDeviceSetupCoordinator`、`SettingsApplicationBehaviorControl` 中将 `.ConfigureAwait(true)` 替换为 `.ConfigureAwait(false)`，避免死锁并遵循项目异步规范
- `WindowsOptimizationPage` driver packages now subscribe to `PropertyChanged` via a named method and unsubscribe in the `Unloaded` handler, preventing lambda-based event-handler leaks / `WindowsOptimizationPage` 驱动包改用具名方法订阅 `PropertyChanged` 并在 `Unloaded` 处理器中取消订阅，避免基于 lambda 的事件处理器泄漏
- CLI IPC client now uses exponential backoff with jitter (up to 40 attempts, 3s max delay) to handle startup race conditions / CLI IPC 客户端改用指数退避 + 抖动策略（最多 40 次重试，3 秒最大延迟）处理启动竞态条件
- Hardcoded `IsEnabled`/`Visibility` values replaced with data bindings in UpdateWindow, UnsupportedWindow, DeviceInformationWindow, DiscreteGPUControl / UpdateWindow、UnsupportedWindow、DeviceInformationWindow、DiscreteGPUControl 中硬编码的 IsEnabled/Visibility 已替换为数据绑定
- LargeFilesWindow fully localized with all 15+ hardcoded English strings migrated to resources; AutomationProperties.Name added / LargeFilesWindow 完全本地化，15+ 个硬编码英文字符串已迁移到资源文件；添加了 AutomationProperties.Name
- MacroPage and AutomationPage ToggleSwitches now sync state through data binding instead of manual Click handlers / MacroPage 和 AutomationPage 的 ToggleSwitch 改为通过数据绑定同步状态
- PluginManager.CheckForUpdatesAsync no longer returns empty dictionary; delegates to PluginRepositoryService for real update checking / PluginManager.CheckForUpdatesAsync 不再返回空字典，改为委托给 PluginRepositoryService 进行真正的更新检查
- Application startup refactored: async void Application_Startup is now a thin dispatcher; startup logic extracted to testable StartupOrchestrator class / 应用启动重构：async void Application_Startup 改为薄分发器；启动逻辑提取到可测试的 StartupOrchestrator 类
- Startup orchestrator and single-instance Mutex activation now have dedicated unit tests / 启动编排器和单实例 Mutex 激活现在有专门的单元测试
- 170+ hardcoded English exception messages across Lib projects migrated to resource files with ExceptionHelper wrapper / Lib 项目中 170+ 个硬编码英文异常消息已迁移到带 ExceptionHelper 包装器的资源文件
- DeviceInformationWindow and DiscreteGPUControl now have proper data binding for Visibility and IsEnabled states / DeviceInformationWindow 和 DiscreteGPUControl 的 Visibility 和 IsEnabled 状态现在有正确数据绑定
- UnsupportedWindow countdown button now uses data binding instead of hardcoded IsEnabled / UnsupportedWindow 倒计时按钮改用数据绑定而非硬编码 IsEnabled
- Power consumption can now be read reliably; sensor data reads have a 2-second timeout so slow sensors no longer block the UI, and failures fall back to the session cache instead of leaving stale values / 功耗读取恢复正常；传感器读取加入 2 秒超时保护，慢速传感器不再阻塞 UI，读取失败时会回退到会话缓存而非显示过期数据

- Dashboard trend charts no longer restart from scratch when navigating away and back; accumulated history is now restored across page navigation / 控制台趋势图切换页面后不再重新从头显示，累积的历史趋势数据在页面导航中得以保留

- Main window minimum width is wider so the dashboard console no longer compresses sensor cards as tightly at the smallest size / 主窗口最小宽度已加宽，避免控制台在最小尺寸下过度压缩传感器卡片
- Dashboard sensor cards keep CPU, battery, and GPU on one row at every window size, using a compact small-window mode and a wider chart layout on large screens / 控制台传感器卡片在所有窗口尺寸下都保持 CPU、电池、GPU 同一行显示，并在小窗口使用紧凑模式、大屏使用更宽的图表布局
- Battery sensor cards now include a live trend chart, and CPU/GPU/battery summary metrics stay on the gauges instead of being duplicated as progress rows / 电池传感器卡片现在包含实时趋势图，CPU/GPU/电池摘要指标保留在环形仪表上，不再重复为进度条行
- Sensor details no longer slow down compact dashboard loading, and unavailable detail rows are hidden instead of filling the console with repeated unavailable values / 传感器详情不再拖慢紧凑控制台加载，不可用的详情行会被隐藏，避免重复显示大量不可用值
- App typography now adjusts with the active display DPI so dashboard text stays compact on high-scaling screens / 应用字体现在会根据当前显示器 DPI 调整，在高缩放屏幕上保持控制台文字更紧凑
- Small dashboard windows now open sensor details in a separate dialog on double-click instead of suppressing details / 小尺寸控制台现在双击会用独立窗口显示传感器详情，而不是直接禁用详情
- Dashboard trend charts are reset and redrawn each time the dashboard sensors open / 控制台传感器趋势图现在每次打开都会重置并重新绘制
- Dashboard sensor detail hints no longer pop up every time the console is opened / 控制台传感器详情提示不再每次打开控制台时弹出
- Malformed BIOS package level values are skipped instead of crashing package rules / 格式错误的 BIOS Level 值会被跳过，不再导致包规则崩溃
- Driver package SHA256 sidecar files in GNU `hash filename` format are accepted / 驱动包校验支持 GNU 风格的 `hash filename` SHA256 sidecar 格式
- Device pack reinstall no longer deletes the existing pack before the replacement move succeeds / 设备包重装时仅在替换移动成功后才移除旧包，避免安装中断导致已安装包丢失
- One malformed Lenovo Vantage catalog entry no longer prevents listing all packages / 单个格式错误的 Lenovo Vantage 包元数据不再导致整份目录列表失败
- CLI IPC connection retries with backoff while the app is still starting / CLI 在应用启动期间会以退避重试 IPC 连接

### Improved / 改进

- CI PR gate consolidated to `Ci-tests.yml`; coverage artifacts uploaded; High/Critical NuGet vulnerabilities block merges / CI 合并至 `Ci-tests.yml` 门禁，上传覆盖率产物，High/Critical NuGet 漏洞将阻断合并
- Main navigation background now matches the outer window surface for a more unified shell / 主导航背景现在与窗口外围底色一致，使应用外壳视觉更统一

- Aligned English and Chinese README product positioning, compatibility scope, version references (v4.2.1), legacy-identifier table, and contribution policy so "Universal" vs Lenovo-only hardware control is explicit / 统一中英文 README 的产品定位、兼容范围、版本引用（v4.2.1）、遗留标识说明与贡献政策，明确「Universal」与联想专用硬件控制的边界
- Updated winget locale descriptions and draft template to reflect full-control vs basic-mode positioning; removed misleading `vantage` tag / 更新 winget 描述与草稿模板以反映完整控制与基础模式定位，移除易误导的 vantage 标签
- Marked `Docs/RELEASE_NOTES_4.1.0_DRAFT.md` as historical; current notes live in CHANGELOG / 将 4.1.0 发布说明草稿标记为历史文档，当前说明以 CHANGELOG 为准
- Enabled `RestorePackagesWithLockFile` in `Directory.Build.props` so future restores consume committed `packages.lock.json` files for reproducible builds / 在 `Directory.Build.props` 中启用 `RestorePackagesWithLockFile`，使后续还原基于已提交的 `packages.lock.json` 实现可复现构建
- Upgraded `Markdig` 1.2.0 -> 1.3.2 and `Autofac` 9.1.0 -> 9.3.0 in central package management for current fixes and performance improvements / 在中央包管理中将 `Markdig` 由 1.2.0 升至 1.3.2、`Autofac` 由 9.1.0 升至 9.3.0，获取上游修复与性能改进

## [4.2.1] - 2026-06-07

### Changed / 变更

- Redesigned the dashboard home page for large windows: sensor sections now show radial gauges and live CPU/GPU trend charts with a legend, battery capacity is visualized with rings, and the dashboard groups reflow into 1, 2, or 3 columns based on window width so content no longer stretches awkwardly when maximized / 重新设计大窗口下的仪表盘主页：传感器区域改用环形仪表盘并加入带图例的 CPU/GPU 实时趋势图，电池容量以环形图展示，仪表盘分组会根据窗口宽度自适应为 1、2 或 3 列，最大化时内容不再被拉伸失真

### Fixed / 修复

- Fixed a startup crash when a damaged dashboard settings file contains empty sensor/dashboard groups / 修复仪表盘设置文件损坏并包含空传感器/仪表盘分组时的启动崩溃
- Fixed dashboard and console sensor loading so partial CPU/GPU readings stay visible, intermittent missing metrics no longer flash, and subsequent opens reuse cached data in the same app session / 修复仪表盘和控制台传感器加载，部分 CPU/GPU 读数会保持显示、偶发缺失的指标不再闪烁，并且同一次应用运行中的后续打开会复用缓存数据
- Fixed GPU power readings flickering to unavailable and restored battery current and average temperature display / 修复 GPU 功耗读数闪烁为不可用的问题，并恢复电池当前温度和平均温度显示

## [4.2.0] - 2026-06-04

### Highlights / 重点

- Stable release for in-app updates from v4.1.0. Existing installs can check for updates normally and install the Full or Online setup package from GitHub Releases.
- Added a real-time on-screen display (OSD) overlay for hardware metrics, ported from upstream Lenovo Legion Toolkit behavior.
- Rolls up post-4.1.0 fixes for God Mode preset refresh, installed optimization-only plugins, dashboard sensor loading, expanded sensor aliases, shipping-test isolation, real hardware validation guards, and broader 2020+ basic-mode device matching.

### Added / 新增

- Added on-screen display (OSD) overlay with Panel and Bar styles, configurable metrics, and settings under Application behavior when hardware sensors are enabled / 新增屏幕显示（OSD）浮层，支持 Panel 与 Bar 两种样式、可配置指标，并在启用硬件传感器时于应用行为设置中提供配置入口

### Fixed / 修复

- Fixed the dashboard power mode settings gear staying hidden on some Legion machines (including Y9000P 2025) when custom mode is available from hardware / 修复部分拯救者机型（含 Y9000P 2025）在硬件已支持自定义模式时，仪表盘性能模式齿轮仍无法打开设置的问题

- Fixed optimization-only plugins after install so manifest and convention-provided System Optimization child actions are visible and usable even when the plugin has no standalone page.
- Fixed dashboard CPU/GPU skeleton timing so loading remains visible until renderable live sensor data is available, and kept battery detailed average temperature out of the dashboard.
- Expanded sensor collection by including nested hardware sensors and additional CPU/GPU/platform aliases used by third-party tools.
- Fixed basic-mode device matching for broader 2020+ brand coverage, including regional brands, family-only catalog packs, and gaming subbrand DMI vendors such as ROG, Alienware, OMEN, Predator, AORUS, and ERAZER.
- Tightened shipping payload guards so test, smoke, validation, and tool projects cannot be referenced by or shipped with the main app.
- Added real hardware validation guard coverage for measurable power-mode readback instead of relying only on simulated state changes.
- Multi-platform work remains diagnostics groundwork in 4.x; formal macOS/Linux release assets are deferred to 5.x.

## [4.1.0] - 2026-05-31

### Highlights / 重点

- Fixed God Mode preset management so create, rename, delete, and preset switching refresh both the visible picker and the stored state correctly.
- Fixed optimization-only plugins so installed local plugins can contribute System Optimization child actions even when they do not expose a standalone page.
- Expanded sensor coverage and fallback handling with better VRAM, GPU hot-spot, memory temperature, SSD temperature, voltage, and shared-memory GPU readings.
- Fixed dashboard loading timing so skeletons remain visible until the first real content refresh is ready, and removed the battery detailed average temperature field.
- Continued separating hardware-validation and smoke-only behavior out of the shipping app and into standalone tools.

### Improved / 改进

- Fixed God Mode preset create, rename, delete, and switching behavior so the UI and persisted state stay in sync.
- Fixed Plugin Extensions optimization-only plugins so their System Optimization child actions load correctly from installed local plugin directories.
- Expanded dashboard and fallback sensor coverage with additional VRAM, memory, SSD, voltage, hot-spot, and memory-temperature readings on more machines.
- Fixed dashboard skeleton timing so loading indicators stay visible until the first real content refresh has completed, and removed the battery detailed average temperature field.
- Continued moving hardware validation and other smoke-only behavior into standalone tools instead of shipping-app entry points.
- Prepared 4.1.0 release metadata, package-manager manifest generation, and local validation so winget/Scoop manifests can be finalized from the tagged release assets.

## [4.0.0] - 2026-05-29

### Highlights / 重点

- Universal Device Toolkit is now the stable public name. Existing Lenovo Legion Toolkit installations can upgrade in place while settings, plugins, updater paths, winget, and Scoop compatibility are preserved.
- Added Full and Online release packages. Full includes bundled languages and device support data; Online starts smaller and installs language/device resources from the GitHub Pages catalog through the app flow.
- Plugin Extensions now use plugin-owned metadata and translations for names, descriptions, details, usage guides, and optimization entries.

### Added / 新增

- Added a first-run language and device setup flow, including online language/device resource installation and progress feedback.
- Added broader basic-mode support for more Lenovo and non-Lenovo PCs, with device matching for common ASUS, Dell, HP, Acer, Xiaomi, Huawei, MECHREVO, Clevo/Tongfang, and generic desktop/laptop profiles.
- Added generic CPU/GPU telemetry fallbacks for basic-mode systems when Lenovo-specific sensor paths are not available.

### Fixed / 修复

- Fixed language pack installation so newly selected languages are available after first launch and from Settings.
- Fixed online plugin installation so installed plugins reload correctly, show their available actions, and no longer open to a misleading "No UI" message when a settings page or optimization entry exists.
- Fixed settings-only plugins appearing as empty System Optimization categories with no actions to select.
- Fixed plugin download progress so installs continue across page navigation and queued installs show a clear waiting state.
- Fixed God Mode preset rename, create, delete, and switching persistence.
- Fixed the Device Information window leaving a large blank area when warranty details are unavailable.
- Fixed Dashboard sensor details so double-click expand/collapse works reliably across child controls.
- Fixed startup stability issues around Lenovo WMI feature reads and update banner event binding.

### Improved / 改进

- Improved Plugin Extensions with collapsible details, usage guides, better spacing, and online/store metadata that stays separate from main app translations.
- Improved Dashboard sensor cards so available CPU, battery, and GPU readings stay visible instead of disappearing when one backend is unsupported.
- Improved the main left navigation with a smoother collapsed/expanded transition.
- Improved performance mode selection by keeping unsupported modes out of the picker and falling back safely when hardware reports an unavailable state.
- Improved package naming, release notes structure, SHA256 output, GitHub Pages resource catalog descriptions, and legacy installer alias handling for the rename transition.

## [3.8.1] - 2026-05-23

### Added

- Added a neutral catalog-backed device support provider while keeping the legacy Lenovo provider facade for compatibility.
- Expanded built-in and generated device packs for ThinkPad, ThinkCentre, ThinkStation, IdeaCentre, Legion desktop, XiaoXin, Y-series legacy, V-series, Slim, Motorola, ASUS, Dell, HP, Acer, MSI, Microsoft Surface, and generic PC basic mode.
- Added vendor alias matching and additional basic-mode packs for GIGABYTE/AORUS, Razer, Samsung, HUAWEI, Xiaomi/Redmi, HONOR, LG, Framework, Panasonic, Dynabook/Toshiba, Fujitsu, VAIO, MEDION, XMG/SCHENKER, Clevo/Tongfang, and related barebone vendors.

### Changed

- Startup device setup now evaluates recommendations through the injected device-support provider instead of hard-coding the Lenovo singleton.
- Non-Lenovo and unsupported Lenovo systems now keep hardware-specific controls hidden while basic workflows such as plugins, system optimization, language, theme, updates, and logs remain available.
- Device-pack vendor matching now normalizes common BIOS/DMI vendor formatting differences, including punctuation, spacing, casing, diacritics, and common company suffix variants such as Inc./Incorporated, Corp./Corporation, and Ltd./Limited.

## [3.8.0] - 2026-05-20

### Added / 新增

- Added the Universal Device Toolkit identity layer with new public display names, new update/resource repository defaults, and legacy Lenovo Legion Toolkit names kept for upgrade compatibility.
- Added Lenovo-first device support data for Legion, LOQ, IdeaPad, ThinkBook, YOGA, Lenovo Slim, legacy Lenovo gaming families, and Motorola Lenovo devices, plus a basic mode for unsupported PCs.
- Added a data-only online device-pack manager and GitHub Pages device-pack catalog generation. Device packs are JSON manifests only and reject executable/script content.

### Changed / 变更

- Release packaging now produces Full and Online installers/portable ZIPs under UniversalDeviceToolkit names, plus the UniversalDeviceToolkit setup alias for the first bridge release.
- Language and device resources now publish through GitHub Pages catalogs instead of separate language-pack GitHub Release assets; Online packages include the base English resources.
- Runtime paths migrate from `%LOCALAPPDATA%\UniversalDeviceToolkit` to `%LOCALAPPDATA%\UniversalDeviceToolkit` without deleting the legacy directory.
- Autorun tasks and single-instance guards now use the new name while also handling legacy UniversalDeviceToolkit identifiers.

### Improved / 改进

- Documentation, release notes, promotion copy, winget drafts, and Scoop guidance now lead with Universal Device Toolkit and plugin-extension positioning while preserving old package identifiers during the transition.
- Update selection now prefers UniversalDeviceToolkit Full assets and uses the new repository defaults, with the UniversalDeviceToolkit alias retained for older update/package-manager paths.
- Consolidated release verification around `UniversalDeviceToolkit_vX.Y.Z_SHA256.txt`.

## [3.7.1] - 2026-05-20

### Added / 新增

- Added app theme style presets with persisted settings: Current Style, Official Cool, Midnight Neon, and Forest Tech.
- Added optional online language-pack install/uninstall support and release packaging for Full, Online, and legacy language-pack assets.

### Fixed / 修复

- Hide the advanced Keyboard Backlight navigation and settings entry on devices that do not support Spectrum/RGB keyboard lighting, preventing empty unsupported pages from appearing in normal use.
- Hardened enum display localization so newly added resource keys resolve correctly even before generated resource designer files are refreshed.
- Improved dispatcher, notification, and registry observer async handling to reduce unobserved task failures during startup and UI refresh.

### Improved / 改进

- Expanded visual regression smoke options for theme-style screenshots, settings-only captures, and unsupported-hardware navigation checks.
- Updated release automation to build Full and Online installers, portable ZIPs, legacy language-pack ZIPs, and consolidated SHA256 files.
- Cleaned Plugin Extensions dead import code and tightened UI action dispatch paths across dashboard, automation, keyboard, and display controls.

## [3.7.0] - 2026-05-19

### Fixed / 修复

- Fixed sensor data handling by correcting CPU wattage queries and unit normalization and adding ACPI thermal-zone fallback when Lenovo temperature sensors are unavailable.
- 修复插件 ZIP 安装中的 Zip Slip 路径遍历风险，阻止恶意插件包写入目标目录之外 / Fixed a Zip Slip path traversal vulnerability during plugin ZIP extraction.
- Fixed multiple WPF-UI 4 migration regressions across title-bar buttons, the startup language selector, About scrolling, Plugin Extensions summary cards, and dark-theme text contrast.
Fixed localization issues including the Norwegian clipboard placeholder and the Chinese `GHz` unit label. / - 修正若干本地化细节，包括挪威语剪贴板占位符和中文传感器频率单位 `GHz`

### Improved / 改进

- Expanded README download, community-maintenance, and winget guidance and added Chinese launch materials for release promotion.
- Improved first-load UX with a dashboard skeleton shimmer and a non-polluting CLI loading animation.
- Migrated app settings, automation, CLI IPC, and Spectrum profile serialization to `System.Text.Json` while preserving existing file compatibility.
- Backfilled WPF resource keys and added a `resx` audit script to improve localization completeness and release readiness checks.
Expanded 2025 Lenovo Legion and LOQ Gen 10 model detection. / - 扩展 2025 年 Lenovo Legion 与 LOQ Gen 10 机型识别，补齐 `15AKP`、`15IRX`、`16ADR`、`16AFR`、`17IRX`、`18IAX` 前缀支持
- Improved CLI and plugin compatibility by moving to the stable `System.CommandLine` API and tightening plugin dependency version handling.
- Hardened release infrastructure with .NET 10 Desktop Runtime detection, pre-package tests, dependency updates, and repository maintenance cleanup.

## [3.6.16] - 2026-05-18

### Fixed / 修复

- 恢复 CPU / Battery / GPU 单卡片仪表板布局，默认折叠详细传感器行，并补上双击展开提示 / Restored the single-card CPU / Battery / GPU dashboard layout with detailed rows collapsed by default and a double-click expansion hint.
- 强化传感器刷新与回退逻辑，减少 Lenovo WMI 瞬时失败导致整张卡片消失的问题 / Hardened sensor refresh and fallback behavior so transient Lenovo WMI failures are less likely to hide the whole dashboard card.
- 在运行时读取失败时保留功耗模式控件可见，回退到最近一次已知状态或均衡模式，而不是直接塌掉 / Kept the power mode selector visible by falling back to the last known or balanced mode when runtime reads fail.
- 修复启动阶段未观察任务异常和插件安装/加载兼容性问题，减少在线烟测时的崩溃与误报 / Fixed startup-time unobserved task failures and tightened plugin install/load compatibility for online smoke coverage.

### Improved / 改进

- 收紧 Plugin Extensions 页面布局，移除多余空白选择态，默认选中首个插件，并补充更清晰的说明和使用引导 / Tightened the Plugin Extensions page layout, removed the redundant empty state, auto-selected the first plugin, and added clearer descriptions and usage guidance.
- 扩展在线插件烟测覆盖，补齐安装、配置/打开与卸载链路，提高默认插件集的验证可靠性 / Expanded online plugin smoke coverage across install, configure/open, and uninstall flows.
- 更新 3.6.16 发布与分发文档，补齐中文宣发文案以及 winget / Scoop 维护流程说明 / Updated 3.6.16 release and distribution docs, including Chinese promotion copy and winget/Scoop maintainer workflows.

## [3.6.15] - 2026-04-29

### Fixed / 修复

- 修复设置页、退出流程、驱动下载与系统优化中的多处稳定性问题，包括缺失配置文件日志噪音、`--disable-update-checker` 空白页、RGB 所有权跳过误报、跨线程提示异常，以及 `del` / `rd` 清理命令执行失败 / Fixed stability issues across settings, shutdown, Driver Download, and System Optimization, including noisy missing-config logs, the blank `--disable-update-checker` page, RGB ownership false alarms, cross-thread snackbar errors, and failing `del` / `rd` cleanup actions.
- 修复插件加载与隔离烟测链路中的一组问题，包括 sidecar DLL 解析、过期微软签名兼容、页面重建循环、单实例锁冲突、`ReleaseMutex` 异常，以及插件市场卡片刷新与 `Open` 入口误判 / Fixed plugin-loading and isolated-smoke issues including sidecar DLL resolution, expired Microsoft signature handling, page rebuild loops, single-instance conflicts, `ReleaseMutex` errors, marketplace card refresh, and `Open` entry-point misclassification.
- 修复应用内 IPC 截图路径重置 WPF 渲染模式的问题，降低兼容模式和软件渲染场景再次出现空白窗口的概率 / Fixed the in-app IPC screenshot path resetting the WPF render mode and reintroducing blank-window issues in compatibility or software-rendering sessions.
- 放宽系统优化命令校验规则，允许受控的 stdout / stderr 重定向，避免 `2>&1` 等合法片段被误判为注入风险 / Relaxed System Optimization command validation so controlled stdout/stderr redirection such as `2>&1` is no longer misclassified as an injection risk.

### Improved / 改进

- CLI 新增 `status` 命令，可快速查看主程序连接状态、更新检查禁用状态和当前更新仓库，便于诊断设置页问题 / Added a `status` CLI command for checking host connectivity, update-check disablement, and the active update repository.
- 驱动下载页重做为更完整、更紧凑的队列式体验，补齐队列管理、空状态、无结果、隐藏项恢复和完成状态展示 / Reworked Driver Download into a more complete and compact queue-based workflow with improved empty, no-result, restore, and completed states.
- 插件扩展页改为左侧列表加右侧详情的管理器式布局，骨架屏与最终布局对齐，卡片操作、图标与说明更清晰 / Reworked Plugin Extensions into a manager-style list/detail layout with aligned skeleton states, clearer actions, icons, and descriptions.
- `custom-mouse` 与 `shell-integration` 统一收敛到配置窗口和系统优化分类入口，降低侧栏分散度 / Consolidated `custom-mouse` and `shell-integration` into the settings-window plus System Optimization entry model.
- 系统优化页将右上角与批量操作迁入工具栏，并补充稳定 AutomationId，提升 UI 冒烟和辅助功能定位 / Moved System Optimization top-right and bulk actions into the toolbar and added stable AutomationIds for UI smoke and accessibility.
- `MainAppPluginUi.Smoke` 做了一轮系统性增强，包括动画等待、弹窗识别与消除、页面截图索引、`--watch` / `--theme` / `--screenshots` 等参数，以及主窗口/设置窗口 IPC 导出能力 / Expanded `MainAppPluginUi.Smoke` with animation-aware waits, popup handling, screenshot indexing, watch/theme/screenshot options, and IPC-based window exports.
- 强化真实插件安装与在线市场验证链路，改进市场刷新、安装成功判定、下载重试、串行安装、页面就绪判断和 `curl.exe` 下载兜底 / Hardened real plugin-install and marketplace validation with better refresh handling, install-success detection, download retries, serial installs, readiness checks, and a `curl.exe` fallback.
- 插件设置宿主壳和样式设置页做了纯 UI 打磨，放大窗口、减少双层滚动，并让 `shell-integration` / `custom-mouse` 的设置体验更接近宿主应用 / Polished the plugin settings host shell and style settings pages with larger windows, less nested scrolling, and host-aligned presentation.
- 新增 `Main App UI Smoke` workflow 与 PowerShell runner 包装脚本，可在独立交互式 Windows runner 上执行真实 UI smoke 并自动回传截图与日志 / Added a dedicated `Main App UI Smoke` workflow and PowerShell runner wrapper for real UI smoke on an interactive Windows runner.

## [3.6.14] - 2026-04-19

### Added / 新增

- 新增设计令牌体系和标准按钮/字体样式，统一间距、圆角、图标、按钮尺寸和字号，为后续 UI 收敛打基础 / Added design tokens plus standard button and typography styles to unify spacing, corners, icon sizes, button sizes, and text scales.

### Improved / 改进

- 将 Plugin Extensions、Windows Optimization、CardHeader、About、Macro、Sensors 等页面的局部样式收敛到全局设计令牌和 Typography，提升界面一致性 / Consolidated local page styles onto shared design tokens and typography across Plugin Extensions, Windows Optimization, CardHeader, About, Macro, Sensors, and related surfaces.
- 插件 UI 烟测切换到更真实的隔离沙箱流程，同时支持真实在线安装和本地 ZIP 导入，并允许通过 `LLT_PLUGIN_SIGNATURE_MODE` 切换签名校验策略 / Moved plugin UI smoke to a more realistic isolated-sandbox flow with real online installs, local ZIP imports, and an explicit `LLT_PLUGIN_SIGNATURE_MODE` override.
- 改进插件市场的在线元数据与安装包下载抗抖动能力，加入多源镜像、重试和缓存回退，减少 GitHub 连接重置带来的空市场页和安装失败 / Improved plugin marketplace resilience with mirrored sources, retries, and cached fallback for metadata and package downloads.
- 升级 GitHub Actions 依赖并清理测试项目中的 nullable / xUnit analyzer 告警，降低 CI 噪声和后续 runner 升级风险 / Upgraded GitHub Actions dependencies and cleaned nullable/xUnit analyzer warnings to reduce CI noise and future runner-upgrade risk.

### Fixed / 修复

- 修复驱动特性写入校验、Quick Action 循环引用、进程监听器缓存清理等核心稳定性问题，减少误报成功、卡死和高事件压力下的异常 / Fixed core stability issues around driver-feature verification, cyclic Quick Actions, and process-listener cache cleanup.
- 修复 IPC 命名管道 ACL、设置页 toggle 永久禁用、关闭时最小化异步保存竞争，以及安装器忽略非零退出码等问题 / Fixed IPC ACL issues, permanently disabled toggles after errors, close-to-tray save races, and installers falsely reporting success on non-zero exit codes.
- 修复插件加载器缺少 sidecar 依赖解析、插件市场失败更新残留半复制目录、以及重扫竞态导致的状态错乱 / Fixed plugin-loader sidecar dependency resolution plus marketplace update/install races that could leave half-copied directories or stale UI state.
- 修复 WMI listener 并发重叠、自动化“立即运行”/独显停用/关闭显示器按钮卡死，以及插件商店请求失败被静默视为空列表等问题 / Fixed overlapping WMI listener execution, stuck quick-action buttons, and plugin-store failures being silently treated as an empty list.

## [3.6.13] - 2026-04-18

### Added / 新增

- 为插件加载新增可配置的签名校验模式，在开发与生产场景下都能更明确地拦截未签名插件 / Added configurable signature validation modes for plugin loading so unsigned plugins are blocked more explicitly in both development and production scenarios

### Fixed / 修复

- 恢复 Panel Logo 灯效功能及其宿主/CLI 注册链路，避免受支持设备上的相关控制项缺失或引入构建回归 / Restored Panel Logo lighting features and their host/CLI registrations so supported devices no longer lose the controls or hit the related build regression
- 为下载的更新包增加 SHA256 完整性校验，避免篡改包在安装前通过校验 / Added SHA256 integrity verification for downloaded update packages so tampered payloads are rejected before installation

### Improved / 改进

- 发布下载现在提供带版本号的安装包、便携 ZIP 和 SHA256 清单，便于校验、归档与问题排查 / Release downloads now ship with versioned setup, portable ZIP, and SHA256 manifest assets for easier verification, archiving, and support

## [3.6.12] - 2026-03-28

### Fixed / 修复

- **Plugin Install State Recovery / 插件安装状态恢复**: Cleared pending-deletion markers during reinstall/update, rejected plugin packages whose normalized `plugin.json` ID does not match the requested store manifest, and blocked incompatible host-version installs before download state is persisted so plugin updates no longer disappear after app exit or land in a false-installed state / 在重装与更新时清理待删除标记，拒绝规范化后 `plugin.json` ID 与商店清单不一致的插件包，并在下载前拦截宿主版本不兼容的安装，避免插件更新后在退出时被删掉，或进入“显示已安装但实际不可用”的错误状态
- **Plugin Update Accuracy / 插件更新准确性**: Limited marketplace update checks to truly installed plugin IDs and aligned quick-open executable discovery with real plugin directories and metadata-backed locations so update badges and `Open` actions no longer drift when only discoverable/local plugins are present / 将插件市场更新检查收敛到真正已安装的插件 ID，并让快速打开动作按真实插件目录与元数据定位可执行文件，避免仅存在可发现/本地插件时更新提示和 `Open` 行为误报

## [3.6.11] - 2026-03-27

### Fixed / 修复

- **WPF Rendering Compatibility / WPF 渲染兼容性**: Centralized the software-rendering fallback in `RenderingCompatibilityHelper`, applied an opaque window background fallback in `BaseWindow`, and routed app startup render-mode selection through the helper so Remote Desktop / forced-software-rendering sessions no longer show blank Mica/Acrylic windows. Evidence: `C:\Users\96152\.openclaw\workspace\opencode_automation\report\UniversalDeviceToolkit\build-rendering-compat.log`, `C:\Users\96152\.openclaw\workspace\opencode_automation\report\UniversalDeviceToolkit\rendering-compat.diff` / 将软件渲染兜底逻辑集中到 `RenderingCompatibilityHelper`，在 `BaseWindow` 中补充不透明背景兜底，并让启动阶段的渲染模式统一走 helper，避免远程桌面或强制软件渲染场景下 Mica/Acrylic 窗口空白
- **Plugin UI Localization / 插件界面本地化**: Replaced plugin marketplace summary/capability/author strings with localized resources and added a localized optimization failure format so simplified Chinese UI no longer shows English labels such as `Total Plugins`, `Quick Open`, or `Failed to apply ...` in the plugin workflow / 将插件市场的摘要、能力标签、作者前缀改为资源化本地化文本，并补充系统优化失败消息模板，使简体中文插件流程中不再显示 `Total Plugins`、`Quick Open`、`Failed to apply ...` 等英文标签
- **Menu Style Editor Localization / 菜单样式编辑器本地化**: Replaced hard-coded Chinese strings in `MenuStyleSettingsWindow` with resource lookups, localized apply/open error prompts, and aligned the editor with the actual Shell config availability so Chinese and cold-locale runs no longer mix in untranslated text or expose missing-file actions / 将 `MenuStyleSettingsWindow` 中的硬编码中文替换为资源查找，补齐应用与打开失败提示的本地化，并按实际 Shell 配置文件可用性控制编辑器状态，避免中文和冷门语言运行时混入未翻译文本或暴露无效文件操作
- **Plugin Host Localization Refresh / 插件宿主本地化刷新**: Added a shared plugin-resource culture change event, taught `PluginPageWrapper` and `PluginSettingsWindow` to rebuild plugin UI after culture changes, and localized previously hard-coded plugin host empty states, delete confirmations, snackbars, and ZIP dialog filters so plugin pages/settings inherit language changes more reliably instead of keeping stale text or popping untranslated helper UI / 新增共享的插件资源文化变更事件，让 `PluginPageWrapper` 与 `PluginSettingsWindow` 在语言变化后重建插件界面，并把原本硬编码的插件宿主空状态、删除确认、提示条与 ZIP 文件对话框过滤文本资源化，减少插件页/设置页保留旧语言或弹出未翻译的辅助界面
- **Plugin Marketplace Hidden Copy / 插件市场隐藏文案**: Routed the remaining install/uninstall/bulk-import fallback snackbars in `PluginExtensionsPage` through resource keys so the plugin marketplace no longer mixes English helper text like install-failed details, dependency uninstall warnings, or `Unknown` import-source placeholders into Chinese mode / 将 `PluginExtensionsPage` 中残留的安装、卸载、批量导入兜底提示统一改走资源键，避免插件市场在中文模式下继续混入安装失败详情、依赖卸载警告或 `Unknown` 这类英文占位提示
- **Hidden Settings Dialog Copy / 隐藏设置弹框文案**: Routed package-download example placeholders in `PackagesPage` and the mirrored `WindowsOptimizationPage` surface through shared resource keys, and localized the Compatibility Check window's manual “Open Log” failure message so hidden host dialogs no longer mix hard-coded English/Chinese helper text / 将 `PackagesPage` 及镜像到 `WindowsOptimizationPage` 的包下载示例占位文案改走共享资源键，并把兼容性检查窗口里手动“打开日志”失败提示资源化，避免宿主隐藏弹框继续混入硬编码中英文辅助文本
- **Explorer Restart Reliability / Explorer 重启可靠性**: Replaced duplicated Explorer restart snippets with a shared helper that waits for Explorer to fully exit, relaunches it via the shell with a `cmd /c start` fallback, uses the Windows `explorer.exe` full path, and verifies the process actually returns so optimization and menu-style apply flows no longer leave the desktop shell closed / 用共享 helper 替换重复的 Explorer 重启片段：等待 Explorer 完全退出、通过 shell 方式拉起并提供 `cmd /c start` 兜底、使用 Windows 下 `explorer.exe` 的全路径、并校验进程确实恢复，避免系统优化和菜单样式应用流程把桌面壳杀掉后不自动回来
- **Windows Optimization Locale Drift / 系统优化语言漂移**: Forced `WindowsOptimizationViewModel` category/action resource lookups to use the app's active `Resource.Culture` explicitly, fixing the case where cold locales like `uz-latn-uz` still rendered Shell Integration and optimization-category cards in Chinese because those strings were resolved against the wrong culture during initialization / 让 `WindowsOptimizationViewModel` 中分类与动作文案的资源读取显式使用当前应用的 `Resource.Culture`，修复 `uz-latn-uz` 这类冷门语言下 Shell Integration 与系统优化分类卡片仍回落成中文的问题
- **Plugin Smoke Cold-Locale Coverage / 插件冒烟冷门语言覆盖**: Taught `MainAppPluginUi.Smoke` to temporarily pre-seed requested plugins into `InstalledExtensions`, restore settings afterward, and fall back to optimization/sidebar verification when marketplace-only settings windows are unavailable, which unblocked `uz-latn-uz` screenshot validation for `custom-mouse` and `network-acceleration` / 让 `MainAppPluginUi.Smoke` 在运行前临时把目标插件写入 `InstalledExtensions` 并在结束后恢复设置，同时在市场页设置窗口不可用时回退到系统优化页或侧边栏验证，从而打通 `custom-mouse` 与 `network-acceleration` 在 `uz-latn-uz` 下的截图验证
- **English Fallback Chain / 英文兜底链**: Added a shared `LocalizationHelper.GetStringOrEnglish(...)` path for runtime resource lookups, forcing dynamic UI text to resolve through current language -> parent culture -> English instead of drifting into unrelated cultures such as Chinese; wired it into Windows Optimization, Plugin Extensions, MainWindow, cleanup helpers, and other runtime-only windows so missing keys now fall back to English consistently / 新增共享的 `LocalizationHelper.GetStringOrEnglish(...)` 运行时资源查找链，让动态 UI 文案统一按“当前语言 -> 父文化 -> 英文”解析，不再漂到中文等无关文化；并已接入系统优化、插件市场、MainWindow、清理提示和其他运行时窗口，使缺键时统一回退英文
- **Unsupported Window Hyperlink Crash / 不受支持硬件窗口超链接崩溃**: Split GitHub link constants into XAML-safe string URLs and kept Uri variants for code paths, then switched `UnsupportedWindow` and `AboutPage` hyperlink bindings to the string values so WPF-UI no longer throws `NavigateUri` parse exceptions during compatibility-check startup / 将 GitHub 链接常量拆为 XAML 安全的字符串 URL，并在代码路径保留 Uri 变体；`UnsupportedWindow` 与 `AboutPage` 超链接绑定改用字符串值，避免兼容性检查启动时 WPF-UI 抛出 `NavigateUri` 解析异常

### Improved / 改进

- **ViVeTool Plugin Smoke / ViVeTool 插件冒烟**: Normalized runtime fixture IDs for `vive-tool`, switched settings-window discovery to descendant modal scanning, used the Configure-button route when marketplace double-click is not applicable, and added in-process screenshot capture fallback so the host smoke run now reaches the `ViVeTool 设置` window reliably / 规范 `vive-tool` 的 runtime fixture ID 映射，将设置窗探测改为 descendant 模态窗口扫描，在市场双击不适用时回退到 Configure 按钮路径，并补充进程内截图兜底，使宿主冒烟现在可以稳定进入 `ViVeTool 设置` 窗口
- **Plugin UI Smoke / 插件界面冒烟**: Updated `MainAppPluginUi.Smoke` to launch the app with `--skip-compat-check`, tolerate missing refresh buttons, and capture optimization-route settings windows so Shell Integration localization and availability fixes can be verified on unsupported hardware / 更新 `MainAppPluginUi.Smoke`：启动主程序时自动附加 `--skip-compat-check`、兼容缺失的刷新按钮，并支持截取优化路由下的设置窗口，便于在不受支持硬件上验证 Shell Integration 的本地化与可用性修复
- **Plugin UI Smoke Evidence / 插件界面冒烟证据**: Completed a real end-to-end smoke pass for `shell-integration` through the optimization route, including action toggles and screenshot capture, while recording reproducible failure evidence for `custom-mouse` category discovery and runtime fixture file-lock cleanup during broader plugin-set runs / 已基于真实运行完成 `shell-integration` 的系统优化路由端到端冒烟验证，包含动作切换与截图留证，同时记录了 `custom-mouse` 分类定位失败以及更大插件集合运行时 fixture 清理文件锁定的可复现失败证据
- **Plugin UI Smoke Stability / 插件界面冒烟稳定性**: Hardened `MainAppPluginUi.Smoke` against stale UI Automation window handles during plugin settings validation so Shell and Network Acceleration screenshot runs no longer fail on transient `ElementNotAvailableException` cleanup/rebind paths / 加固 `MainAppPluginUi.Smoke` 对插件设置验证期间陈旧 UI Automation 窗口句柄的处理，避免 Shell 与网络加速截图因瞬时 `ElementNotAvailableException` 清理/重绑失败
- **Shell Integration Smoke Coverage / Shell Integration 冒烟覆盖**: Adjusted `MainAppPluginUi.Smoke` to validate the real optimization-route entry for `shell-integration`, capture the main-window optimization screenshot, and avoid redundant return-to-market cleanup when the plugin was already installed / 调整 `MainAppPluginUi.Smoke`，改为验证 `shell-integration` 实际走到的系统优化入口、截取主窗口优化页截图，并在插件本来就已安装时跳过多余的回插件市场清理流程

## [3.6.6] - 2026-03-15

### Fixed / 修复

- **Plugin Store Source / 插件商店源**: The plugin catalog is now fetched from the main repository's rolling `plugin-catalog` release, avoiding a separate repository and keeping application releases readable / 插件目录现在从主仓库滚动的 `plugin-catalog` 发布获取，避免额外仓库并保持应用发布列表清晰
- **Plugin Marketplace / 插件市场**: Verified end-to-end download, install, and load of `shell-integration v1.0.4` through the main app plugin system after the store-source correction / 在修正商店源之后，已通过主程序插件系统端到端验证 `shell-integration v1.0.4` 的下载、安装与加载流程

### Improved / 改进

- **Plugin Documentation / 插件文档**: Added main-repository links to `Plugins/Official`, manifests, and release metadata so the complete plugin surface is discoverable from one project

### Fixed / 修复

- **Remote Desktop Rendering / 远程桌面渲染**: Added a software-rendering fallback toggle for RDP/headless sessions to avoid blank UI when no physical display is active / 为远程桌面或无显示器场景新增“软件渲染”开关与兜底策略，避免界面空白
- **Background Command Execution / 后台命令执行**: Fixed `CMD.RunAsync` fire-and-forget mode to stop redirecting stdout/stderr when `waitForExit` is `false`, preventing background processes with large output from hanging before completion
- **Command Injection Guard / 命令注入闃叉*: Fixed `CMD.ContainsDangerousInput` to scan all `&` occurrences so mixed input like safe redirection followed by command chaining can no longer bypass validation
- **CMD Argument Validation / CMD 参数校验**: Fixed `CMD.ContainsDangerousInput` false positives by allowing escaped ampersands (`^&`) and valid implicit redirection (`>&1`, `>&2`) while still blocking no-space command chaining (e.g. `echo a&whoami`) / 修复 `CMD.ContainsDangerousInput` 误判：在保持拦截无空格命令拼接（如 `echo a&whoami`）的同时，允许合法的转义与重定向写法（`^&`、`>&1`、`>&2`）
- **Command Execution / 命令执行**: Fixed `CMD.RunAsync` output-buffer deadlock by draining standard output/error streams while waiting for process exit (prevents hangs on large output commands such as directory listing)
- **Retry Logic / 重试逻辑**: Fixed `RetryHelper` to correctly stop and throw `MaximumRetriesReachedException` after reaching retry limit instead of looping indefinitely / 修复 `RetryHelper` 在达到重试上限后的行为：现在会正确停止并抛出 `MaximumRetriesReachedException`，不再无限循环
- **Power Mode Error Message / 电源模式错误消息**: Fixed `PowerModeUnavailableWithoutACException` message to include the blocked power mode for clearer diagnostics / 修复 `PowerModeUnavailableWithoutACException` 的消息内容，包含被阻止的电源模式，便于问题诊断
- **Status Tray Popup / 托盘状态弹窗**: Hide battery discharge/min/max rate rows when running in compatibility mode to avoid showing meaningless `0.00 W` values on unsupported machines
- **Localization / 本地化**: Fixed plugin-open error localization by removing an invalid `{0}` placeholder from the title resource and unifying plugin open failure message formatting to `PluginExtensionsPage_OpenFailedMessage`
- **Localization / 本地化**: Fixed missing `SettingsPage_Autorun_Message` in base and zh-Hans resources to ensure settings subtitle renders correctly in default and simplified Chinese UI / 修复基准与简体中文资源中缺失的 `SettingsPage_Autorun_Message`，确保设置页副标题在默认语言与简体中文界面下正确显示
- **Localization / 本地化**: Added missing base resource entries for network optimization action keys used by `WindowsOptimizationCategoryProvider` to ensure fallback localization works outside zh-Hans
- **Localization / 本地化**: Removed stale locale-only resource keys in zh-Hans/zh-Hant/ar that had no code references, and aligned locale files with base keys to reduce translation drift / 清理 zh-Hans/zh-Hant/ar 中无代码引用的陈旧本地化键，并将多语言资源与基准键对齐，降低翻译漂移
- **Localization / 本地化**: Restored base fallback entries for `WindowsOptimizationPage_Extensions_ComingSoon_`* and `PluginExtensionsPage_OpenPluginFailed` to keep `Resource.resx` aligned with generated designer metadata and avoid null fallback strings if reintroduced
- **Localization / 本地化**: Improved Chinese translation quality by synchronizing untranslated `zh-Hant` entries from `zh-Hans` with Simplified-to-Traditional conversion and manually localizing high-visibility plugin/menu-style UI strings in both `zh-Hans` and `zh-Hant` / 提升中文翻译质量：将 `zh-Hant` 中未翻译条目基于 `zh-Hans` 同步并执行简转繁，同时对 `zh-Hans`/`zh-Hant` 的高可见插件/菜单样式界面文案进行人工本地化修订
- **Localization / 本地化**: Performed a full 20+ locale semantic translation pass for newly added English UI strings across WPF/Lib/Automation/Macro resources (Bing-backed batching + placeholder-safe restoration), updating 16k+ entries and preserving resource structure integrity (`missing=0`, `extra=0`, `placeholder_mismatch=0`)
- **Localization**: Added a follow-up 20+ locale semantic completion pass to translate additional English-identical leftovers (`+63` entries across `25` locale files) while keeping structural audit clean (`missing=0`, `extra=0`, `placeholder_mismatch=0`).
- **Localization**: Continued multi-round semantic localization refinement across 20+ locales with interruption-safe per-locale runs, reducing English-identical residual entries from `1047` to `486` while preserving structural consistency (`missing=0`, `extra=0`, `placeholder_mismatch=0`).
- **Localization**: Performed a second continuation wave with locale-specific provider routing and Portuguese mapping compatibility (`pt`), reducing residual English-identical entries from `486` to `291` while keeping structural audits fully clean (`missing=0`, `extra=0`, `placeholder_mismatch=0`).

### Improved / 改进

- **Test Stability / 测试稳定*: Replaced the `CMD.RunAsync` cancellation test command from `timeout` to a deterministic `ping`-based long-running command to avoid environment-dependent false negatives in headless runs
- **Smoke Evidence / 冒烟证据**: Captured latest WPF smoke log at `attachments/lenovo-legion-toolkit/wpf-smoke-latest.log` for verification traceability / 记录最新 WPF 冒烟日志（`attachments/lenovo-legion-toolkit/wpf-smoke-latest.log`）用于验证留痕
- **Localization Workflow / 本地化流程**: Replaced legacy single-file Crowdin mapping with a repository-wide `crowdin.yml` that covers WPF/Lib/Automation/Macro resource modules and locale naming mappings (`zh-hans`, `zh-hant`, `pt-br`, `nl-nl`, `uz-latn-uz`) / 将旧的单文件 Crowdin 映射升级为仓库级 `crowdin.yml`，覆盖 WPF/Lib/Automation/Macro 四个资源模块，并补齐 `zh-hans`、`zh-hant`、`pt-br`、`nl-nl`、`uz-latn-uz` 等语言命名映射
- **Documentation / 文档**: Updated README and Docs set to align with current repository links, workflow files, release examples, and translation synchronization commands / 更新 README 与 Docs 文档集，使其与当前仓库链接、工作流文件、发布示例及翻译同步命令保持一致
- **Documentation / 文档**: Added a WPF smoke build shortcut to deployment docs to highlight `scripts/smoke-build.ps1` / 在部署文档中补充 WPF 冒烟构建快捷命令，说明 `scripts/smoke-build.ps1` 的使用方式
- **Documentation / 文档**: Documented how to capture smoke build output logs with `Tee-Object` for sharing / 说明如何使用 `Tee-Object` 捕获 WPF 冒烟构建输出日志，便于分享
- **Plugin UI Smoke / 插件界面冒烟**: Stabilized `MainAppPluginUi.Smoke` settings-window automation by switching to descendant modal-window discovery, filtering stale window handles, adding deterministic close-wait logic, and using a configure-button fallback when double-click is flaky; verified end-to-end network plugin settings + feature interactions
- **Plugin Open Routing / 插件打开路由**: Extended plugin marketplace `Open` behavior to include optimization-category plugins, and added category-focused navigation into Windows Optimization for `shell-integration` and `custom-mouse` / 扩展插件市场 `Open` 行为以支持系统优化分类插件，并为 `shell-integration` `custom-mouse` 增加跳转系统优化并定位分类的能力

## [3.6.4] - 2026-02-26

### Improved / 改进

- **Plugin Marketplace Validation / 插件市场验证**: Extended desktop smoke validation for plugin marketplace interactions (open plugin page, install/uninstall, double-click configuration window) and verified the end-to-end flow against latest plugin runtime fixes

## [3.6.3] - 2026-02-26

### Improved / 改进

- **Plugin Tooling / 插件工具**: Added `Plugins/Tooling/PluginCompletionUiTool` for independent visual validation without launching the main app / 在主仓库 `Plugins/Tooling/PluginCompletionUiTool` 中新增独立的插件完成可视化校验工具，无需启动主程序即可进行可视化验证

## [3.6.2] - 2026-02-26

### Fixed / 修复

- **Plugin Navigation / 插件导航**: Fixed sidebar plugin navigation to include only installed plugins that provide `IPluginPage`, preventing empty plugin pages
- **Plugin Actions / 插件操作**: Fixed plugin card action visibility and capability probing by separating feature-page and settings-page detection / 修复插件卡片操作可见性与能力探测逻辑，拆分“功能页”和“设置页”判定
- **Plugin Settings Host / 插件设置宿主**: Fixed `PluginSettingsWindow` to support `IPluginPage` settings providers in addition to raw `Page` objects
- **Plugin Implementations / 插件实现**: Fixed official plugin runtime behavior by adding missing UI/settings/optimization capabilities for `custom-mouse`, `network-acceleration`, and `shell-integration` / 修复官方插件运行时行为：为 `custom-mouse`、`network-acceleration`、`shell-integration` 补齐缺失的 UI/设置/系统优化扩展能力

### Improved / 改进

- **Windows Optimization Extensions / 系统优化扩展**: Improved integration flow by surfacing `shell-integration` as a plugin-provided optimization category with executable actions

## [3.6.1] - 2026-02-25

### Added / 新增

- **Dashboard / 控制台**: Added Dashboard navigation item preservation in compatibility mode (--skip-compat-check), allowing users to access CPU/GPU/Battery monitoring on unsupported machines / 在兼容模式（--skip-compat-check）下保留 Dashboard 导航项，允许用户在不支持的机器上访问 CPU/GPU/电池监控
- **Plugin Management / 插件管理**: Added one-click bulk install button to install all currently available online plugins / 新增插件一键安装按钮，可一次安装当前在线可用的全部插件

### Fixed / 修复

- **Plugin Store / 插件商店**: Fixed plugin store URLs and file sizes (Crs10259 SSC-STUDIO, correct file sizes) / 修复插件商店 URL 和文件大小（Crs10259 SSC-STUDIO，正确的文件大小）
- **Localization / 本地化**: Fixed hardcoded "Recommended" text in Windows Optimization view to use localized resource
- **Plugin Configuration / 插件配置**: Fixed plugin configuration button visibility for plugins exposing `GetSettingsPage` / 修复插件配置按钮可见性，支持实现 `GetSettingsPage` 的插件
- **Plugin Configuration / 插件配置**: Added double-click behavior on plugin list items to open plugin settings for installed plugins / 为已安装插件新增列表项双击打开配置页面行为
- **Settings UI / 设置界面**: Fixed inconsistent sidebar shadow rendering across different PCs by replacing the settings navigation selection shadow with a stable highlight-only style / 修复设置页侧边栏阴影在不同电脑上的渲染不一致问题，改为更稳定的高亮样式
- **Settings UI / 设置界面**: Updated the default update repository owner shown in Settings to `SSC-STUDIO` and aligned owner placeholders across languages / 将设置页中更新仓库拥有者默认显示更新为 `SSC-STUDIO`，并同步多语言占位符
- **Plugin Navigation / 插件导航**: Fixed installed plugin sidebar visibility by including installed system plugins in navigation refresh / 修复已安装插件侧边栏可见性，在导航刷新中包含已安装系统插件
- **Plugin Loading / 插件加载**: Fixed plugin discovery and ZIP installation to support both `UniversalDeviceToolkit.Plugins.*.dll` and ID-based DLL names (for example `custom-mouse.dll`) / 修复插件发现与 ZIP 安装逻辑，兼容 `UniversalDeviceToolkit.Plugins.*.dll` 与按插件 ID 命名的 DLL（如 `custom-mouse.dll`）
- **Plugin Manifest Compatibility / 插件清单兼容性**: Fixed legacy `minLLTVersion` compatibility in host manifest parsing and ecosystem metadata alignment / 修复主程序清单解析对旧字段 `minLLTVersion` 的兼容，并对齐插件生态元数据
- **Plugin Download / 插件下载**: Fixed online install failures on GitHub 404 assets by adding multi-URL retry and local package fallback from existing compiled plugin directories / 修复 GitHub 资源 404 导致的在线安装失败，新增多 URL 重试与本地已编译插件目录打包回退机制
- **Plugin Update UX / 插件更新体验**: Fixed update hint visibility and metadata rendering by showing update info only for installed plugins with real updates, hiding empty release/changelog fields, formatting release date, and enabling changelog URL click-through from the update icon
- **Plugin Icon Color / 插件图标颜色**: Fixed plugin icon background color instability across app restarts by replacing non-deterministic hash usage and wiring `store.json` `iconBackground` into plugin cards / 修复插件图标背景色重启后变化不一致的问题：替换非确定性哈希方案，并将 `store.json` 的 `iconBackground` 正式接入插件卡片显示

### Improved / 改进

- **Plugin Store Reliability / 插件商店可靠性**: Added store metadata fallback fetch order (`main` `master`) to reduce branch mismatch failures

## [3.6.0] - 2026-02-25

### Added / 新增

- **Plugin System / 插件系统**: Implemented plugin dependency resolution, sandboxing, hot-reload, and event bus system / 实现插件依赖解析、沙箱、热重载和事件总线系统
- **Plugin System / 插件系统**: Created working plugin examples (CustomMouse, ShellIntegration) with full functionality / 创建可用的插件示例（CustomMouse、ShellIntegration），功能完整
- **Plugin System / 插件系统**: Implemented plugin version checking and update manager with three update strategies / 实现插件版本检查和更新管理器，支持三种更新策略
- **Plugin System / 插件系统**: Added plugin configuration management with user preferences / 添加插件配置管理，支持用户偏好设置
- **Plugin System / 插件系统**: Added plugin update settings (check on startup, auto-download, notification, frequency) / 添加插件更新设置（启动检查、自动下载、通知、频率）
- **Plugin System / 插件系统**: Integrated plugins repository and migrated downloads to releases / 集成插件仓库并将下载迁移到 releases
- **Internationalization / 国际化**: Added multilingual support for CustomMouse and ShellIntegration plugins (13 languages) / CustomMouse 与 ShellIntegration 插件添加多语言支持（13 种语言）
- **Internationalization / 国际化**: Migrated hardcoded Chinese text in XAML files to resource files / XAML 文件中硬编码的中文文本迁移到资源文件
- **Documentation / 文档**: Created comprehensive documentation (ARCHITECTURE.md, DEPLOYMENT.md, SECURITY.md, CODE_OF_CONDUCT.md) / 创建完整文档（ARCHITECTURE.md、DEPLOYMENT.md、SECURITY.md、CODE_OF_CONDUCT.md
- **Documentation / 文档**: Added quick start guide, troubleshooting section to README / README 中添加快速入门指南、故障排查部分
- **Testing Infrastructure / 测试基础璁炬*: Added comprehensive test coverage for PowerModeFeature, BatteryFeature, and plugin features / PowerModeFeature、BatteryFeature 和插件功能添加全面测试覆

### Improved / 改进

- Migrated core projects to target `net10.0-windows` / 将核心项目迁移到 `net10.0-windows`
- Implemented Central Package Management (CPM) for centralized NuGet package version management / 使用中央包管理 (CPM) 集中管理 NuGet 包版本
- Optimized shutdown performance from 8 seconds to 0.35 seconds (23x faster) / 优化关闭性能从 8 秒提升到 0.35 秒（提升23倍）
- Reduced plugin shutdown wait time from 500ms to 200ms / 将插件关闭等待时间从 500ms 减少到 200ms
- Optimized service stop timeout from 8 seconds to 2 seconds / 将服务停止超时从 8 秒优化到 2 秒
- Removed LibreHardwareMonitorLib dependency, simplified CPU voltage reading using WMI / 移除 LibreHardwareMonitorLib 依赖，使用 WMI 简化 CPU 电压读取
- Enhanced plugin management UI with hover effects, author display, and modern "No Results" state / 增强插件管理 UI：悬停效果、作者显示、现代化"无结果"状态
- Added context menu for plugins (open folder, copy ID, uninstall) / 为插件添加右键菜单（打开文件夹、复制 ID、卸载）
- Improved search experience with built-in clear button / 改进搜索体验，增加清除按钮
- Optimized UI transition animations and enabled high-performance animations by default / 优化 UI 切换动画，默认启用高性能动画
- Redesigned plugin item UI with checkmark badge on installed plugins / 重新设计插件列表项 UI：将"已安装"按钮替换为图标上的已安装角标

### Fixed / 修复

- **Security**: Fixed JSON deserialization vulnerability in AbstractSettings.cs / 修复 AbstractSettings.cs 中的 JSON 反序列化安全漏洞
- **Thread Safety**: Fixed race conditions in AbstractSettings.cs and BatteryDischargeRateMonitorService.cs / 修复 AbstractSettings.cs 和 BatteryDischargeRateMonitorService.cs 中的竞态条件
- **Memory**: Fixed memory leaks in MainWindow.xaml.cs and implemented proper IDisposable / 修复 MainWindow.xaml.cs 内存泄漏，实现正确的 IDisposable
- **Plugin System**: Fixed plugin assembly file locking issue enabling updates without restart / 修复插件程序集文件锁定问题，支持无需重启更新
- Fixed Snackbar overlap conflict between Plugin Extensions and Windows Optimization / 修复插件扩展和系统优化界面的 Snackbar 冲突
- Fixed process residue after exit with forced termination mechanism / 修复退出后进程残留问题，引入强制终止机制
- Fixed 404 error when fetching plugins from store / 修复从商店获取插件时的 404 错误
- Fixed XamlParseException in Plugin Extensions page / 修复插件扩展页面的 XamlParseException

## [3.5.1] - 2026-01-29

### Added / 新增

- Safety confirmation dialog before system cleanup operations / 系统清理操作前的安全确认弹窗
- New cleanup items: App leftovers, Chrome/Edge/Firefox browser cache / 新增清理项：应用残留文件、Chrome/Edge/Firefox 浏览器缓存
- Registry redundancy cleanup for recent documents and app usage / 注册表冗余项清理（最近文档、应用使用记录等）
- Large file scanning in user profile folders with customizable size filters / 用户个人文件夹中的大文件扫描功能，支持自定义大小筛选
- One-click "Start All" interaction for driver downloads in System Optimization / 系统优化驱动下载新增"开始安装全部"一键操作

### Improved / 改进

- Redesigned icons for "Select Recommended" and "Clear Selection" in Windows Optimization / 重新设计系统优化页面"选择推荐"和"清除全部"图标
- Instant execution mechanism for optimization items upon checking / 系统优化项勾选后即时生效机制
- Batched Snackbar notifications for multiple optimization actions / 优化批量应用项时的 Snackbar 消息提示，合并显示
- Expanded system cleanup algorithm for better efficiency and coverage / 扩展系统清理算法，提升效率与覆盖范围
- Refactored Cleanup UI: Categories are now always visible, and Scan process is more transparent with a progress bar / 重构清理界面：项目列表始终可见，扫描过程配合进度条更透明
- Enhanced Driver Download UI: Dual-state toggle button (Start/Pause) for better control / 增强驱动下载 UI：采用"开始/暂停"双态切换按钮，提升控制体验
- Enhanced crash reports with memory state and process tree logging / 增强崩溃报告，增加内存状态和进程树记录
- Introduced `ProcessWatcher` for automatic child process lifecycle management / 引入 `ProcessWatcher` 自动管理子进程生命周期
- Optimized detection and cleanup of residual processes during startup / 优化启动时的残留进程检测与清理
- Redesigned plugin item UI with checkmark badge on installed plugins / 重新设计插件列表项 UI：将"已安装"按钮替换为图标上的已安装角标

### Fixed / 修复

- Scan button hover visibility issue in Cleanup page / 修复清理页面扫描按钮悬停时的显示问题
- ShellIntegration plugin compilation errors and namespace conflicts / 修复 ShellIntegration 插件编译错误及命名空间冲突
- Plugin SDK reference issues in ShellIntegration project / 修复 ShellIntegration 项目中的插件 SDK 引用问题
- Resolved process residue after UI crash with a forced exit mechanism / 解决 UI 崩溃后的进程残留问题，引入强制退出机制

## [3.5.0] - 2026-01-28

### Added / 新增

- Two new plugins have been added through the extension. For details, please refer to the CHANGELOG.md of each plugin. / 插件拓展新增两个插件，详情见插件每个插件的CHANGELOG.md
- Real-time power usage display for CPU and GPU in dashboard / 控制台新增 CPU 和 GPU 实时功耗显示
- Detailed model name display for CPU and GPU / CPU 和 GPU 详细型号名称显示
- Double-click interaction to toggle sensor details / 双击传感器卡片切换详情显示
- Plugin configuration management with user preferences / 插件配置管理，支持用户偏好设置
- Multi-language support for plugin interface / 插件界面多语言支持

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Unified dashboard layout merging battery, CPU, and GPU stats / 统一控制台布局，合并电池、CPU 和 GPU 状态
- Enhanced battery status display with progress bars for all metrics / 增强电池状态显示，简略视图所有指标均配有进度条
- Optimized progress bar styling and column spacing in sensors dashboard / 优化传感器控制台的进度条样式和列间距
- GPU clock display logic (Core clock in main view, Memory clock in details) / GPU 频率显示逻辑（主视图显示核心频率，详情显示显存频率）
- Plugin management interface in Windows Optimization settings / Windows优化设置中的插件管理界面
- Better error handling for plugin configuration operations / 插件配置操作的更好错误处理
- Plugin extensions list updates when switching to Extensions tab / 切换到扩展标签页时插件扩展列表更新
- Removed beautification-related code from WindowsOptimizationService and WindowsOptimizationPage / 从WindowsOptimizationService和WindowsOptimizationPage中移除美化相关代码
- Organized working directory: removed unused templates, moved shell integration files to plugin directory / 整理工作目录：删除未使用的模板，将Shell集成文件移动到插件目录
- Refactored shell integration helper usage to instance-based pattern for consistency / 重构Shell集成helper使用为基于实例的模式以确保一致性

### Fixed / 修复

- Corrected plugin metadata and version information / 修正插件元数据和版本信息
- System optimization Extensions tab for managing installed plugins / 系统优化扩展标签页，用于管理已安装的插件
- Plugin Extension ViewModel for better integration with system optimization / 插件扩展ViewModel，更好地与系统集成
- Plugin icon background color mapping for different plugin types / 不同插件类型的图标背景颜色映射
- PluginManager TryGetPlugin method for better plugin discovery / PluginManager TryGetPlugin方法，改进插件发现
- Removed donate functionality and all related UI components / 移除赞助功能及所有相关UI组件
- Added missing ExtensionsNavButton_Checked event handler for plugin tab navigation / 添加了缺失的ExtensionsNavButton_Checked事件处理器，用于插件标签页导航
- Plugin bulk import improvements for compiled plugins (DLL-only packages) / 针对编译插件（仅包含DLL的包）的批量导入改进

## [3.4.1] - 2026-01-24

### Added / 新增

- Plugin Stop interface for safe updates and uninstallation / 插件 Stop 接口，支持安全更新和卸载
- Debug logging for plugin configuration visibility diagnostics / 插件配置可见性诊断的调试日志
- Bulk plugin import functionality / 批量插件导入功能
- Comprehensive multilingual support for plugin bulk import features / 插件批量导入功能的完整多语言支持
- Plugin icon background color support from store.json / 从 store.json 读取插件图标背景颜色支持
- Improved make.bat plugin build commands with local test copy option / 改进 make.bat 插件构建命令，支持本地测试复制选项

### Fixed / 修复

- Plugin update process now stops plugins before updating / 插件更新流程现在会在更新前停止插件
- Configuration button responsiveness with better error handling / 配置按钮响应性及更好的错误处理
- Plugin installation/uninstallation file lock issues / 插件安装/卸载文件锁定问题
- PluginManifestAdapter missing Stop() method implementation / PluginManifestAdapter 缺失 Stop() 方法实现
- Plugin configuration button appearing for uninstalled plugins (with debug logging) / 插件配置按钮出现在未安装插件上的问题（附带调试日志）
- IsInstalled check now verifies plugin files exist on disk / IsInstalled 检查现在会验证插件文件是否存在于磁盘
- BooleanAndConverter safety improvements for null and non-boolean values / BooleanAndConverter 安全性改进，处理 null 和非布尔值
- **Plugin configuration button not responding - completely redesigned implementation** / **插件配置按钮无响应 - 完全重新设计实现**
- **Configuration button visibility logic with HasConfiguration property** / **配置按钮可见性逻辑使用 HasConfiguration 属性**
- **PluginViewModel compilation errors after configuration support changes** / **配置支持更改后的 PluginViewModel 编译错误**
- **ViveTool plugin appearing multiple times due to development folder scanning** / **ViveTool插件因开发文件夹扫描而多次显示**
- **PluginManager.PluginManifestAdapter priority issue - installed plugins showing as online adapters** / **PluginManager.PluginManifestAdapter优先级问题 - 已安装插件显示为在线适配器**
- **UI update loop caused by excessive UpdateAllPluginsUI calls** / **UI更新循环由过多的UpdateAllPluginsUI调用引起**
- **Plugin icon background colors changing on each app launch** / **插件图标背景颜色在每次应用启动时变化**
- **XAML tag mismatch error in PluginExtensionsPage** / **PluginExtensionsPage 中的 XAML 标签不匹配错误**
- **Missing translations for plugin snackbar messages** / **插件 snackbar 消息缺少翻译**
- **Hardcoded English text in plugin UI elements** / **插件 UI 元素中的硬编码英文文本**
- **Resource.Designer.cs missing new plugin resource strings** / **Resource.Designer.cs 缺少新的插件资源字符串**

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Plugin update reliability with proper resource cleanup / 插件更新可靠性及正确的资源清理
- Plugin configuration window error handling / 插件配置窗口错误处理
- Plugin state tracking and installation status validation / 插件状态跟踪和安装状态验证
- **Configuration button click handling with detailed logging** / **配置按钮点击处理及详细日志记录**
- **Plugin configuration support detection with CheckConfigurationSupport method** / **插件配置支持检测使用 CheckConfigurationSupport 方法**
- **Error handling and user feedback for configuration operations** / **配置操作的错误处理和用户反馈**
- **Plugin scanning filter to exclude development folders (obj, bin, Debug, Release)** / **插件扫描过滤器排除开发文件夹（obj、bin、Debug、Release）**
- **Plugin merging logic to prioritize installed plugins over online adapters** / **插件合并逻辑优先选择已安装插件而非在线适配器**
- **Optimized UI update flow to prevent infinite loops** / **优化UI更新流程防止无限循环**
- **Simplified plugin scanning logic - only scan root plugin directories** / **简化插件扫描逻辑 - 仅扫描根目录的插件目录**
- **Plugin icon background colors now read from store.json instead of dynamic generation** / **插件图标背景颜色现在从 store.json 读取，而非动态生成**
- **ViveTool status display moved to configuration page only** / **ViveTool 状态显示仅移至配置页面**
- **Enhanced make.bat with improved plugin build and local test copy functionality** / **增强 make.bat，改进插件构建和本地测试复制功能**

## [3.4.0] - 2026-01-22

### Added / 新增

- Bulk plugin import functionality with progress tracking / 批量插件导入功能及进度跟踪
- Plugin icon background colors in store configuration / 插件商店中图标背景颜色配置
- Comprehensive multilingual support for plugins (ja, ko, de, zh-hant) / 插件完整多语言支持（日语、韩语、德语、繁体中文）
- ViveTool status display and download functionality in plugin settings / 插件设置中的ViveTool状态显示和下载功能
- Plugin localization framework and resource standardization / 插件本地化框架及资源标准化

### Fixed / 修复

- Hard-coded strings in NetworkAcceleration plugin XAML files / NetworkAcceleration 插件 XAML 文件中的硬编码字符串
- Plugin configuration button click handler issues / 插件配置按钮点击处理问题
- Missing multilingual resource keys for UI elements / UI元素缺失的多语言资源键
- Plugin version synchronization with store metadata / 插件版本与商店元数据同步
- Missing Resource.Designer.cs entries for bulk import / 批量导入缺失的 Resource.Designer.cs 条目
- Duplicate resource entries in Resource.resx file / Resource.resx 文件中重复的资源条目
- JSON syntax errors in plugin store preventing online plugin loading / 插件商店JSON语法错误导致无法加载在线插件
- Removed redundant PluginImport plugin from store / 从商店中移除冗余的PluginImport插件
- Plugin uninstall button not updating UI after successful uninstall / 插件卸载成功后卸载按钮UI未更新
- Plugin descriptions not supporting Chinese localization / 插件描述不支持中文本地化
- Bulk import button icon and tooltip unclear / 批量导入按钮图标和提示不清晰
- Implemented proper ZIP file import functionality for local plugins / 实现了本地插件ZIP文件的正确导入功能
- Fixed configure button visibility to require both installed and supports configuration / 修复配置按钮可见性，需要同时安装且支持配置

### Added / 新增

- Plugin state reset functionality with Ctrl+Shift+R shortcut / 添加了插件状态重置功能，使用Ctrl+Shift+R快捷键
- Visual tip about plugin state reset shortcut in UI / 在UI中添加了插件状态重置快捷键的视觉提示

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- ViveTool plugin interface optimization by removing redundant status display / 优化ViveTool插件界面，移除冗余状态显示
- Plugin import workflow with ZIP file validation / 增强插件导入工作流及ZIP文件验证
- Plugin store UI with improved icon display and color coding / 改进插件商店界面图标显示和颜色编码
- Plugin management error handling and user feedback / 插件管理错误处理和用户反馈
- Plugin icon text color now adapts to theme (white in dark mode, black in light mode) / 插件图标文字颜色现在根据主题自动适配（深色模式白色，亮色模式黑色）

---

## [3.3.0] - 2026-01-XX

### Added / 新增

- Complete plugin system with online store and GitHub Actions publishing workflow / 完整的插件系统，包含在线商店和 GitHub Actions 发布工作流
- Network acceleration plugin with traffic statistics and UI improvements / 网络加速插件及流量统计界面改进
- ViveTool plugin integration with v1.3.0 release / ViveTool 插件集成及 v1.3.0 发布
- Plugin SDK for third-party development / 第三方插件开发 SDK

### Fixed / 修复

- Plugin installation permission errors and build issues / 插件安装权限错误和构建问题
- Compilation errors in plugin extensions and related components / 插件扩展及相关组件的编译错误
- Hard-coded strings in NetworkAcceleration plugin XAML files / NetworkAcceleration 插件 XAML 文件中的硬编码字符串

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Plugin UI with card-based layout and multilingual support / 卡片式布局和多语言支持的插件界面
- Plugin store with automatic file hash generation / 自动文件哈希生成的插件商店
- Localization consistency across all plugin components / 所有插件组件的本地化一致性

---

## [3.2.0] - 2026-01-XX

### Added / 新增

- Plugin auto-update functionality with version checking / 插件自动更新功能及版本检查
- Plugin import from compressed files / 从压缩文件导入插件
- Plugin installation with download progress bar / 带下载进度条的插件安装
- Comprehensive plugin multilingual support (ja, ko, de, zh-hant) / 插件完整多语言支持（日语、韩语、德语、繁体中文）

### Fixed / 修复

- Plugin icon loading logic for installed vs uninstalled plugins / 已安装和未安装插件的图标加载逻辑
- Plugin UI layout and interaction issues / 插件界面布局和交互问题

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Plugin details panel with automatic icon generation / 自动图标生成的插件详情面板
- Performance optimizations for plugin loading and management / 插件加载和管理的性能优化
- Plugin resource file organization and maintainability / 插件资源文件组织性和可维护性

---

## [3.2.1] - 2026-01-15

### Fixed / 修复

- Fixed bugs in settings interface / 修复设置界面中的错误

---

## [3.2.0] - 2026-01-14

### Added / 新增

- Updated 'Tools' UI with improved layout / 更新"工具"界面，改进布局
- Added collection utilities for better data handling / 添加集合工具以改进数据处理

---

## [3.1.5] - 2026-01-14

### Added / 新增

- Optimized ViVeTool plugin and updated Settings page navigation style / 优化ViVeTool插件并更新设置页面导航样式

---

## [3.1.3] - 2026-01-13

### Fixed / 修复

- Version bump only - no actual code changes / 仅版本号升级 - 无实际代码变更

---

## [3.1.2] - 2026-01-12

### Fixed / 修复

- Updated tools configuration / 更新工具配置

---

## [3.1.1] - 2026-01-12

### Fixed / 修复

- Version bump only - minor update preparation / 仅版本号升级 - 为小更新准备

---

## [3.1.0] - 2025-11-XX

### Added / 新增

- Categorized settings page navigation / 分类设置页面导航
- Advanced CLI with enhanced functionality / 增强功能的高级命令行工具
- Multiple SSIDs support for WiFi automation triggers / WiFi 自动化触发器支持多个 SSID
- Periodic action automation / 周期性操作自动化

### Fixed / 修复

- Power plan selector in settings / 设置中的电源计划选择器
- User inactivity timer bug / 用户非活动计时器错误
- CLI validator logic and duplicate WTS entries / CLI 验证器逻辑和重复 WTS 条目

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- UI responsiveness and performance / 界面响应性和性能
- Error messages and user feedback / 错误消息和用户反馈
- Hardware detection and compatibility / 硬件检测和兼容性

---

## [3.0.5] - 2026-01-11

### Fixed / 修复

- Installer build configuration in make.bat / make.bat 中的安装程序构建配置
- GitHub Actions workflow permissions / GitHub Actions 工作流权限

---

## [3.0.4] - 2026-01-11

### Fixed / 修复

- GitHub Actions release permissions and updates / GitHub Actions 发布权限和更新

---

## [3.0.3] - 2026-01-11

### Fixed / 修复

- Minor compatibility fixes / 小的兼容性修复

---

## [3.0.2] - 2026-01-11

### Fixed / 修复

- Version bump to 3.0 series / 版本升级到3.0系列

---

## [3.0.1] - 2025-09-XX

### Added / 新增

- .NET 8.0 migration / .NET 8.0 迁移
- Improved error handling and logging / 改进错误处理和日志记录
- Shell integration enhancements / Shell 集成增强

### Fixed / 修复

- ShellIntegration submodule paths and build artifacts / ShellIntegration 子模块路径和构建产物
- Installation and distribution issues / 安装和分发问题

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Performance optimizations / 性能优化
- Code cleanup and refactoring / 代码清理和重构

---

## [2.26.1] - 2023-08-XX

### Fixed / 修复

- Security vulnerability fixes / 安全漏洞修复
- Minor bug fixes and stability improvements / 小错误修复和稳定性改进

---

## [2.26.0] - 2023-08-XX

### Added / 新增

- Final stability improvements before 3.0 migration / 3.0迁移前的最终稳定性改进
- Enhanced hardware support for new Legion models / 对新Legion型号的增强硬件支持

---

## [2.25.3] - 2023-08-XX

### Fixed / 修复

- Critical bug fixes for production stability / 生产环境稳定性的关键错误修复

---

## [2.25.2] - 2023-08-XX

### Fixed / 修复

- Minor bug fixes and performance optimizations / 小错误修复和性能优化

---

## [2.25.1] - 2023-08-XX

### Fixed / 修复

- Stability improvements and crash fixes / 稳定性改进和崩溃修复

---

## [2.25.0] - 2023-08-XX

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Code quality and maintainability improvements / 代码质量和可维护性改进
- Performance optimizations and resource management / 性能优化和资源管理

---

## [2.24.2] - 2023-07-XX

### Fixed / 修复

- Minor bug fixes and stability improvements / 小错误修复和稳定性改进

---

## [2.24.1] - 2023-07-XX

### Fixed / 修复

- Bug fixes for reported issues / 已报告问题的错误修复

---

## [2.24.0] - 2023-07-XX

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Overall system stability and performance / 整体系统稳定性和性能
- User experience enhancements / 用户体验增强

---

## [2.23.1] - 2023-07-XX

### Fixed / 修复

- Critical bug fixes and stability improvements / 关键错误修复和稳定性改进

---

## [2.23.0] - 2023-07-XX

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Performance optimizations and memory management / 性能优化和内存管理
- Enhanced error handling and logging / 增强的错误处理和日志记录

---

## [2.22.2] - 2023-06-XX

### Fixed / 修复

- Security patches and minor bug fixes / 安全补丁和小错误修复

---

## [2.22.1] - 2023-06-XX

### Fixed / 修复

- Bug fixes for stability and compatibility / 稳定性和兼容性的错误修复

---

## [2.22.0] - 2023-06-XX

### Added / 新增

- Performance monitoring improvements / 性能监控改进
- Enhanced hardware detection capabilities / 增强的硬件检测能力

---

## [2.21.3] - 2023-05-XX

### Fixed / 修复

- Critical stability fixes and crash prevention / 关键稳定性修复和崩溃防护

---

## [2.21.2] - 2023-05-XX

### Fixed / 修复

- Minor bug fixes and user experience improvements / 小错误修复和用户体验改进

---

## [2.21.1] - 2023-05-XX

### Fixed / 修复

- Bug fixes for reported issues / 已报告问题的错误修复

---

## [2.21.0] - 2023-05-XX

### Added / 新增

- Advanced fan control improvements / 高级风扇控制改进
- Enhanced system integration features / 增强的系统集成功能

---

## [2.20.2] - 2023-04-XX

### Fixed / 修复

- Minor stability fixes and performance improvements / 小稳定性修复和性能改进

---

## [2.20.1] - 2023-04-XX

### Fixed / 修复

- Bug fixes for user-reported issues / 用户报告问题的错误修复

---

## [2.20.0] - 2023-04-XX

### Added / 新增

- New automation triggers and actions / 新的自动化触发器和操作
- Enhanced RGB lighting effects / 增强的RGB灯光效果

---

## [2.19.0] - 2023-03-XX

### Added / 新增

- Improved system monitoring capabilities / 改进的系统监控功能
- Enhanced user interface responsiveness / 增强的用户界面响应性

---

## [2.18.0] - 2023-02-XX

### Added / 新增

- Additional hardware support for new Legion models / 对新Legion型号的额外硬件支持
- Performance optimizations and bug fixes / 性能优化和错误修复

---

## [2.17.0] - 2023-02-XX

### Added / 新增

- Enhanced automation system features / 增强的自动化系统功能
- Improved system stability and performance / 改进的系统稳定性和性能

---

## [2.16.1] - 2023-08-25

### Fixed / 修复

- Fix resharper warnings / 修复 ReSharper 警告
- Fix #935 / 修复问题 #935
- Fix crash caused by inputting non-digit into color picker input (#934) / 修复颜色选择器输入非数字导致的崩溃 (#934)
- New Crowdin updates (#930) / 新的 Crowdin 更新 (#930)
- New Crowdin updates (#929) / 新的 Crowdin 更新 (#929)

---

## [2.16.0] - 2023-08-24

### Added / 新增

- Final 2.x release with stability improvements / 带??稳定性改进的最终2.x版本
- Enhanced compatibility and performance / 增强的兼容性和性能

---

## [2.15.4] - 2023-07-18

### Fixed / 修复

- Critical stability fixes for production use / 生产使用的关键稳定性修复
- Performance improvements and bug fixes / 性能改进和错误修复

---

## [2.15.3] - 2023-07-18

### Fixed / 修复

- Minor bug fixes and improvements / 小错误修复和改进

---

## [2.15.2] - 2023-07-18

### Fixed / 修复

- Additional bug fixes and stability improvements / 额外的错误修复和稳定性改进

---

## [2.15.1] - 2023-07-12

### Fixed / 修复

- Bug fixes for user-reported issues / 用户报告问题的错误修复
- Stability and performance improvements / 稳定性和性能改进

---

## [2.15.0] - 2023-08-XX

### Added / 新增

- Experimental GPU Working Mode switch / 实验性 GPU 工作模式切换
- Spectrum RGB keyboard backlight control / Spectrum RGB 键盘背光控制
- Panel logo and ports backlight options / 面板标志和端口背光选项
- Boot logo customization / 启动标志自定义
- Advanced fan curve controls / 高级风扇曲线控制

### Fixed / 修复

- Compatibility with various Legion models / 与各种 Legion 型号的兼容性
- Keyboard backlight control issues / 键盘背光控制问题
- Power mode switching stability / 电源模式切换稳定性

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- RGB lighting effects and customization / RGB 灯光效果和自定义
- UI for keyboard and lighting controls / 键盘和灯光控制界面
- Hardware detection and device support / 硬件检测和设备支持

---

## [2.14.0] - 2023-07-XX

### Added / 新增

- GPU overclocking support / GPU 超频支持
- Advanced automation with time-based triggers / 基于时间的触发器高级自动化
- Custom tray icon tooltips / 自定义托盘图标工具提示
- Monitor (dis)connected automation triggers / 显示器连接/断开自动化触发器

### Fixed / 修复

- Runtime exceptions and crashes / 运行时异常和崩溃
- Process listener restart issues / 进程监听器重启问题
- Various UI bugs and inconsistencies / 各种界面错误和不一致

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Performance optimization for sensors / 传感器性能优化
- Notification system and positioning / 通知系统和定位
- Compatibility with newer Windows versions / 与较新 Windows 版本的兼容性

---

## [2.14.3] - 2023-06-26

### Fixed / 修复

- Critical bug fixes and stability improvements / 关键错误修复和稳定性改进

---

## [2.14.2] - 2023-06-24

### Fixed / 修复

- Additional bug fixes and performance improvements / 额外的错误修复和性能改进

---

## [2.14.1] - 2023-06-21

### Fixed / 修复

- Minor bug fixes and user experience improvements / 小错误修复和用户体验改进

---

## [2.13.2] - 2023-05-25

### Fixed / 修复

- Minor stability fixes and performance improvements / 小稳定性修复和性能改进

---

## [2.13.1] - 2023-05-25

### Fixed / 修复

- Bug fixes for user-reported issues / 用户报告问题的错误修复

---

## [2.13.2] - 2023-05-25

### Fixed / 修复

- Additional WiFi automation stability fixes / 额外的WiFi自动化稳定性修复

---

## [2.13.1] - 2023-05-25

### Fixed / 修复

- WiFi automation bug fixes and improvements / WiFi自动化错误修复和改进

---

## [2.13.0] - 2023-06-XX

### Added / 新增

- WiFi connect/disconnect automation actions / WiFi 连接/断开自动化操作
- Resume trigger for automation pipelines / 自动化流水线的恢复触发器
- Battery temperature monitoring and wear level / 电池温度监控和损耗等级
- HWiNFO64 integration for advanced monitoring / HWiNFO64 集成用于高级监控

### Fixed / 修复

- Gaming detection and automation / 游戏检测和自动化
- Power mode synchronization / 电源模式同步
- Various stability and compatibility issues / 各种稳定性和兼容性问题

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Automation pipeline processing / 自动化流水线处理
- Hardware monitoring and sensors / 硬件监控和传感器
- User interface responsiveness / 用户界面响应性

---

## [2.23.1] - 2023-XX-XX

### Fixed / 修复

- Critical stability fixes and performance optimizations / 关键稳定性修复和性能优化

---

## [2.23.0] - 2023-XX-XX

### Added / 新增

- Performance monitoring improvements and system integration / 性能监控改进和系统集成

---

## [2.12.0] - 2023-05-XX

### Added / 新增

- HDR state automation and triggers / HDR 状态自动化和触发器
- Device connected/disconnected automation / 设备连接/断开自动化
- Advanced power plan management / 高级电源计划管理
- Custom boot logo feature / 自定义启动标志功能

### Fixed / 修复

- Display brightness control issues / 显示亮度控制问题
- Power mode indicator errors / 电源模式指示器错误
- Automation pipeline failures / 自动化流水线故障

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- User activity detection / 用户活动检测
- Battery information accuracy / 电池信息准确性
- Overall system performance / 整体系统性能

---

## [2.11.2] - 2023-03-18

### Fixed / 修复

- Critical stability fixes and bug resolutions / 关键稳定性修复和错误解决

---

## [2.11.1] - 2023-03-18

### Fixed / 修复

- Minor bug fixes and performance improvements / 小错误修复和性能改进

---

## [2.11.0] - 2023-04-XX

### Added / 新增

- Multiple SSIDs for WiFi triggers / WiFi 触发器支持多个 SSID
- DPI scale automation / DPI 缩放自动化
- Screen resolution switching automation / 屏幕分辨率切换自动化
- Custom notification positioning / 自定义通知定位

### Fixed / 修复

- Touchpad scrolling performance / 触摸板滚动性能
- Process listener functionality / 进程监听器功能
- Notification display and positioning / 通知显示和定位

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- UI scaling and high DPI support / 界面缩放和高 DPI 支持
- Automation step execution / 自动化步骤执行
- Error handling and user feedback / 错误处理和用户反馈

---

## [2.10.0] - 2023-03-XX

### Added / 新增

- RGB keyboard automation steps / RGB 键盘自动化步骤
- Custom dashboard widgets and groups / 自定义仪表板小部件和分组
- Update available notifications / 更新可用通知
- Battery usage time estimation / 电池使用时间估算

### Fixed / 修复

- Power mode state restoration / 电源模式状态恢复
- GPU controller initialization / GPU 控制器初始化
- Settings import/export functionality / 设置导入/导出功能

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Dashboard customization and layout / 仪表板自定义和布局
- RGB lighting consistency / RGB 灯光一致性
- Overall application performance / 整体应用程序性能

---

## [2.9.1] - 2023-02-08

### Fixed / 修复

- Stability improvements and bug fixes / 稳定性改进和错误修复
- Performance optimizations / 性能优化

---

## [2.9.0] - 2023-02-XX

### Added / 新增

- AI mode with intelligent performance adjustment / AI 模式及智能性能调整
- Advanced fan control with custom curves / 高级风扇控制及自定义曲线
- GPU temperature and utilization monitoring / GPU 温度和利用率监控
- Custom power mode settings / 自定义电源模式设置

### Fixed / 修复

- Hybrid mode switching reliability / 混合模式切换可靠性
- Fan curve application / 风扇曲线应用
- Thermal sensor readings / 温度传感器读数

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Fan control algorithms / 风扇控制算法
- Temperature monitoring accuracy / 温度监控准确性
- System stability under load / 负载下系统稳定性

---

## [2.8.1] - 2023-01-18

### Fixed / 修复

- Minor bug fixes and stability improvements / 小错误修复和稳定性改进
- GPU mode switching reliability / GPU模式切换可靠性

---

## [2.8.0] - 2023-01-XX

### Added / 新增

- Hybrid GPU mode support / 混合 GPU 模式支持
- Advanced power limit controls / 高级功耗限制控制
- Battery health monitoring / 电池健康监控
- Custom automation triggers / 自定义自动化触发器

### Fixed / 修复

- GPU mode switching / GPU 模式切换
- Power limit application / 功耗限制应用
- Battery status reporting / 电池状态报告

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- GPU management and control / GPU 管理和控制
- Power efficiency optimization / 功效优化
- Hardware compatibility detection / 硬件兼容性检测

---

## [2.7.1] - 2022-12-15

### Fixed / 修复

- Automation pipeline reliability improvements / 自动化流水线可靠性改进
- Minor bug fixes and performance tweaks / 小错误修复和性能调整

---

## [2.7.0] - 2022-12-XX

### Added / 新增

- Automation system with pipelines and triggers / 自动化系统及流水线和触发器
- Process start/stop automation / 进程启动/停止自动化
- Time-based automation triggers / 基于时间的自动化触发器
- WiFi network automation triggers / WiFi 网络自动化触发器

### Fixed / 修复

- Application startup and initialization / 应用程序启动和初始化
- Settings persistence and loading / 设置持久化和加载
- UI responsiveness during automation / 自动化期间界面响应性

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Automation performance and reliability / 自动化性能和可靠性
- User interface for automation setup / 自动化设置用户界面
- Error handling in automation pipelines / 自动化流水线中的错误处理

---

## [2.6.5] - 2022-11-06

### Fixed / 修复

- Keyboard backlight control improvements / 键盘背光控制改进
- Stability fixes and performance optimizations / 稳定性修复和性能优化

---

## [2.6.4] - 2022-10-19

### Fixed / 修复

- RGB lighting consistency fixes / RGB灯光一致性修复
- Minor bug fixes and improvements / 小错误修复和改进

---

## [2.6.3] - 2022-10-16

### Fixed / 修复

- Color application and persistence fixes / 颜色应用和持久性修复
- Performance optimizations / 性能优化

---

## [2.6.2] - 2022-09-30

### Fixed / 修复

- Keyboard detection improvements / 键盘检测改进
- Minor stability fixes / 小稳定性修复

---

## [2.6.1] - 2022-09-29

### Fixed / 修复

- RGB control conflicts and errors / RGB 控制冲突和错误
- Initial RGB system stability / 初始RGB系统稳定性

---

## [2.6.0] - 2022-11-XX

### Added / 新增

- RGB keyboard backlight control / RGB 键盘背光控制
- Multiple color zones and effects / 多色彩区域和效果
- Keyboard lighting presets / 键盘灯光预设
- Real-time color picker / 实时颜色选择器

### Fixed / 修复

- RGB control conflicts with Vantage / 与 Vantage 的 RGB 控制冲突
- Keyboard detection and initialization / 键盘检测和初始化
- Color application and persistence / 颜色应用和持久化

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- RGB lighting performance / RGB 灯光性能
- User interface for RGB controls / RGB 控制用户界面
- Hardware compatibility for RGB / RGB 硬件兼容性

---

## [2.5.0] - 2022-10-XX

### Added / 新增

- Package downloader for drivers and utilities / 驱动程序和实用程序包下载器
- System information and warranty display / 系统信息和保修显示
- Advanced compatibility checking / 高级兼容性检查
- Custom notification system / 自定义通知系统

### Fixed / 修复

- Update checking and notifications / 更新检查和通知
- Package download and installation / 包下载和安装
- System information accuracy / 系统信息准确性

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Download management and reliability / 下载管理和可靠性
- User interface for system info / 系统信息用户界面
- Overall application stability / 整体应用程序稳定性

---

## [2.4.1] - 2022-08-16

### Fixed / 修复

- Display configuration issues / 显示配置问题
- Stability improvements and bug fixes / 稳定性改进和错误修复

---

## [2.4.0] - 2022-09-XX

### Added / 新增

- Custom power mode with full control / 完全控制的自定义电源模式
- Advanced CPU and GPU power limits / 高级 CPU 和 GPU 功耗限制
- Temperature-based performance scaling / 基于温度的性能缩放
- Real-time performance monitoring / 实时性能监控

### Fixed / 修复

- Power mode switching reliability / 电源模式切换可靠性
- Performance limit application / 性能限制应用
- Temperature sensor readings / 温度传感器读数

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Power management algorithms / 电源管理算法
- Hardware control precision / 硬件控制精度
- User interface responsiveness / 用户界面响应性

---

## [2.3.1] - 2022-08-04

### Fixed / 修复

- RGB keyboard control improvements and logging / RGB键盘控制改进和日志记录
- Display and power management fixes / 显示和电源管理修复

---

## [2.3.0] - 2022-08-XX

### Added / 新增

- White keyboard backlight control / 白色键盘背光控制
- Microphone mute/unmute automation / 麦克风静音/取消静音自动化
- Display refresh rate control / 显示刷新率控制
- Advanced power plan management / 高级电源计划管理

### Fixed / 修复

- Keyboard backlight detection / 键盘背光检测
- Display configuration issues / 显示配置问题
- Power plan synchronization / 电源计划同步

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Keyboard control reliability / 键盘控制可靠性
- Display management / 显示管理
- Power management integration / 电源管理集成

---

## [2.2.1] - 2022-06-30

### Fixed / 修复

- Color application consistency / 颜色应用一致性
- RGB control reliability / RGB控制可靠性

---

## [2.2.0] - 2022-07-XX

### Added / 新增

- RGB keyboard preset system / RGB 键盘预设系统
- Custom color schemes and effects / 自定义颜色方案和效果
- Keyboard automation integration / 键盘自动化集成
- Enhanced RGB control algorithms / 增强的 RGB 控制算法

### Fixed / 修复

- RGB control conflicts and errors / RGB 控制冲突和错误
- Color application consistency / 颜色应用一致性
- Keyboard detection issues / 键盘检测问题

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- RGB lighting performance / RGB 灯光性能
- User interface for RGB controls / RGB 控制用户界面
- Hardware compatibility / 硬件兼容性

---

## [2.1.1] - 2022-06-25

### Fixed / 修复

- Fix restart after hybrid mode change / 修复混合模式更改后重启问题
- Fix for crash on AMD systems / 修复AMD系统崩溃问题
- Added accent color picker / 添加强调色选择器
- Apply current preset on startup / 启动时应用当前预设

---

## [2.1.0] - 2022-06-XX

### Added / 新增

- System accent color matching / 系统主题色匹配
- Custom themes and appearance settings / 自定义主题和外观设置
- Enhanced UI with WPFUI framework / 使用 WPFUI 框架的增强界面
- Tray icon improvements and actions / 托盘图标改进和操作

### Fixed / 修复

- Theme application and persistence / 主题应用和持久化
- UI rendering and scaling issues / UI 渲染和缩放问题
- Tray icon functionality / 托盘图标功能

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- User interface design and usability / 用户界面设计和可用性
- System integration and consistency / 系统集成和一致性
- Overall visual experience / 整体视觉体验

---

## [2.0.0] - 2022-05-XX

### Added / 新增

- Complete rewrite with WPFUI framework / 使用 WPFUI 框架完全重写
- Modern user interface design / 现代用户界面设计
- Enhanced hardware compatibility / 增强的硬件兼容性
- Advanced power management features / 高级电源管理功能

### Fixed / 修复

- Legacy UI framework limitations / 传统 UI 框架限制
- Hardware control reliability / 硬件控制可靠性
- System integration issues / 系统集成问题

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Application performance and responsiveness / 应用程序性能和响应性
- User experience and workflow / 用户体验和工作流
- Code architecture and maintainability / 代码架构和可维护性

---

## [1.6.0] - 2022-04-XX

### Added / 新增

- Initial RGB keyboard support / 初始 RGB 键盘支持
- Basic color control and presets / 基本颜色控制和预设
- Keyboard detection and initialization / 键盘检测和初始化

### Fixed / 修复

- Keyboard compatibility issues / 键盘兼容性问题
- Color application errors / 颜色应用错误

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Hardware detection accuracy / 硬件检测准确性
- User interface for keyboard controls / 键盘控制用户界面

---

## [1.5.0] - 2022-03-XX

### Added / 新增

- GPU monitoring and control / GPU 监控和控制
- dGPU deactivation support / dGPU 停用支持
- Power mode synchronization / 电源模式同步

### Fixed / 修复

- GPU detection issues / GPU 检测问题
- Power mode switching / 电源模式切换

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- GPU management reliability / GPU 管理可靠性
- Performance optimization / 性能优化

---

## [1.4.0] - 2022-02-XX

### Added / 新增

- Power plan management / 电源计划管理
- Enhanced power mode controls / 增强的电源模式控制
- Windows integration features / Windows 集成功能

### Fixed / 修复

- Power plan synchronization / 电源计划同步
- Mode switching reliability / 模式切换可靠性

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- User interface for power management / 电源管理用户界面
- System integration depth / 系统集成深度

---

## [1.3.0] - 2022-01-XX

### Added / 新增

- GPU activity monitoring / GPU 活动监控
- Enhanced compatibility detection / 增强的兼容性检测
- Additional device support / 额外设备支持

### Fixed / 修复

- GPU monitoring accuracy / GPU 监控准确性
- Compatibility detection / 兼容性检测

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- Hardware support breadth / 硬件支持广度
- Monitoring reliability / 监控可靠性

---

## [1.2.0] - 2021-12-XX

### Added / 新增

- Basic automation features / 基本自动化功能
- Process monitoring / 进程监控
- Settings persistence / 设置持久化

### Fixed / 修复

- Application stability / 应用程序稳定性
- Settings loading / 设置加载

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- User experience / 用户体验
- System integration / 系统集成

---

## [1.1.0] - 2021-11-XX

### Added / 新增

- Power mode controls / 电源模式控制
- Basic hardware monitoring / 基本硬件监控
- System tray integration / 系统托盘集成

### Fixed / 修复

- Initial stability issues / 初始稳定性问题
- Hardware detection / 硬件检测

### Improved / 改进

- Fixed layout column index error and restricted maximum width of the details panel in Windows Optimization to prevent content from obscuring the left-side area
Added persistence for selection state and page mode in Windows Optimization / - 增加系统优化界面的选择状态和模式记忆功能
- User interface / 用户界面
- System compatibility / 系统兼容性

---

## [1.0.0] - 2021-10-XX

### Added / 新增

- Initial release of Lenovo Legion Toolkit / Lenovo Legion Toolkit 初始版本
- Basic power mode switching / 基本电源模式切换
- Hardware compatibility detection / 硬件兼容性检测
- User interface for Legion devices / Legion 设备用户界面

---

## Migration Guide / 迁移指南

### From 2.x to 3.x / 2.x 3.x

- Backup your settings before upgrading / 升级前备份您的设置
- Some automation features have been redesigned / 某些自动化功能已重新设计
- Plugin system replaces old tools functionality / 插件系统替换旧工具功能

### From 1.x to 2.x / 1.x 2.x

- Complete UI overhaul / 完整的 UI 改造
- Settings migration required / 需要设置迁移
- Enhanced hardware support / 增强的硬件支持

---

## Support / 支持

- **GitHub Issues**: [Report bugs and request features](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/issues)
- **Discord**: [Community support and discussions](https://discord.com/invite/legionseries)

---

## Contributors / 贡献者

Thanks to everyone who has contributed to this project!
感谢所有为这个项目做出贡献的人！

- Main developer: BartoszCichecki / 主要开发者：BartoszCichecki
- Community contributors and translators / 社区贡献者和翻译者
- Beta testers and feedback providers / Beta 测试者和反馈提供者

---

*This changelog follows the format established by [Keep a Changelog](https://keepachangelog.com/).*
*此更新日志遵循 [Keep a Changelog](https://keepachangelog.com/) 建立的格式。
