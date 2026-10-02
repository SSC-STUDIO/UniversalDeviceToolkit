# Scripts & Tools

`Scripts/` 是面向 CI/发布与本地开发的 PowerShell 工具集（Windows PowerShell 5.1 + PowerShell 7 兼容）；`Tools/` 是独立的开发验证与翻译管线。两者都不进发布包，由 `Scripts/Assert-ShippingPayload.ps1` 在打包阶段拦截。

> 约定：所有脚本支持 `powershell -ExecutionPolicy Bypass -File Scripts/<name>.ps1 -Help`（`Get-Help`）查看参数；仓库根为工作目录；CI 与 `Release.yml`/`Ci-tests.yml` 为权威调用示例。

## 索引

| 脚本 | 归类 | 一句话 | 何时用 |
| --- | --- | --- | --- |
| [Install-UdtSkill.ps1](#install-udtskillps1) | 开发辅助 | 把 `Docs/Skills/udt-hardware-cli` 分发到 Cursor/Claude/Codex/opencode | `git pull` 后更新技能、给 Agent 装 `udt` 能力 |
| [Run-TestFailFast.ps1](#run-testfailfastps1) | 本地快检 | 与 CI 一致的快检层：Contracts → Fast | 提交前本地自检 |
| [Test-WindowsTestEnvironment.ps1](#test-windowstestenvironmentps1) | 本地快检 | 校验 Windows 有状态测试的前置（.NET 10、临时文件、HKCU） | 跑 `Tests.Stateful` 前，CI 预检 |
| [Test-CrossPlatformInWsl.ps1](#test-crossplatforminwslps1) | 本地快检 | 经 WSL 编译并测试跨平台面（Linux TFM） | 在 Windows 上验证 Linux 行为 |
| [Assert-CultureNaming.ps1](#assert-culturenamingps1) | CI 门禁 | 强制 BCP 47 规范文化名（`zh-Hans` 而非 `zh-hans`） | CI 必过；本地改 resx/目录后自检 |
| [Assert-ShippingPayload.ps1](#assert-shippingpayloadps1) | CI/发布门禁 | 拦截发布包中的测试/验证残留（`*.Tests*`、`Tools/`、`*.pdb`、`UDT_APPDATA_OVERRIDE`） | 打包后、发布前必过 |
| [Assert-AuthenticodeSignatures.ps1](#assert-authenticodesignaturesps1) | 发布门禁 | 校验 `exe`/`dll` 的 Authenticode 签名有效 | Release 签名后验证 |
| [Prune-ShippingFootprint.ps1](#prune-shippingfootprintps1) | 发布裁剪 | 删除 `*.pdb`、非 `win-x64` 原生、`AllowedCultures` 之外的卫星资源 | `dotnet publish` 后、打包前 |
| [Build-CrossPlatformCliAsset.ps1](#build-crossplatformcliassetps1) | 发布组装 | 发布 `UniversalDeviceToolkit.CrossPlatform` 并打 `*_CLI_cross-platform.zip` | Release 可选（≥5.x） |
| [Build-LanguageAssets.ps1](#build-languageassetps1) | 发布组装 | 从 Host 卫星资源生成语言包与目录，并收尾 `release-assets/` | Release 多阶段（Host 后、收尾） |
| [Build-ElectronInstaller.ps1](#build-electroninstallerps1) | 发布组装 | 构建 Electron Full/Online 载荷与 NSIS 安装器（支持分阶段签名） | 本地 `BuildInstaller/` 或 Release 三阶段 |
| [New-ReleaseNotes.ps1](#new-releasenotesps1) | 发布收尾 | 从 `CHANGELOG.md` 抽取版本段生成 `release-notes.md` | Release 生成说明时 |

`Tools/` 见下方 [TOOLS](#tools) 小节。

---

## Scripts 详情

### Install-UdtSkill.ps1

分发可安装的 Agent Skill（`udt` 硬件控制）。

```powershell
powershell -ExecutionPolicy Bypass -File Scripts/Install-UdtSkill.ps1
powershell -ExecutionPolicy Bypass -File Scripts/Install-UdtSkill.ps1 -DryRun
powershell -ExecutionPolicy Bypass -File Scripts/Install-UdtSkill.ps1 -All   # 强制创建父目录
```

目标目录（不存在则跳过，除非 `-All`）：

- `%USERPROFILE%\.cursor\skills\udt-hardware-cli\`
- `%USERPROFILE%\.claude\skills\udt-hardware-cli\`
- `%USERPROFILE%\.codex\skills\udt-hardware-cli\`
- `%USERPROFILE%\.config\opencode\skills\udt-hardware-cli\`

验证：`udt doctor --json`（`udt-cli` 为一代别名）

### Run-TestFailFast.ps1

```powershell
pwsh ./Scripts/Run-TestFailFast.ps1                 # Release 配置
pwsh ./Scripts/Run-TestFailFast.ps1 -Configuration Debug
pwsh ./Scripts/Run-TestFailFast.ps1 -NoBuild        # 已构建时跳过编译
```

等价于 `Ci-tests.yml` 的前两层：`Tests.Contracts` → `Fast.Tests`。通过后再跑完整 `dotnet test`。

### Test-WindowsTestEnvironment.ps1

```powershell
pwsh ./Scripts/Test-WindowsTestEnvironment.ps1
```

校验：Windows 系统、.NET 10 SDK、`%TEMP%` 读写、`HKCU` 读写。失败则 `Tests.Stateful` 不可跑。

### Test-CrossPlatformInWsl.ps1

```powershell
powershell -ExecutionPolicy Bypass -File Scripts/Test-CrossPlatformInWsl.ps1
powershell -ExecutionPolicy Bypass -File Scripts/Test-CrossPlatformInWsl.ps1 -Distro Ubuntu -Configuration Release
```

要求本机已安装 WSL 发行版且内含 .NET 10。脚本不自动安装发行版。

### Assert-CultureNaming.ps1

```powershell
pwsh ./Scripts/Assert-CultureNaming.ps1
pwsh ./Scripts/Assert-CultureNaming.ps1 -RepositoryRoot C:\repo
```

规则见 `CONTRIBUTING.md` 10.1：语言小写、Script 首字母大写、Region 大写（`zh-Hans` / `pt-BR` / `uz-Latn-UZ`）。覆盖资源文件名、`Directory.Build.props`、`crowdin.yml`、`Build-LanguageAssets.ps1`、C# `LocalizationCatalog`、安装器语言列表等。

### Assert-ShippingPayload.ps1

```powershell
pwsh ./Scripts/Assert-ShippingPayload.ps1 -PayloadPath Build
pwsh ./Scripts/Assert-ShippingPayload.ps1 -PayloadPath Build-CrossPlatformCli
```

禁入：`SpectrumTester*`、`*.Tests*`、`*.Smoke*`、路径段 `Tools`/`Tests`/`x86`/`arm64`、`*.pdb`、`UDT_APPDATA_OVERRIDE`（`Lib.Abstractions.dll` 豁免）。

### Assert-AuthenticodeSignatures.ps1

```powershell
pwsh ./Scripts/Assert-AuthenticodeSignatures.ps1 -Path Build
pwsh ./Scripts/Assert-AuthenticodeSignatures.ps1 -Path UniversalDeviceToolkit.Host/publish/win-x64
```

`Release.yml` 在 Host 载荷、Electron 载荷与最终安装器签名后各调用一次。

### Prune-ShippingFootprint.ps1

```powershell
pwsh ./Scripts/Prune-ShippingFootprint.ps1 -PayloadPath Build
pwsh ./Scripts/Prune-ShippingFootprint.ps1 -PayloadPath UniversalDeviceToolkit.Host/publish/win-x64 -RuntimeIdentifier win-x64 -AllowedCultures 'ar;bg;cs;de;el;en;es;fr;hu;it;ja;lv;nl-nl;pl;pt;pt-br;ro;ru;sk;tr;uk;uz-latn-uz;vi;zh-hans;zh-hant'
```

删除：`*.pdb`、非 `win-x64` 目录（`x86`/`arm64`/`libMonoPosixHelper*`）、`AllowedCultures` 之外的卫星资源。`AllowedCultures` 为空时不过滤语言。

### Build-CrossPlatformCliAsset.ps1

```powershell
pwsh ./Scripts/Build-CrossPlatformCliAsset.ps1 -Version 6.0.0 -ReleaseOutput release-assets -SkipHashUpdate
pwsh ./Scripts/Build-CrossPlatformCliAsset.ps1 -Version 6.0.0 -AssetVersion 6.0.0-preview.1 -ReleaseOutput release-assets
```

发布 `UniversalDeviceToolkit.CrossPlatform`（`net10.0`、`AnyCPU`）到 `Build-CrossPlatformCli/`，注入 `udt`/`udt.cmd` 启动器与 `README.txt`，校验后打 `UniversalDeviceToolkit_v<Version>_CLI_cross-platform.zip` 并（可选）追加 `SHA256.txt`。仅 `≥5.x` 允许发布。

### Build-LanguageAssets.ps1

```powershell
# Host 后、安装器前：生成语言包与目录
pwsh ./Scripts/Build-LanguageAssets.ps1 -BuildDir Build -HostBuildDir UniversalDeviceToolkit.Host/publish/win-x64 -OnlineBuildDir Build-English -ReleaseOutput release-assets -PagesOutput release-assets/pages -Version 6.0.0

# 收尾：写入最终安装器/ZIP 校验并完成目录
pwsh ./Scripts/Build-LanguageAssets.ps1 -FinalizeOnly -ReleaseOutput release-assets -PagesOutput release-assets/pages -Version 6.0.0 -FullInstallerPath BuildInstaller/UniversalDeviceToolkitSetup.exe -OnlineInstallerPath BuildInstaller/UniversalDeviceToolkitOnlineSetup.exe -FullZipPath BuildInstaller/UniversalDeviceToolkit_v6.0.0_Full_win-x64.zip -OnlineZipPath BuildInstaller/UniversalDeviceToolkit_v6.0.0_Online_win-x64.zip -IncludeCrossPlatformCli
```

`AllowedCultures` 与 `Directory.Build.props` 的 `UdtSatelliteResourceLanguages` 保持一致。

### Build-ElectronInstaller.ps1

```powershell
# 本地一键（全流程）
pwsh ./Scripts/Build-ElectronInstaller.ps1 -Version 6.0.0

# Release 三阶段（配合签名）
pwsh ./Scripts/Build-ElectronInstaller.ps1 -Version 6.0.0 -PreparePayloadsOnly
# ...签名...
pwsh ./Scripts/Build-ElectronInstaller.ps1 -Version 6.0.0 -PrepareInstallerShellOnly
# ...签名...
pwsh ./Scripts/Build-ElectronInstaller.ps1 -Version 6.0.0 -PackagePreparedPayloads
```

前提：`UniversalDeviceToolkit.Host/publish/win-x64` 已就绪（`Release.yml` 先 `dotnet publish Host`）。产物：`BuildInstaller/UniversalDeviceToolkitSetup.exe`（Full，离线）、`BuildInstaller/UniversalDeviceToolkitOnlineSetup.exe`（Online，`≤15 MB` stub + `*.nsis.7z`）、对应 ZIP；`BuildInstallerPayload/full|online|installer-shell|nsis` 为已签名中间树。`Version` 支持 `6.0.0` 与 `6.0.0-preview.1` 等 SemVer。

### New-ReleaseNotes.ps1

```powershell
pwsh ./Scripts/New-ReleaseNotes.ps1 -Version 6.0.0 -ChangelogPath CHANGELOG.md -AssetNames @("UniversalDeviceToolkitSetup.exe","UniversalDeviceToolkit_v6.0.0_SHA256.txt") -OutputPath release-assets/release-notes.md
```

按 `## [X.Y.Z] - YYYY-MM-DD` 抽取 `CHANGELOG.md` 对应段，UTF-8 无 BOM 读取。

---

## TOOLS

### CheckSourceUnicode — 源码 Unicode 卫生

`Tools/CheckSourceUnicode/check-unicode.mjs` 扫描零宽/混淆空白/全角 ASCII/同形字符（AI 水印常见载体）。

```powershell
node Tools/CheckSourceUnicode/check-unicode.mjs            # 全仓
node Tools/CheckSourceUnicode/check-unicode.mjs Docs        # 指定目录
node Tools/CheckSourceUnicode/check-unicode.mjs Tools/I18nTranslate
```

规则见 `AGENTS.md`「字符编码与 AI 水印防污染」与 `Tools/CheckSourceUnicode/README.md`。CI 不直接跑本工具，但提交前检出即需修复。

### I18nTranslate — resx 批量翻译管线

`Tools/I18nTranslate/` 基于本地 `llama-server`（OpenAI 兼容）批量翻译 `crowdin.yml` 声明的 resx。

```powershell
.\Tools\I18nTranslate\i18n-translate.ps1                      # 全量增量翻译
.\Tools\I18nTranslate\i18n-translate.ps1 -Locales hi,sw       # 试点
.\Tools\I18nTranslate\i18n-translate.ps1 -DryRun              # 预览
.\Tools\I18nTranslate\i18n-translate.ps1 -ParallelJobs 4
```

前置：`LocalAI-Studio/local-llm/start-ai.ps1 -Model translategemma|gemma4-e4b` 与 `bench.ps1` 健康检查。配置：`locales.txt`（引擎路由）、`prompts.json`/`glossary.json`（术语与语族模板）、`build-prompt-pack.py`（合并 `_agent_out/` 草稿）。详情见 `Tools/I18nTranslate/README.md`。

### HardwareValidation — 真机验证

`Tools/HardwareValidation/` 为开发期真机校验，不进发布包。

```powershell
.\Tools\HardwareValidation\Run-PerformanceEffectVerification.ps1 -RepoRoot . -TimeoutSeconds 240
```

UAC 提权后校验：UI 功耗模式点击→回读 `SmartFanMode`、God Mode 批量写入→回读→还原、直接 `SmartFanMode` 写入→回读→还原。结果落 `PerformanceEffectVerification-*.result.txt`。

`Run-HardwareValidationElevated.ps1` 是上面脚本调用的底层入口：以 UAC 提权运行同目录的 `HardwareValidation` 控制台程序（引用 `UniversalDeviceToolkit.Lib`），按 `-Scenario StatusCheck|CpuVerify|BatchDefault|PowerModeVerify` 执行 God Mode / 功耗模式的写入—回读—还原，并把结果写到 `-ResultPath`。

---

## 与 CI/发布的对应

| 场景 | 脚本链 |
| --- | --- |
| 本地提交前快检 | `Run-TestFailFast.ps1` → `node Tools/CheckSourceUnicode/check-unicode.mjs` → `Assert-CultureNaming.ps1` |
| Windows 有状态测试前 | `Test-WindowsTestEnvironment.ps1` |
| 跨平台验证 | `Test-CrossPlatformInWsl.ps1` |
| 本地一键发布（未签名） | `dotnet publish Host --self-contained win-x64` → `Prune-ShippingFootprint.ps1` → `Build-LanguageAssets.ps1` → `Build-ElectronInstaller.ps1 -Version X.Y.Z` |
| Release 三阶段 | `Release.yml` 按 `PreparePayloadsOnly` → `PrepareInstallerShellOnly` → `PackagePreparedPayloads` 调用 `Build-ElectronInstaller.ps1`，中间穿插 `Assert-ShippingPayload.ps1` / `Assert-AuthenticodeSignatures.ps1` |

更完整的构建与发布流程见 [DEPLOYMENT.md](./DEPLOYMENT.md)；测试分层见 [TEST_DIAGNOSTICS.md](./TEST_DIAGNOSTICS.md)。
