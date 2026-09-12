<img height="128" align="left" src="Assets/Logo.png" alt="Logo">

# Universal Device Toolkit

[![CI Tests](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/actions/workflows/Ci-tests.yml/badge.svg?branch=master)](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/actions/workflows/Ci-tests.yml)
[![GitHub release](https://img.shields.io/github/v/release/SSC-STUDIO/UniversalDeviceToolkit?color=blue)](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest)
[![GitHub stars](https://img.shields.io/github/stars/SSC-STUDIO/UniversalDeviceToolkit?style=social)](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/stargazers)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![GitHub downloads](https://img.shields.io/github/downloads/SSC-STUDIO/UniversalDeviceToolkit/total)](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases)

> **开源 · 不用账号 · 不碰遥测**
>
> 为受支持的联想拯救者 / LOQ 管理性能模式、键盘灯、显卡模式和电池养护。UDT 是基于 Lenovo Legion Toolkit 的独立 GPL-3.0 项目。无需账号、不收集遥测；可用控制取决于机型、固件和驱动。

<div align="center">

[![从 GitHub Releases 下载](https://img.shields.io/badge/下载-GitHub%20Releases-2ea44f?style=for-the-badge&logo=github&logoColor=white)](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest)

**安装：** [Releases](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest)

<a href="https://github.com/SSC-STUDIO/UniversalDeviceToolkit"><img src="Assets/Screenshot_zh-hans.png" width="700" alt="UDT 控制台：传感器、性能模式与混合模式（简体中文深色界面）" /></a>

<sub>UDT 简体中文深色控制台。如果它对你有用，欢迎点击仓库右上角 Star，方便以后找到更新；也欢迎带机型和 Windows 版本到 <a href="https://github.com/SSC-STUDIO/UniversalDeviceToolkit/issues">Issues</a> 反馈兼容性。</sub>

</div>

### 宣传片

<div align="center">

<a href="Assets/UDT_Promo_zh.mp4"><img src="Assets/UDT_Promo_poster.jpg" width="700" alt="通用设备工具箱 30 秒宣传片" /></a>

<sub>我们自己剪的 30 秒中文旁白：开源、不用账号、不碰遥测。<a href="Assets/UDT_Promo_zh.mp4">播放 MP4</a> · <a href="README.md">English</a></sub>

</div>

---

#### 其他语言版本
* [English README](README.md)

---

Universal Device Toolkit（UDT，原 Lenovo Legion Toolkit）是一款轻量级 Windows 设备工具：在受支持的机器上直接做硬件控制。不支持的 PC 进入**基础模式**，硬件开关保持隐藏，界面不会假装有 Vantage 级能力。

> [!NOTE]
> **「Universal」的含义**
> UDT 是聚焦硬件控制的工具，并带安全降级：**完整硬件控制**面向联想拯救者、LOQ、IdeaPad Gaming 等受支持机型，以及已有可测 provider 的其他品牌；**基础模式**隐藏不支持的硬件项，而不是承诺每台电脑都有 Vantage 级控制。名称不是「通用 Windows 工具平台」的意思。

### 为什么选择 UDT？

| | UDT | Lenovo Vantage |
|---|:---:|:---:|
| 后台服务 | **无** | 需要 |
| 遥测 / 联想账号 | **无** | 需要 |
| 开源（GPL-3.0） | **是** | 否 |
| 指针与光标定制 | **内置** | 有限 |
| 命令行与自动化 | **是** | 否 |
| 非联想 PC 可用 | **基础模式** | 否 |

> 插件系统已在 **6.1** 退役：Custom Mouse 成为内置的指针与光标控制，网络加速自 5.0 起内置于系统优化；详见 [插件系统（已退役）](#插件系统已退役)。

**适合谁**

- 拯救者 / LOQ 用户，想卸 Vantage 但还要 Fn+Q、RGB、独显控制
- 不支持的机器：希望硬件项被诚实隐藏（基础模式），而不是假开关
- 人和 Agent 共用 `udt`（应用在跑，设置里打开 CLI；`udt-cli` 仍为兼容别名），代码是 GPL 开源能自己看

现成发帖文案：[PROMOTION_CN.md](Docs/Promotion/PROMOTION_CN.md) · [PROMOTION_EN.md](Docs/Promotion/PROMOTION_EN.md) · 投放手册 [COMMUNITY_OUTREACH.md](Docs/Promotion/COMMUNITY_OUTREACH.md)

本仓库是在 GPL-3.0 许可下持续维护的独立项目，重点覆盖兼容性修复、安全加固、CI/发布自动化、新机型识别与 Windows 兼容维护。旧版 Lenovo Legion Toolkit 用户升级后可保留设置与数据；包管理器身份在 6.x 断代（winget 改为 `SSC-STUDIO.UniversalDeviceToolkit`，Scoop 改为 `universaldevicetoolkit`），旧包 ID 不支持原地升级。正式产品以 **Windows 为先**：GitHub Releases 发布 Windows NSIS Full/Online 安装包，内嵌自包含 win-x64 Host。macOS 与 Linux 为**实验面**（Electron 壳、可移植 Host、CrossPlatform 诊断 CLI），在对应发布流水线落地前**没有官方 Electron 发行包**。Android 和移动端伴侣应用不在项目范围内，也不受支持。

本软件不运行后台服务，典型内存约 400MB（Electron 界面 + .NET Host；托盘空闲更低），不收集用户信息。

### 功能一览

| 功能 | 说明 |
|:---|:---|
| 🔥 **性能与功耗** | Fn+Q 性能模式、自定义模式风扇曲线、CPU/GPU 功耗上限 |
| 🌈 **RGB 与灯效** | Spectrum 逐键、四区 RGB、白光键盘、启动 Logo |
| 🎮 **显卡控制** | 独显开关、MUX、超频、关闭独显 |
| 🔌 **电池养护** | 保养模式、充电阈值（60%/80%）、健康度分级与损耗率实时分析 |
| 🧹 **系统与缓存清理** | 一键清理显卡着色器（DirectX/Vulkan）、微信/QQ 缓存及开发者包管理器缓存 |
| 🎛️ **托盘控制中心** | 任务栏托盘弹出微型控制台，内嵌电源模式胶囊切换器与电量徽章 |
| ⚡ **动作** | 自动化管线与宏统一在一个工作区，支持 JSON/剪贴板快速导入导出 |
| 🖥️ **传感器** | CPU/GPU 温度、风扇转速、频率监测 |
| 🖱️ **指针与光标** | 高分屏自定义光标主题、随 UI 缩放调节光标大小（内置） |
| 🌍 **25 种语言** | 完整本地化 + 社区翻译 |
| 📦 **极致轻量** | 托盘空闲深度休眠、无后台常驻服务、无遥测、无账号 |

### ⚡ 为什么选用 Electron？它真的臃肿吗？（架构与深度性能优化揭秘）

不少开发者与玩家对基于网页技术（Electron / Chromium）的桌面客户端存在固有偏见，认为其“必定动辄消耗数 G 内存、冷启动慢、掉帧卡顿”。

然而，**架构设计与工程调优的深度决定了软件的最终表现**。UDT 采用了 **现代化 Electron 前端 + 无窗口自包含 .NET 10 后端 (Headless Host)** 的前后端分离解耦架构，并实施了极为严苛的性能控制与专项优化：

#### 1. 前后端职责高度清晰，各展所长
- **前端（Electron + React 19 + TypeScript）**：仅专注负责高精度像素渲染、Windows 11 Mica 亚克力动态流光材质、跨 DPI 高清缩放与 25 种语言热切换。
- **后端（.NET 10 / C# 13 无头宿主进程）**：所有底层硬件访问（WMI/ACPI、内核驱动直通、电源策略交互、传感器数据流轮询与自动化管线引擎）全部在原生高性能 .NET 运行时内执行，通过基于 stdio 的极速 JSON-RPC 与前端通讯。

#### 2. UDT 专属的五大底层性能优化
- 🍃 **托盘休眠「零内存伪装」机制 (Zero-Memory Tray Sleeping)**：
  不同于大多数软件将窗口“隐藏”在后台仍保持完整 DOM 树与渲染进程，当 UDT 最小化或关闭到托盘时，主进程会**彻底销毁 (Destroy) 主窗口与 Chromium 渲染实例**；托盘弹窗更是采用毫秒级空闲自动卸载策略。应用常驻后台时内存占用降至最低，不抢占任何前台游戏与生产力资源。
- ⚡ **亚秒级页面就绪响应 (Sub-400ms Median Ready Latency)**：
  所有页面模块按路由懒加载，图表引擎按需引入，页面切换到完全交互就绪的中位数耗时控制在 **≤ 400ms**。
- 🎯 **高频热路径零冗余分配 (Hot-Path Zero Allocation)**：
  对于每秒刷新的传感器图表、仪表盘与列表渲染，静态 ECharts 配置与 DOM 结构通过 `useMemo` 与静态缓存深度复用，数据更新走增量通道，坚决避免渲染循环内重复创建对象导致的垃圾回收（GC）停顿。
- 📦 **严格图分析与按需按路由拆包 (Strict Tree Shaking & Bundle Pruning)**：
  使用 `electron-vite` 进行严谨的依赖图裁剪，对 7000+ Fluent 图标库进行逐个模块按需导入，杜绝整体引入；页面与语言包均走异步动态加载。
- 🛡️ **无常驻 Windows 服务，无后台遥测 (Zero Services, Zero Telemetry)**：
  不安装任何常驻后台的 Windows Service，不向任何服务器发送遥测数据，进程随退随止。

#### 3. 实测性能与对比测试（UDT 深度优化版 vs 传统 WPF 版 vs 官方电脑管家）

| 评测维度 (Metric) | 传统 WPF 版本 (Legacy WPF) | 官方联想电脑管家/Vantage | UDT 6.0 (深度优化 Electron) | 优势评估 (Outcome) |
|---|:---:|:---:|:---:|:---|
| **常驻后台 Windows 服务** | 0 个 | 3~5 个常驻服务 | **0 个 (无常驻服务)** | 绝不拖慢系统开机与后台游戏 |
| **冷启动首屏就绪时间** | 1.8s ~ 2.5s | 4.0s ~ 8.0s+ | **≤ 400ms (中位数)** | **媲美 VS Code 级敏捷秒开** |
| **托盘后台常驻内存** | 150MB ~ 250MB | 300MB ~ 600MB+ | **30MB ~ 60MB (彻底销毁 DOM)** | **远低于 WPF 版本（降幅超 70%）** |
| **前台活跃峰值内存** | 180MB ~ 300MB | 500MB ~ 1.2GB | **30MB ~ 300MB (实地测得)** | **随页面波动，仍远低于 Vantage** |
| **多语言热重载能力** | 需重启应用生效 | 需重新加载 | **毫秒级热切换 (25 种语言)** | 真正的跨语言现代化体验 |
| **UI 缩放与高 DPI 适配** | 易产生字体发虚/布局变形 | 较差 | **矢量像素级缩放 (80%~150%)** | 跨 2K/4K/OLED 屏幕清晰细腻 |

<details>
<summary>更多截图</summary>

| 英文界面（深色） | 简体中文界面（深色） |
|---|---|
| ![Main](Assets/Screenshot_main.png) | ![Chinese](Assets/Screenshot_zh-hans.png) |

</details>

&nbsp;

# 目录
  - [宣传片](#宣传片)
  - [为什么选择 UDT？](#为什么选择-udt)
  - [功能一览](#功能一览)
  - [免责声明](#免责声明)
  - [下载](#下载)
  - [兼容性](#兼容性)
  - [功能介绍](#功能介绍)
  - [插件系统（已退役）](#插件系统已退役)
  - [FAQ](#faq)
  - [如何开启记录 Log](#如何开启记录-log)
  - [本地化翻译](#本地化翻译)
  - [文档索引](#文档索引)
  - [贡献此项目](#贡献此项目)
  - [截图说明](#截图说明)

## 免责声明

**本软件非联想官方出品软件，使用需要自担风险。**

请仔细耐心地阅读本文档，以便了解关于本软件的一些重要信息。

> [!TIP]
> 如果你正在寻找一个 Lenovo Vantage 在 Linux 系统下的替代品，请查看 [LenovoLegionLinux](https://github.com/johnfanv2/LenovoLegionLinux) 项目。

## 下载

请认准当前维护仓库 `SSC-STUDIO/UniversalDeviceToolkit` 的发布页下载版本。6.x 起包管理器断代：winget 改用 `SSC-STUDIO.UniversalDeviceToolkit`，Scoop 改用 `universaldevicetoolkit`；旧包 ID 不支持原地升级。

> [!NOTE]
> **当前稳定版：v6.1.1。** 主程序使用 `vX.Y.Z` 标签发布。历史插件目录发布（`plugin-catalog` / `plugin-catalog-preview`）仅作归档——插件系统已在 6.1 退役，宿主不再读取。
> **winget 说明：** 6.x 包 ID `SSC-STUDIO.UniversalDeviceToolkit` 已预留，但尚未合入 microsoft/winget-pkgs，因此目前 `winget install` 会失败；旧的 Lenovo Legion Toolkit 包同样不支持原地升级。请先使用 Releases。

- **GitHub Releases**：从 [Releases](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest) 下载最新版 Full 或 Online 安装包。**Full** 是完整离线 NSIS 安装器（内嵌 Electron 与自包含 .NET Host）。**Online** 是约 15MB 以内的分阶安装器，安装时再下载同一套运行时；语言包和机型包仍可在应用内目录安装。请始终安装最新版本；设置与数据会自动迁移，包管理器安装需改用 6.x 新 ID。
- ~~**winget**（待上架）~~：6.x 标识 `SSC-STUDIO.UniversalDeviceToolkit` 已预留，提交 winget-pkgs 后才会可用；旧包不支持原地升级。
- ~~**Scoop**~~：`SSC-STUDIO/scoop-bucket` 这个 manifest 仓库尚未创建，目前没有可用的 Scoop bucket。请先使用 Releases，bucket 上线后再补充安装命令。
- **校验文件**：每个 Release 附带 `SHA256.txt`，建议下载前校验。

#### 命名与升级兼容

更名期间，以下运行时标识**故意保留旧名**以保证设置延续；包管理器 ID 是例外——6.x 启用全新 ID，不支持原地升级：

| 用户可见 | 遗留标识 | 保留原因 |
|---|---|---|
| UI / Releases 产品名 | Universal Device Toolkit (UDT) | 当前对外品牌 |
| winget / Scoop 包 ID | `SSC-STUDIO.UniversalDeviceToolkit`（6.x 新 ID，待上架）/ `universaldevicetoolkit`（6.x 新 Scoop 名） | 6.x 包管理器断代；旧 `SSC-STUDIO.LenovoLegionToolkit` / `lenovolegiontoolkit` 不支持原地升级 |
| CLI 可执行文件 | `udt.exe`（`udt-cli.exe` 一代兼容别名） | 短命名；旧脚本仍可用 |
| 数据目录 | `%LOCALAPPDATA%\UniversalDeviceToolkit` | 设置自动迁移 |
| 自动化环境变量 | `LLT_*` + `UDT_*`（双写） | 用户脚本兼容；同时提供 UDT 别名 |
| 核心程序集 | `UniversalDeviceToolkit.Lib*`（主 ABI） | 第三阶段 ABI 延续 |

仓库目录使用 `UniversalDeviceToolkit.*`。新用户从 Releases 安装 UDT；上表遗留名为兼容别名，并非另一款产品。

#### 接下来的步骤

UDT 在后台运行时效果最好，请在设置中启用**开机启动**和**关闭时最小化**。接着在设置中禁用 Lenovo Vantage、Legion Zone 与 Lenovo Hotkeys，或直接卸载。之后 UDT 会在开机后自启并接管上述软件的功能。

> [!WARNING]
> 如果你完全关闭 UDT，性能模式（Fn + Q）与电源计划同步、键盘宏及自动化等功能将无法正常工作。这是因为 UDT **不运行任何后台服务**，完全退出后无法响应变更。

另外，请查看下方 [兼容性](#兼容性) 部分。

#### 驱动依赖

在纯净 Windows 上安装 UDT 且需要联想硬件控制时，请确保已安装必要驱动，否则部分选项不可用。联想受支持系统上尤其需要：

1. Lenovo Energy Management
2. Lenovo Vantage Gaming Feature Driver

#### 在安装 .NET 依赖时出现问题？

若安装程序未正确安装 .NET，请手动安装：

1. 打开 https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0
2. 找到「.NET 桌面运行时」
3. 下载 x64 安装程序并运行

完成后在终端执行 `dotnet --info`，在「已安装的 .NET 运行时」中应看到 `Microsoft.NETCore.App 10.x.x` 与 `Microsoft.WindowsDesktop.App 10.x.x`（位于 `C:\Program Files\dotnet\shared`）。

#### 语言包（完整版 / 在线版）与隐私

- **完整版（Full）** 离线附带多语言卫星程序集；**在线版（Online）** 默认仅英文，仅在你于启动语言窗或设置中选择语言时才下载语言包。
- 离线、`--safe-start` 或无网络时：以英文继续运行，**不会**为语言包自动联网。
- 目录下载使用 HTTPS（或你配置的目录 URL），安装前校验 **SHA-256**。无账号、无遥测。
- 企业代理：使用系统代理，或将 `UDT_RESOURCE_CATALOG_URL` 指向内网目录镜像。详见 `Docs/LanguagePacks.md`。

## 兼容性

UDT 通过目录化设备支持识别机型：受支持的联想游戏/创作本获得完整硬件控制；不匹配的联想型号及非联想 PC 进入**基础模式**，不支持的硬件项保持隐藏。语言、主题、更新、日志与安全流程可能仍在——那不是安装 UDT 的理由。

**完整硬件控制系列**：
- Legion 5、Legion Slim 5、Legion Pro 5
- Legion 7、Legion Pro 7、Legion 9
- Legion Go
- LOQ
- IdeaPad Gaming、ThinkBook、YOGA 及选定联想游戏系列
- 国行命名如 R7000/R7000P/R9000/Y7000/Y7000P/Y9000（含 Y7000P 2020H）

**基础模式系列**（隐藏硬件开关，无完整硬件控制）：
- 联想 ThinkPad、ThinkCentre、ThinkStation、IdeaCentre、拯救者台式、小新、V 系列等未匹配联想型号
- 摩托罗拉、华硕、戴尔、惠普、宏碁、微星、Surface、技嘉/AORUS、雷蛇、三星 Galaxy Book、苹果 Mac、华为 MateBook、小米/RedmiBook、realme、Infinix、荣耀 MagicBook、LG gram、Framework 及更多常见 PC 品牌

匹配逻辑见 `UniversalDeviceToolkit.Lib/DeviceSupport/LenovoDeviceSupportProvider.cs` 与在线 device pack。硬件控制主要面向 6 代（MY2021）至 9 代（MY2024）及更新；部分 5 代（MY2020）机型部分功能可用。

若 UDT 以基础模式启动，属于有意隐藏不支持的硬件控制。不要指望那里有 Vantage 级硬件能力。可测 provider 的日志与 device-pack 数据才是扩大硬件支持的路径。

### macOS 与 Linux（实验）

UDT 正式产品以 **Windows 为先**。官方 GitHub Releases 发布 Windows NSIS Full/Online 安装包，内嵌自包含 win-x64 Host（`Release.yml`）。在对应流水线落地前，**没有官方 macOS 或 Linux Electron 发行包**。

当前 macOS/Linux 上存在的是实验性开发面，不是已发布产品：

- `UniversalDeviceToolkit.CrossPlatform` 诊断 CLI（有 CI 覆盖）
- 可移植 `net10.0` 库，以及用 `UDTWindows=false` / `UDT_PLATFORM=linux|macos` 构建的可移植 Host（`build.sh host`）
- 适配标题栏、菜单、托盘与 OSD 的 Electron 壳代码

可移植 Host 对大多数 Windows 专属 RPC 返回 `-32099`（`Not supported on this platform.`）。不要把本地 `npm run dist:mac` / `npm run dist:linux` 产物当作官方发行包。

| 能力 | Windows（已支持） | macOS / Linux（实验） |
|---|---|---|
| 联想硬件控制（Fn+Q、RGB、风扇曲线、独显、电池养护） | 是 | 否 |
| Windows 系统优化 | 是 | 否（可移植 Host 对该域打桩） |
| 主题、应用内更新、日志界面 | 是 | 壳层可能渲染；无官方更新通道或发行资源 |
| 标题栏 / 菜单 / 托盘 / OSD 壳 | 是 | 壳代码存在；不是已发布产品 |
| 重启 / 关机 / 睡眠操作 | 是 | 否 |
| Windows 电源计划切换 | 是 | 否 |

> [!NOTE]
> 重启/关机/睡眠与 Windows 电源计划切换在 Electron 主进程中使用 Windows 专属工具（`shutdown.exe`、`powercfg`）。OSD 悬浮窗本身是 Electron 壳；传感器数据来自 Host，仅在 Windows 上有实际意义。

**构建 Electron 客户端（Windows 产品路径）**

```bash
cd UniversalDeviceToolkit.Electron
npm ci              # 仅首次（使用 package-lock.json）
npm run dev         # 开发服务器 + Electron 窗口（热重载）
npm run dist:win    # Windows NSIS 安装包（x64）；官方发布使用此路径
```

`npm run dist:mac` 与 `npm run dist:linux` 是**实验性本地打包脚本**。它们要求可移植 Host 已发布到 `UniversalDeviceToolkit.Host/publish/osx-*` 或 `linux-x64`。`Release.yml` 不会运行它们，也不会挂载 DMG/AppImage/DEB 资源。

**实验性可移植 Host**（不是发行产物）：

```bash
# Linux x64
UDT_PLATFORM=linux ./build.sh host

# macOS（自动检测 osx-arm64 或 osx-x64）
UDT_PLATFORM=macos ./build.sh host
```

或使用带 `-p:UDTWindows=false` 的等价 `dotnet publish`。用默认 Windows TFM（`net10.0-windows10.0.26100.0`）去发布 `osx-*` / `linux-x64` 不是受支持的产品路径。详见 [DEPLOYMENT.md](Docs/DEPLOYMENT.md)。

仓库还包含 `UniversalDeviceToolkit.CrossPlatform`——一个纯 `net10.0` 的诊断 CLI，可在 macOS/Linux/Windows 本机运行（构建方式见 [DEPLOYMENT.md](Docs/DEPLOYMENT.md)）：

<details>
<summary>跨平台 CLI 命令（开发者）</summary>

```powershell
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- status
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- json
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- hardware
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- telemetry
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- power
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- profile
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- controls
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- elevate set cpu-governor performance
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- support
dotnet run --project UniversalDeviceToolkit.CrossPlatform -- doctor
```

在 macOS/Linux 上，该 CLI 报告平台/运行时信息，从 Linux DMI（`/sys/class/dmi/id`）或 macOS `sysctl`/`system_profiler` 读取基础硬件身份，从 Linux procfs/sysfs 或 macOS `sysctl` 读取安全的 CPU/内存/频率/温度/风扇遥测数据，从 Linux `power_supply` 或 macOS `pmset` 读取电池与外部电源状态，通过 Linux `powerprofilesctl` 或 macOS `pmset` 检查平台电源配置，将常见厂商匹配到安全的基础设备包，并将机器视为安全基础模式。`doctor` 命令将就绪检查汇总为通过/警告/失败报告。厂商专用控制后端是未来的扩展点。

</details>

### 联想软件兼容

建议在使用 UDT 时禁用或卸载 Lenovo Vantage、Hotkeys 和 Legion Zone。与联想官方软件同时运行时可能出现功能冲突。

> [!TIP]
> 一般来说最简单的解决方法就是使用 UDT 内的禁用选项。

### 备注

UDT 目前不支持多用户安装；多账户或无管理员权限的账户可能遇到问题。UDT 需要管理员账户才能正常工作。多用户支持在长期路线图中。

> [!NOTE]
> **保修查询（国行）**：因上游 API 不稳定，近期版本已移除国行拯救者保修查询；已缓存数据在刷新或清除前可能仍显示。

## 功能介绍

UDT 可以做到：

- **深度系统垃圾与着色器缓存清理**：一键扫描并清理 DirectX/Vulkan 着色器缓存、微信/QQ 冗余文件、开发者包管理器缓存（npm/yarn/pnpm/cargo/pip）与系统临时文件。
- **电池健康度与损耗率实时监测**：精准计算设计容量、实际满充容量、电池循环次数与健康等级评定。
- **自动化预设模板与跨设备共享**：内置电池养护、游戏极致、静音办公等预设模板；支持 JSON 导入/导出与剪贴板一键分享。
- **增强型托盘快捷控制面板**：托盘胶囊分段电源模式切换、电池电量徽章与硬件快速概览。
- **专有硬件模式与风扇曲线调节**：改变性能模式、充电模式（养护/快充），调节 2022+ 机型风扇自定义曲线。
- **全方位键盘与灯效控制**：支持 Spectrum 单键 RGB、4 分区 RGB、白色背光键盘、Logo 灯与后接口灯调节。
- **独立显卡混合模式与强制休眠**：支持独显直连/混合模式切换、独显活动进程监控与独显强制断电休眠（英伟达 GPU）。
- **设备自动化管线与宏命令**：电源触发、应用联动、外接屏幕、网络状态、时间周期等全自动执行管线。
- **无常驻服务与驱动管理**：一键禁用 Lenovo Vantage / Legion Zone 常驻服务，从官方服务器安全下载驱动。

### 深度系统垃圾与着色器缓存清理

位于「工具 → 系统与存储」页面，针对现代 PC 玩家与生产力用户的高频磁盘占用痛点进行专项深度清理：

- 🎮 **显卡着色器缓存清理 (DirectX / Vulkan Shader Caches)**：安全清理 DirectX D3DSCache 与 NV_Cache，解决游戏着色器损坏或磁盘爆满问题。
- 💬 **即时通讯软件缓存清理 (WeChat / QQ Caches)**：一键清理微信、QQ 接收的临时缓存文件与废弃预览，释放数十 GB 冗余磁盘空间。
- 💻 **开发者包管理器缓存 (Package Manager Caches)**：支持一键清理 `npm`、`pnpm`、`yarn`、`cargo` 与 `pip` 的全局下载与编译构建缓存。
- 🧹 **系统缩略图与废弃临时文件**：清理 Windows 缩略图数据库与系统级临时目录。

### 电池健康度与损耗率实时监测

位于「控制台 → 电池详细信息」页面：

- **实时健康度评级**：根据当前满充容量与出厂设计容量实时计算健康度百分比，直观评定等级（优秀 ≥ 90%、良好 ≥ 80%、需注意 < 80%）。
- **多维度电池遥测**：实时展示充放电功率区间（mW）、实时充放电速率（W）、电池温度、循环次数、制造日期与首次使用日期。

### 自动化预设模板与跨设备宏共享

位于「动作 → 自动化」与「动作 → 宏」页面：

- **内置标准化预设模板**：提供“拔电自动切换节能与 60Hz”、“插电启动游戏性能模式与高刷”、“夜间静音办公”等成熟自动化预设，新手一键套用。
- **跨设备配置导入/导出**：支持将自动化规则与键盘宏序列导出为标准 JSON 文件，或通过“复制到剪贴板 / 从剪贴板导入”快速在多台电脑间无缝共享。

### 增强型托盘快捷控制面板

- 点击任务栏托盘图标即可唤出精简快捷控制浮窗：
  - **胶囊分段按钮**：一键在安静模式、均衡模式、野兽模式之间毫秒级切换。
  - **电池电量徽章**：实时显示剩余电量百分比、养护模式状态与充放电状态。
  - **即开即走**：托盘浮窗在失去焦点后自动完全卸载，不留任何后台内存开销。

### 指针与光标（内置）

「鼠标」页面内置指针与光标控制：高分屏自定义光标主题（宿主附带 `W11-CC-V2.2-HDPI`）、随 UI 缩放调节光标大小与指针速度。该能力原由 Custom Mouse 插件提供；插件系统退役后已收编为宿主内置功能，已有光标主题与设置会自动导入。

### 自定义模式

以下版本的 BIOS 支持自定义模式：
* GKCN（24及以上）
* GKCN（46及以上）
* H1CN（39及以上）
* HACN（31及以上）
* HHCN（20及以上）

请确保你的 BIOS 版本达到以上最低版本限制。如果你仍然在使用更老版本的 BIOS，你需要先更新才能使用自定义模式。

### RGB 和灯光控制

UDT 支持 Spectrum 单键 RGB 和四分区 RGB 背光。请在使用这些功能前确保你已经禁用了 Lenovo Vantage，以避免双方同时发送控制信息造成冲突。如果你在使用别的可能和 UDT 冲突的 RGB 控制软件，请参考 [FAQ](#faq) 里的解决方案。

UDT 也支持其他像一级或三级白色键盘背光，Legion Logo 背光和后部接口背光等灯光控制功能，不过有以下限制：

* GKCN54WW 及以下版本的 BIOS 由于其中的 bug 会造成系统蓝屏，因此部分灯光控制功能被禁用。
* 部分 Legion 笔记本型号（尤其是 2021 年的型号）可能不显示所有控制选项或显示部分不存在的选项。这主要是由于 BIOS 中对于功能可用性的配置错误。

需要 Corsair iCue 的灯光控制不会被 UDT 支持。

> [!IMPORTANT]
> 目前已知 Riot Vanguard DRM（使用它的游戏有例如：瓦罗兰特） 会造成 RGB 和灯光控制功能出现问题。如果你在安装了它之后发现了 UDT 内 RGB 设置消失的错误，请卸载它或确保它并非开机自启项。

### 混合模式和显卡工作模式

> [!NOTE]
> 请注意这些功能和英伟达 Advanced Optimus 动态显示切换不同，也不与其一同工作。

你有两种主要工作模式来控制你的独立显卡工作方式：

1. 启用混合模式 - 内置显示器会被连接到集成显卡，独立显卡只会在需要时工作以延长续航。
2. 禁用混合模式（独显直连）- 内置显示器被直接连接到独立显卡以提高性能，但这也意味着电池续航会缩短。

切换这两种工作模式需要重启电脑。

在 2022 及 2023 年的型号上混合模式还有两个附加选项：

1. 禁用独立显卡 - 这会断开独立显卡连接，以最大限度延长电池续航。
2. 自动混合模式 - 这会在笔记本使用电池供电时尝试断开独立显卡连接，并在重新连接电源时重新启用独立显卡。

当独立显卡在被使用时（包括有软件或程序调用了独立显卡，连接了外部显示器或其他联想没有说明的情况）独立显卡连接大概率无法被断开。请在使用 UDT 的强制休眠显卡功能之前确保 UDT 已经监测到了独立显卡关闭并没有连接外部显示器，否则可能会造成错误。

所有以上提到的功能均是通过调用 EC（嵌入式控制器）的功能实现的，因此这些功能工作与否取决于联想固件的适配性，而非 UDT。据我的观察和测试，这些功能在大多数时候都是可靠的，除非你频繁切换显卡工作模式。电脑可能需要一定的时间才能完成切换，因此请在每次切换后稍作等待。UDT 也尝试通过阻止频繁切换显卡工作模式和在 EC 未能正常唤醒独立显卡时重试唤醒来缓解这个问题。因此在切换到混合模式时，当 EC 未能唤醒独立显卡时，独立显卡可能需要十秒左右才能重新出现。

如果你还遇到问题，请尝试[命令行参数](#命令行参数)内的其他实验性显卡工作模式。

> [!WARNING]
> 通过设备管理器禁用独立显卡并不会断开独立显卡连接，并同时会导致高耗电量！

### 强制休眠英伟达显卡

有时独立显卡会一直保持活动状态。例如在你插上外接显示器并断开后，一些进程会继续使用独显上运行，导致续航骤减。

在 UDT 中有两种办法强制休眠显卡。

1. 强制关闭所有在独显上运行的进程。（这种方式貌似更有效）,
2. 短暂强制禁用独立显卡，使在独显上运行的进程全部切换到核显。

当独显处于活动状态，并使用混合模式且没有外接显示器到独显上时，强制休眠显卡才会亮起。如果你将鼠标悬停在右侧的问号标志上，将会看到独显的状态与正在使用独显的进程。

> [!NOTE]
> 强制休眠显卡可能会导致一些应用崩溃。

### 超频英伟达独立显卡

此超频选项用于简单的超频，类似于 Legion Zone 与 Vantage 中的超频。它并不打算取代微星小飞机（Afterburner）等工具。同时以下有几点你需要注意：
* 确保在 BIOS 中开启了 GPU 超频选项（如果你的电脑有的话）。
* 当 Vantage 或 Legion Zone 运行时，超频无法生效。
* 不建议在使用微星小飞机（Afterburner）等超频工具时使用此选项。
* 如果你之前修改过控制台，那么你需要点击“自定义”按钮并添加此选项才能看到此选项。

### Windows 电源计划和 Windows 性能模式

首先，你在 UDT 中（或使用 Fn+Q）切换的性能模式和 Windows 电源计划或 Windows 性能模式**是不同的**。

现代的（也是更推荐的）管理 Windows 电源计划及性能模式的方法是只使用一个默认的“平衡（推荐）”电源计划，同时在 Windows 设置内选择以下三种 Windows 性能模式其中之一：

* 推荐的项目
* 更好的性能
* 最佳性能

（译者注：原文此处的三种性能模式直译为：最佳效能，平衡，最佳性能，但在译者的设备上三种性能模式则是上文所写的三种。我无法确定这是系统翻译的不一致性，还是不同代设备之间的差异性造成的，故在使用译者设备上系统内的名称的同时在此注解。）

你可以在 UDT 的设置中为 UDT 性能模式（安静、均衡、野兽和自定义模式）分配对应的 Windows 性能模式。在这之后，当你切换 UDT 性能模式时，UDT 会自动切换 Windows 性能模式。

传统的方法是使用多个“电源计划”，有些设备出厂时已经安装了这些计划。如果你决定使用这种传统的方法，请将 Windows 设置中的设置保持为默认或推荐设置，并将 UDT 设置为在切换性能模式时自动切换电源计划。

如果你在同步电源模式/计划，尤其是在以上提到的两种方法之间切换时遇到问题，你可以使用 `powercfg -restoredefaultschemes; shutdown /r /t ` 命令重置 Windows 电源设置。此命令会重置**所有** Windows 电源计划回默认并重启电脑。这意味着，所有除“平衡（推荐）”以外的电源计划均将会被删除，因此如果你在这之后还希望使用这些电源计划，请务必在此之前手动备份。

### 开机画面

在 2021 及 2022 年的拯救者笔记本上可以使用 UDT 更改开机画面（默认为拯救者的 Logo 图像）。

开机画面**并不被储存在 UEFI 内**，而是在启动盘的 UEFI 分区内。在设置开机画面时，UDT 会做一些基本的图像检查，比如分辨率、图像格式检查，并计算校验和以确保兼容性。不过，**UDT 无法保证通过检查的图像一定会正确的被 UEFI 读取**。在更改开机画面后的下一次启动时，UEFI 会尝试从 UEFI 分区中加载图像并在开机时显示出来，但若加载失败，则会沿用默认图像。具体的标准除了分辨率和格式外尚不清楚。

若你设置的开机画面无法被正确显示，请尝试别的图片。

### 在自动化中运行程序或脚本

你可以在自动化中使用“运行”步骤执行任何程序或脚本。在配置时你需要提供程序（`.exe`）或脚本（`.bat`）的路径。你也可以提供程序或脚本的参数，就像在命令行下运行它们一样。

<details>
<summary>实例</summary>

_关闭电脑_
 - 执行路径：`shutdown`
 - 参数：`/s /t 0`

_重启电脑_
 - 执行路径：`shutdown`
 - 参数：`/r`

_运行程序_
 - 执行路径：`C:\path\to\the\program.exe`（如果该程序所在文件夹已经被加入了 PATH 环境变量，你也可以直接输入程序名）
 - 参数：` `（可选，请查阅你使用的程序的文档或网站以获取可用的参数列表）

_运行脚本_
 - 执行路径：`C:\path\to\the\script.bat`（如果该脚本所在文件夹已经被加入了 PATH 环境变量，你也可以直接输入程序名）
 - 参数：` `（可选，请查阅你使用的程序的文档或网站以获取可用的参数列表）

_运行 Python 脚本_
 - 执行路径：`C:\path\to\python.exe`（若你已经将 Python 的安装路径加入了 PATH 环境变量，你也可以直接使用 `python`）
 - 参数：`C:\path\to\script.py`

 </details>

#### 环境变量

UDT 会自动在进程运行环境内添加一些可被访问的环境变量。这些环境变量对于需要执行上下文的高级脚本会十分实用。根据触发器的不同 UDT 会添加不同的环境变量。

<details>
<summary>环境变量</summary>

- 当电源适配器插入时
	- `LLT_IS_AC_ADAPTER_CONNECTED=TRUE`
- 当较低功率电源适配器插入时
	- `LLT_IS_AC_ADAPTER_CONNECTED=TRUE`
	- `LLT_IS_AC_ADAPTER_LOW_POWER=TRUE`
- 当电源适配器断开时
	- `LLT_IS_AC_ADAPTER_CONNECTED=FALSE`
- 当性能模式改变时
	- `LLT_POWER_MODE=<value>`，`value` 的值由当前性能模式决定：`1` 为安静模式，`2` 为均衡模式，`3` 为野兽模式，`255` 为自定义模式
	- `LLT_POWER_MODE_NAME=<value>`，`value` 的值为当前性能模式对应的英语大写名称：`QUIET`, `BALANCE`, `PERFORMANCE`, `CUSTOM`
- 当打开游戏时
	- `LLT_IS_GAME_RUNNING=TRUE`
- 当关闭游戏时
	- `LLT_IS_GAME_RUNNING=FALSE`
- 当应用程序启动时
	- `LLT_PROCESSES_STARTED=TRUE`
	- `LLT_PROCESSES=<value>`，`value` 的值为以逗号分隔的进程名
- 当指定的应用关闭时
	- `LLT_PROCESSES_STARTED=FALSE`
	- `LLT_PROCESSES=<value>`，`value` 的值为以逗号分隔的进程名	
- 打开盖子时
	- `LLT_IS_LID_OPEN=TRUE`
- 合上盖子时
	- `LLT_IS_LID_OPEN=FALSE`
- 当显示器打开时
	- `LLT_IS_DISPLAY_ON=TRUE`
- 当显示器关闭时
	- `LLT_IS_DISPLAY_ON=FALSE`
- 当连接了外置屏幕后
	- `LLT_IS_EXTERNAL_DISPLAY_CONNECTED=TRUE`
- 当断开了外置屏幕后
	- `LLT_IS_EXTERNAL_DISPLAY_CONNECTED=FALSE`
- 当启用 HDR 时
	- `LLT_IS_HDR_ON=TRUE`
- 当关闭 HDR 时
	- `LLT_IS_HDR_ON=FALSE`
- 当与 Wi-Fi 连接时
	- `LLT_WIFI_CONNECTED=TRUE`
	- `LLT_WIFI_SSID=<value>`，`value` 的值为网络的 SSID
- 当与 Wi-Fi 断开连接时
	- `LLT_WIFI_CONNECTED=FALSE`	
- 在特定的时间
	- `LLT_IS_SUNSET=<value>`，`value` 的值为 `TRUE` 或 `FALSE`，取决于触发器的设置
	- `LLT_IS_SUNRISE=<value>`，`value` 的值为 `TRUE` 或 `FALSE`，取决于触发器的设置
	- `LLT_TIME"`，`value` 的值为 `HH:mm`，取决于触发器的设置
	- `LLT_DAYS"`, `value` 的值为以逗号分隔的包含以下内容的列表：`MONDAY`、`TUESDAY`、`WEDNESDAY`、`THURSDAY`、`FRIDAY`、`SATURDAY`、`SUNDAY`，取决于触发器的设置
- 循环自动化
	- `LLT_PERIOD=<value>`，`value` 的值为间隔的秒数
- 当开机时
	- `LLT_STARTUP=TRUE`
- 当唤醒时
	- `LLT_RESUME=TRUE`

</details>

#### 程序输出

当 `等待运行结束` 被启用时，UDT 会抓取被启动的进程的标准输出流内的内容。这些程序输出会被存入 `$RUN_OUTPUT$` 变量，并可在“显示提示弹窗”步骤中使用。

### 命令行界面

你可以在命令行内直接控制 UDT 的部分功能。UDT 命令行界面的可执行文件位于安装文件夹下，名为 `udt.exe`（`udt-cli.exe` 为一代兼容别名）。

命令行界面需要 UDT 在后台运行并且在设置内启用命令行界面，否则其无法正常工作。你也可以选择将命令行界面添加至你的用户 `PATH` 环境变量。

约定、`--json` 与 `doctor` 见 [Docs/CLI.md](Docs/CLI.md)。给 Agent 用的 skill（复制到 `~/.cursor/skills/udt-hardware-cli/`）：[Docs/Skills/udt-hardware-cli/SKILL.md](Docs/Skills/udt-hardware-cli/SKILL.md)。

使用命令行界面无需管理员权限。

<details>
<summary>功能</summary>

* `udt doctor` - 检查 CLI 是否就绪（`--json` 供 Agent 使用，无需 IPC）
* `udt --json …` - 所有命令统一以单个 JSON 对象输出
* `udt quickAction --list` - 列出所有快捷操作
* `udt quickAction <name>` - 执行快捷操作 `<name>`
* `udt feature --list` - 列出所有可用功能
* `udt feature get <name>` - 打印功能 `<name>` 当前的值
* `udt feature set <name> --list` - 列出功能 `<name>` 所有可设定的值
* `udt feature set <name> <value>` - 将功能 `<name>` 的值设定为 `<value>`
* `udt spectrum profile get` - 打印当前 Spectrum RGB 预设
* `udt spectrum profile set <profile>` - 将 Spectrum RGB 预设设定为 `<profile>`
* `udt spectrum brightness get` - 打印当前 Spectrum RGB 的亮度
* `udt spectrum brightness set <brightness>` - 将 Spectrum RGB 的亮度设定为 `<brightness>`
* `udt rgb get` - 打印当前四分区 RGB 预设
* `udt rgb set <profile>` - 将四分区 RGB 预设设定为 `<preset>`

</details>

## 插件系统（已退役）

插件系统已在 **6.1** 退役：插件加载、「插件扩展」页面与插件目录工具链均已从本仓库移除。

原有能力去向：

- **自定义鼠标（Custom Mouse）** → 内置于「鼠标」页面的指针与光标控制（已有光标主题与设置自动导入）
- **网络加速（Network Acceleration）** → 自 5.0 起内置于「工具 → 网络与加速」
- **Shell 集成 / ViVeTool** → 已下架，宿主无内置替代

`%LOCALAPPDATA%\UniversalDeviceToolkit\plugins` 中遗留的数据保留在磁盘但不再加载。历史 `plugin-catalog` GitHub Release 标签仅作归档，不得用作应用更新通道。详见 [CHANGELOG](CHANGELOG.md)。

## 贡献者

特别感谢：

* [ViRb3](https://github.com/ViRb3) 创建了 [Lenovo Controller](https://github.com/ViRb3/LenovoController)，这是 UDT 的基础。
* [falahati](https://github.com/falahati) 创建了 [NvAPIWrapper](https://github.com/falahati/NvAPIWrapper) 和 [WindowsDisplayAPI](https://github.com/falahati/WindowsDisplayAPI)
* [SmokelessCPU](https://github.com/SmokelessCPU) 帮助我适配了四分区 RGB 键盘背光。
* [Mario Bălănică](https://github.com/mariobalanica) 的所有贡献。
* [Ace-Radom](https://github.com/Ace-Radom) 的所有贡献。

翻译贡献者：
* 保加利亚语 - [Ekscentricitet](https://github.com/Ekscentricitet)
* 简体中文 - [凌卡Karl](https://github.com/KarlLee830), [Ace-Radom](https://github.com/Ace-Radom)
* 繁体中文 - [flandretw](https://github.com/flandretw)
* 捷克语 - J0sef
* 荷兰语 - Melm, [JarneStaalPXL](https://github.com/JarneStaalPXL)
* 法语 - EliotAku, [Georges de Massol](https://github.com/jojo2massol), Rigbone, ZeroDegree
* 德语 - Sko-Inductor, Running_Dead89
* 希腊语 - GreatApo
* 意大利语 - [Lampadina17](https://github.com/Lampadina17)
* 卡拉卡尔帕克语 - KarLin, Gulnaz, Niyazbek Tolibaev, Shingis Joldasbaev
* 拉脱维亚语 - RJSkudra
* 罗马尼亚语 - [Mario Bălănică](https://github.com/mariobalanica)
* 斯洛伐克语 - Mitschud, Newbie414
* 西班牙语 - M.A.G.
* 葡萄牙语 - dvsilva
* 葡萄牙语（巴西） - Vernon
* 俄语 - [Edward Johan](https://github.com/younyokel)
* 土耳其语 - Undervolt
* 乌克兰语 - [Vladyslav Prydatko](https://github.com/va1dee), [Dmytro Zozulia](https://github.com/Nollasko)
* 越南语 - Not_Nhan, Kuri, Nagidrop

## FAQ

* [为什么即使我已经卸载了 Vantage，我依然可以看到它正在运行？](#为什么即使我已经卸载了-Vantage，我依然可以看到它正在运行？)
* [为什么我的杀毒软件报告安装程序含有病毒/木马/恶意软件？](#为什么我的杀毒软件报告安装程序含有病毒/木马/恶意软件？)
* [我能自定义热键吗？](#我能自定义热键吗？)
* [我可以自定义节能模式充电阈值吗？](#我可以自定义节能模式充电阈值吗？)
* [我可以更改安静，均衡及野兽模式的风扇策略吗？](#我可以更改安静，均衡及野兽模式的风扇策略吗？)
* [更换主板后提示不兼容？](#更换主板后提示不兼容？)
* [为什么我无法在使用电池供电时切换到野兽与自定义模式？](#为什么我无法在使用电池供电时切换到野兽与自定义模式？)
* [我可以在使用 UDT 时使用其他RGB软件吗？](#我可以在使用UDT时使用其他RGB软件吗？)
* [支持 iCue RGB 键盘吗？](#支持-iCue-RGB-键盘吗？)
* [能不能多增加一些 RGB 效果？](#能不能多增加一些-RGB-效果？)
* [2022 款之前的机器能否自定义调节风扇曲线？](#2022-款之前的机器能否自定义调节风扇曲线？)
* [为啥在启用 Legion AI 引擎后切换性能模式看起来似乎有些问题？](#为啥在启用-Legion-AI-引擎后切换性能模式看起来似乎有些问题？)
* [为何即使自动化配置正确，游戏检测仍无法正常触发？](#为何即使自动化配置正确，游戏检测仍无法正常触发？)
* [为什么将鼠标悬停在 UDT 托盘图标上却看不到自定义工具提示？](#为什么将鼠标悬停在UDT托盘图标上却看不到自定义工具提示？)
* [我在哪里可以找到 CPU 睿频模式的设置？](#我在哪里可以找到-CPU-睿频模式的设置？)
* [如果我在对 GPU 进行超频时超过了阈值，该怎么办？](#如果我在对-GPU-进行超频时超过了阈值，该怎么办？)
* [我的开机画面为什么没有生效？](#我的开机画面为什么没有生效？)
* [为什么使用智能 Fn 锁时会出现卡顿？](#为什么使用智能-Fn-锁时会出现卡顿？)

#### 为什么即使我已经卸载了 Vantage，我依然可以看到它正在运行？

自 2.14.0 版本开始，UDT 对于 Vantage 残留进程检测更加严格。总的来说，Vantage 安装了三个组件：

1. Lenovo Vantage App
2. Lenovo Vantage Service
3. System Interface Foundation V2 Device

最简单的解决方法是进入 UDT 的设置界面并禁用 Lenovo Vantage，LegionZone 和 Hotkeys（只有没有被卸载的软件会被显示在设置界面）。

如果你还是想卸载它们，请确保卸载全部三个组件，否则 UDT 的部分功能可能无法正常运行。你可以检查任务管理器中是否有名称中包含 “Vantage” 或是 “ImController” 字样的进程。如果你需要别的帮助或是在卸载 `ImController` 过程中遇到问题，可以参考：[Uninstalling System Interface Foundation V2 Device](https://support.lenovo.com/us/en/solutions/HT506070)。

#### 为什么我的杀毒软件报告安装程序含有病毒/木马/恶意软件？

UDT 使用了许多底层的 Windows API，杀毒软件可能会识别这些 API 的调用为可疑的，从而造成误报。UDT 本身是开源的，任何感觉此软件有问题的人都可以审查源代码。正式发布的程序和安装包由 GitHub Actions 构建，通过 Azure Trusted Signing 签名，并在发布前验证。未签名的本地构建不应被视为官方发布物。

简而言之，如果你从这个项目的网站上下载了安装程序，不需要担心杀毒软件的报毒，这只是一个误报。同时如果你有能力帮助解决杀毒软件的误报问题，欢迎联系我。

#### 我能自定义热键吗？

你可以在 UDT 的设置界面内自定义 Fn+F9 的热键功能。其余的热键是无法被自定义的。

#### 我可以自定义节能模式充电阈值吗？

不能，该阈值是在固件中被锁死的，无法被更改。对于 2021 年及以前的型号该阈值为 60%，对于之后的型号则为 80%。

#### 我可以更改安静，均衡及野兽模式的风扇策略吗？

不能。除自定义模式以外，风扇策略都是不能更改的。

#### 更换主板后提示不兼容？

有时新主板内的机型和序列号信息出错，你可以尝试 [这篇教程](https://laptopwiki.eu/laptopwiki/guides/lenovo/legion_bios_lvarrecovery) 来恢复。

#### 为什么我无法在使用电池供电时切换到野兽与自定义模式？

在 2.11.0 版本后，UDT 与 Lenovo Vantage 与 Legion Zone 的行为保持一致，将不再允许在没有插入适当电源适配器的情况下启用野兽与自定义模式。

如果出于某种原因，你想在电池供电的情况下使用这些模式，你可以使用`--allow-all-power-modes-on-battery`参数来启用（参见[命令行参数](#命令行参数)）。

> [!WARNING]
> 当笔记本电脑没有连接到全功率的电源适配器时，功耗限制与其他设置在大多数设备上无法正常应用，同时可能会出现一些未知的问题。

#### 我可以在使用 UDT 时使用其他RGB软件吗？

总的来说，可以。 当 Lenovo Vantage 运行时，UDT 将禁用 RGB 控制，以避免冲突。如果你想使用其他 RGB 软件，如 [L5P-Keyboard-RGB](https://github.com/4JX/L5P-Keyboard-RGB) 或 [OpenRGB](https://openrgb.org/)，你可以在 UDT 中使用 `--force-disable-rgbkb` 或 `--force-disable-spectrumkb` 参数禁用 RGB 以避免冲突（参考[命令行参数](#命令行参数)）。

#### 支持 iCue RGB 键盘吗？

不支持，我推荐你看看 [OpenRGB](https://openrgb.org/) 这个项目。

#### 能不能多增加一些 RGB 效果？

只有硬件支持的选项可用，不计划支持自定义效果。如果你想要自定义效果可以看看 [L5P-Keyboard-RGB](https://github.com/4JX/L5P-Keyboard-RGB) 或 [OpenRGB](https://openrgb.org/)。

#### 2022 款之前的机器能否自定义调节风扇曲线？

如果你是 2022 款与更新的机型的拯救者，但不支持自定义风扇曲线的话，请提交 Issue，我们会尝试适配。2022 款之前的机型由于技术原因无法支持。

#### 为啥在启用 Legion AI 引擎后切换性能模式看起来似乎有些问题？

貌似有些 BIOS 版本在使用 Fn+Q 快捷键时有一个奇怪的问题，你可以试试更新 BIOS，除此之外只能等联想修复它了。

#### 为何即使自动化配置正确，游戏检测仍无法正常触发？

UDT 的游戏检测功能是基于 Windows 游戏检测的。这意味着游戏检测功能仅能被 Windows 认为是游戏的 EXE 文件触发。同时如果你删除了 Xbox Game Bar，此功能大概率将无法正常工作。

Windows 可能无法正确识别所有的游戏，但你可以在 Xbox Game Bar (Win + G) 设置中将任何程序标记为游戏。你可以在注册表中找到可识别的游戏列表：`HKEY_CURRENT_USER\System\GameConfigStore\Children`。

#### 为什么将鼠标悬停在 UDT 托盘图标上却看不到自定义工具提示？

在 Windows 10 和 11 中，微软对托盘做了大量的修改，修改导致了很多东西产生了变化，从而导致许多东西无法正常工作。因此，自定义工具提示有时会无法正常工作。你可以试试更新你的 Windows，除此之外没什么好办法了。

#### 我在哪里可以找到 CPU 睿频模式的设置？

简而言之，在 Windows 控制面板中。此选项因很难维护已经从 UDT 中删除了。在 UDT 的设置中，你可以找到一个直接跳转到控制面板中电源计划设置页面的按钮，在那里你可以轻松地编辑 CPU 睿频模式设置以及 Windows 电源计划的其他设置。

默认情况下，这个设置是隐藏的，但你也可以通过在终端运行`powercfg.exe -attributes sub_processor perfboostmode -attrib_hide`来重新打开这个选项。
另外我还推荐其他一些应用程序可以轻松地管理多个电源计划设置：[PowerSettingsExplorer](https://forums.guru3d.com/threads/windows-power-plan-settings-explorer-utility.416058/) 与 [QuickCPU](https://coderbag.com/product/quickcpu)。

#### 如果我在对 GPU 进行超频时超过了阈值，该怎么办？

如果你超频到 GPU 无法稳定运行，甚至无法启动 Windows 的情况，你可以通过以下两种方法尝试解决：

1. 进入 BIOS，尝试找到类似于 “Enabled GPU Overclocking” 与“显卡超频”的选项并将其禁用，启动 Windows，修改UDT中的超频参数，并将此选项再次启用。
2. 在安全模式下启动 Windows，删除 LLT 设置下的 `gpu_oc.json` 文件，该文件位于 `"%LOCALAPPDATA%\UniversalDeviceToolkit`。

#### 我的开机画面为什么没有生效？

当你设置开机画面时，UDT会做一些基本的检查以确保图像的分辨率和格式符合标准。如果 UDT 显示开机画面已被设置，意味着该图像已经被写入了启动盘的 UEFI 分区。如果你在启动时没有看到开机画面，这意味着你的开机画面无法被正确显示，即使相关选项已经在 UEFI 中被成功配置。在这种情况下，你也许可以尝试使用另外的一张图片，更改图片的格式，或使用别的图像编辑软件，等等。如果你尝试了所有可能的解决方案但你的开机画面还是无法被正常显示，那这也许就是由 BIOS 造成的问题了，你可以尝试更新 BIOS 版本再重试。

#### 为什么使用智能 Fn 锁时会出现卡顿？

在一些版本的 BIOS 上切换 Fn 锁是会造成一定的卡顿。由于智能 Fn 锁本质上是自动的 Fn 锁切换，因此也会受到这个问题的影响。

如果你遇到了这个问题，你可以尝试关闭 BIOS 内的 `Fn键动态替换`（英语版 BIOS 则为 `Fool Proof Fn Ctrl`）功能。这也许可以解决 Fn 锁切换的卡顿。

#### 为什么我无法在设备信息中找到保修信息？

由于联想不断更新国行保修服务的 API 造成此功能越来越难以维护，在最新版本中 UDT 移除了该功能对国内拯救者全部型号的支持。如果你曾经通过 UDT 获取过保修信息那它将保持正常显示，但一旦你手动刷新保修信息或删除了储存的数据这些信息就会消失。这一改动仅会影响使用国行拯救者型号的用户。

## 命令行参数

一些并不常用的功能在 GUI 中没有对应的启动开关。这些功能需要通过在启动 UDT 时添加命令行参数，或将参数添加到 `args.txt` 中的方式启用。

* `--trace` - 启用日志记录并将日志保存到 `%LOCALAPPDATA%\UniversalDeviceToolkit\log`
* `--minimized` - 以最小化到托盘的方式启动 UDT
* `--disable-tray-tooltip` - 当鼠标悬停在托盘图标上方时不显示 UDT 托盘自定义工具提示
* `--allow-all-power-modes-on-battery` - 允许在未接通外部电源的情况下启用所有性能模式 _（使用该参数时 UDT 不保证能够正常运行，也不会为此参数造成的问题提供技术支持）_
* `--force-disable-rgbkb` - 禁用四分区 RGB 键盘的所有光效控制功能
* `--force-disable-spectrumkb` - 禁用 Spectrum 单键 RGB 的所有光效控制功能
* `--force-disable-lenovolighting` - 禁用拯救者 Logo，白色键盘背光，和其他如端口背光的光效控制功能
* `--experimental-gpu-working-mode` - 将显卡工作模式切换至和 LegionZone 相同的实验性模式 _（使用该参数时 UDT 不保证能够正常运行，也不会为此参数造成的问题提供技术支持）_
* `--proxy-url=example.com` - 指定 UDT 应该使用的代理服务器地址
* `--proxy-username=some_username` - 如果需要，指定 UDT 使用的代理服务器的用户名
* `--proxy-password=some_password` - 如果需要，指定 UDT 使用的代理服务器的密码
* `--proxy-allow-all-certs` - 如果需要，放宽通过代理服务器建立 HTTPS/SSL 连接的所需标准
* `--disable-update-checker` - 禁用 UDT 自动新版本检测 _（若你希望依赖于 winget，scoop 等等软件更新 UDT，你可以启用此选项）_

如果你希望将所需参数保存至 `args.txt` 文件内：
1. 进入 `%LOCALAPPDATA%\UniversalDeviceToolkit` 文件夹
2. 在那里创建一个名为 `args.txt` 的文本文件
3. 在文件内的每一行添加**一个**参数
4. 启动 UDT

任何没有在上方列出的，曾经可用的命令行参数均已被废弃，也无法再使用。

## 如何开启记录 Log

在一些情况下如果你能提交应用记录的日志信息，将会对我调试和解决问题十分十分有用。

记录 Log 日志的步骤：

1. 确保 UDT 已关闭（后台也记得关掉）；
2. 打开 `运行` （使用 Win + R 打开）然后输入 `"%LOCALAPPDATA%\Programs\UniversalDeviceToolkit\Lenovo Legion Toolkit.exe" --trace` 然后点击确定；
3. UDT 将会启动并且可以在左上角能看到 `[LOGGING ENABLED]` ；
4. 复现你遇到的问题；
5. 关闭 UDT （同样记得关掉后台）；
6. 然后打开 `运行` （使用 Win + R 打开）然后输入 `"%LOCALAPPDATA%\UniversalDeviceToolkit\log"` ；
7. 这里就是存放日志文件的地方了，请在 Issue 内汇报 Bug 时一并提交。

## 贡献此项目

我感谢你们提交的任何反馈！不要犹豫，直接提交 Issue。我们也欢迎提交 PR，但提交 PR 前务必查看 [CONTRIBUTING.md](CONTRIBUTING.md) 文件！

> [!IMPORTANT]
> 译者提示：由于 UDT 并非由国人发起的项目，主要开发者也大多来自欧美，为了整体交流环境的统一和协调，所有 Issue，PR 和 Commit Message **必须**使用英语书写，否则将被直接关闭并锁定，**没有例外**。此点也已在 [CONTRIBUTING.md](CONTRIBUTING.md) 中说明。若你无法流畅地使用英语表达，你可以在使用中文完成草稿后使用百度翻译或 [DeepL](https://www.deepl.com/zh/translator) 等翻译网站或软件将草稿翻译为英语后提交。

#### 适配

> [!IMPORTANT]
> **完整硬件控制适配请求**仅面向联想拯救者、IdeaPad Gaming 与 LOQ 系列——请勿为其他品牌或不支持的联想产品线提交 Vantage 级硬件控制需求。
>
> **硬件 provider 贡献欢迎**：非联想及不匹配的联想 PC 在有可测 provider 之前以基础模式运行。欢迎提交 device-pack 数据、日志与真机测试；通用 Windows 工具类需求不接受。

若能适配更多设备当然更好，但这需要你的帮助！

如果你愿意在未适配的机型上试试这个软件，请在启动时点击弹窗的继续按钮，UDT 会自动打开日志记录，这样你就可以在提交 Issue 时提交了！

*注意一些功能可能无法正常运行*

如果你在 Github 上提交 Issue 并附上你的测试结果和日志我将十分感谢你！

请确保在你提交的 Issue 中包含以下信息：

1. 完整的设备型号 (例如：Legion Y9000X 2022款 IAH7)
2. 正常工作的功能
3. 出错的功能
4. 会导致崩溃闪退的功能

你提交的信息越多，随着时间的推进，LLT 就会变得越来越好！如果有什么出错的地方请准确写下问题并附上日志。(日志保存地址 `%LOCALAPPDATA%\UniversalDeviceToolkit\log`). 

## 本地化翻译

UDT 的本地化通过 Crowdin 管理，仓库配置文件为 `crowdin.yml`。

- 源文件：4 个模块下的中立资源 `Resource.resx`
  - `UniversalDeviceToolkit.Lib/Resources`
  - `UniversalDeviceToolkit.Lib.Automation/Resources`
  - `UniversalDeviceToolkit.Lib.Macro/Resources`
  - `UniversalDeviceToolkit.CLI/Resources`（`CLI.Resources.resx`）
  Electron 界面文案位于 `UniversalDeviceToolkit.Electron/src/renderer/src/i18n/locales/`（TS 模块）。
- 目标文件：与源文件同目录的 `Resource.<locale>.resx`（CLI 为 `CLI.Resources.<locale>.resx`）。文化名使用 BCP 47 规范形式（`zh-Hans`、`zh-Hant`、`pt-BR`、`nl-NL`、`uz-Latn-UZ`），由 `Scripts/Assert-CultureNaming.ps1` 在 CI 强制校验。
- 语言映射：在 `crowdin.yml` 内定义（例如 `zh-CN -> zh-Hans`、`zh-TW -> zh-Hant`、`pt-BR -> pt-BR`）。

常用命令：

```bash
# 上传源字符串
crowdin upload sources --config crowdin.yml

# 上传现有翻译
crowdin upload translations --config crowdin.yml

# 下载翻译结果
crowdin download --config crowdin.yml
```

## 文档索引

更多技术文档位于 `Docs/` 目录：

| 文档 | 说明 |
|------|------|
| [ARCHITECTURE.md](Docs/ARCHITECTURE.md) | 系统架构、组件与数据流 |
| [DEPLOYMENT.md](Docs/DEPLOYMENT.md) | 构建、测试、部署与发布流程 |
| [LanguagePacks.md](Docs/LanguagePacks.md) | 语言包目录协议与生命周期 |
| [NetworkAcceleration.md](Docs/NetworkAcceleration.md) | 内置网络与加速模块 |
| [PROMOTION_CN.md](Docs/Promotion/PROMOTION_CN.md) | 发布与社区宣发文案（中文） |
| [PROMOTION_EN.md](Docs/Promotion/PROMOTION_EN.md) | 发布与社区宣发文案（英文） |
| [COMMUNITY_OUTREACH.md](Docs/Promotion/COMMUNITY_OUTREACH.md) | 社区发帖手册与提交记录 |
| [SECURITY.md](Docs/SECURITY.md) | 安全策略与实践 |
| [CODE_OF_CONDUCT.md](Docs/CODE_OF_CONDUCT.md) | 社区行为准则 |

### 截图说明

逻辑窗口尺寸 **1300×850**（像素尺寸随 DPI 缩放）。README 中以宽度 700 显示。

| 文件 | 说明 |
|------|------|
| `Assets/UDT_Promo_zh.mp4` | 30 秒中文旁白宣传片（点 [宣传片](#宣传片) 海报即可播放） |
| `Assets/UDT_Promo_en.mp4` | 30 秒英文旁白宣传片（英文 README 使用） |
| `Assets/UDT_Promo_poster.jpg` | 宣传片封面 |
| `Assets/Screenshot_main.png` | 主界面（英文，深色主题） |
| `Assets/Screenshot_zh-hans.png` | 主界面（简体中文，深色主题） |

刷新步骤见 [DEPLOYMENT.md](Docs/DEPLOYMENT.md#readme-screenshots)。

### 故障排查

- **无法启动？** 检查 [.NET 10 桌面运行时](#在安装-net-依赖时出现问题)
- **功能不可用？** 见 [兼容性](#兼容性)
- **需要日志？** 见 [如何开启记录 Log](#如何开启记录-log)
- **仍需帮助？** 提交 [GitHub Issue](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/issues)

## 致谢

特别感谢 Lenovo Legion Toolkit 原作者 Bartosz Cichecki 的开创性工作，为本项目提供了重要启发。

---

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=SSC-STUDIO/UniversalDeviceToolkit&type=Date)](https://star-history.com/#SSC-STUDIO/UniversalDeviceToolkit&Date)

<div align="center">

### ⭐ 如果 UDT 帮到你，请点个 Star

GitHub 靠 Star 排序。点一下，下一个想卸 Vantage 的拯救者用户才找得到这个工具。目标：**1,000 stars**。

[![Star this repo](https://img.shields.io/github/stars/SSC-STUDIO/UniversalDeviceToolkit?style=social&label=Star%20UDT)](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/stargazers)

**欢迎贡献！** 请先阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

</div>
