## 欢迎来到 UDT 开发者指南！

### 其他语言版本：
* [English](CONTRIBUTING.md)

首先感谢你花时间为本项目做出贡献！随着 UDT 的热度越来越高，为了确保你的贡献能够被顺利采纳，你应该遵守一定的格式和规则。

<br/>

_由于 Issues 总量的增加，不符合标准的 Issue 会在无预先警告的情况下被关闭或删除。屡次违反者将被本项目封禁。_

<br/>

**开发环境准备**

1. 安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（Windows、macOS 或 Linux）
2. 安装 [Node.js 20+](https://nodejs.org/)（Electron 客户端）
3. 克隆仓库：`git clone https://github.com/SSC-STUDIO/UniversalDeviceToolkit.git`
4. 还原（与 CI 一致）：`dotnet restore UniversalDeviceToolkit.sln --locked-mode`
5. 构建：`dotnet build -c Release -m:1 --no-restore`
6. 运行测试：`dotnet test -c Release`  
   或仅跑 CI fail-fast 分层：`pwsh ./Scripts/Run-TestFailFast.ps1`

> [!NOTE]
> 完整解决方案构建**仅限 Windows**（Host 与 Lib 目标框架为
> `net10.0-windows10.0.26100.0` 并强制 win-x64）。macOS/Linux 上请走可移植路径：
> `./build.sh Release` 可在三平台构建跨平台库与
> `UniversalDeviceToolkit.CrossPlatform` CLI，
> 并运行 `UniversalDeviceToolkit.CrossPlatform.Tests`
> （见 `Docs/DEPLOYMENT.md` → 「Cross-platform builds」）。

**Electron 客户端（界面）**

UI 是位于 `UniversalDeviceToolkit.Electron/` 的 Electron 应用（Node.js +
electron-vite + React；不属于 .NET 解决方案）。首次安装依赖后即可启动：

```bash
cd UniversalDeviceToolkit.Electron
npm ci            # 仅首次（使用 package-lock.json）
npm run dev       # 开发服务器 + Electron 窗口（热重载）
npm start         # 运行构建产物（先 `npm run build`）
npm run lint      # ESLint 门禁（错误会使 CI 失败）
npm run typecheck # TS 类型检查（web + main/preload）
npm test          # 渲染进程 / 主进程 / 安装器契约测试
```

仓库根目录的 `package.json` 只是把 `npm run dev|build|lint|typecheck|start|dist*` 转发到 `UniversalDeviceToolkit.Electron/`，让这些命令在仓库根目录也能直接执行；它本身没有任何依赖。其中的 `version` 属于发布版本号的一部分，必须与 `Directory.Build.props` 一致（由 `PackagingGuardTests` 强制校验）。

在 Visual Studio 中，解决方案里有一个精简的 `UniversalDeviceToolkit.Electron`
启动器项目（无操作占位 exe）。把它设为**启动项目**并按 **F5** —— 它的
"Electron (npm run dev)" 启动配置会自动执行 `npm run dev`。

> **不要把 `UniversalDeviceToolkit.Host` 设为启动项目。** Host 是无头
> JSON-RPC 后端（基于 stdio），由 Electron 启动时自动拉起，从不显示窗口。
> 进程模型见 `Docs/ARCHITECTURE.md`。

**跨平台开发（macOS / Linux，实验性）**

正式产品以 Windows 为准。官方发布（`Release.yml`）只出 Windows NSIS 安装包和 win-x64 Host。macOS/Linux 仍是实验路径：没有官方 Electron 发行物，本地 `npm run dist:mac` / `npm run dist:linux` 也不是发布产物。

Electron 壳可以在 macOS/Linux 上做界面开发：

```bash
cd UniversalDeviceToolkit.Electron
npm ci            # 仅首次
npm run dev       # 开发服务器 + Electron 窗口（热重载）
npm run lint      # ESLint 门禁
npm run typecheck # TS 类型检查
```

可移植 Host（`net10.0`，`UDTWindows=false`）会把多数 Windows 专用 RPC 标成 `-32099`。不要把默认 Windows TFM 发到 `osx-*` / `linux-x64`。

```bash
# 实验性可移植 Host（不是发布产物）
UDT_PLATFORM=linux ./build.sh host
UDT_PLATFORM=macos ./build.sh host

# 等价写法：
dotnet publish UniversalDeviceToolkit.Host/UniversalDeviceToolkit.Host.csproj \
    -c Release -r linux-x64 -p:UDTWindows=false --self-contained \
    -o UniversalDeviceToolkit.Host/publish/linux-x64

# Windows（x64）— 装进 NSIS 安装包的正式路径
dotnet publish UniversalDeviceToolkit.Host/UniversalDeviceToolkit.Host.csproj \
    -c Release -r win-x64 --self-contained \
    -o UniversalDeviceToolkit.Host/publish/win-x64
```

> [!NOTE]
> 硬件控制仍只在 Windows 上可用。Windows 发布路径与实验性可移植 Host 见
> `Docs/DEPLOYMENT.md`；Electron 界面壳（标题栏、菜单栏、托盘、OSD、系统电源）
> 见 `Docs/ARCHITECTURE.md` → 「Platform Notes」。

**测试分层**见 [Docs/TEST_DIAGNOSTICS.md](Docs/TEST_DIAGNOSTICS.md)。宿主测试按工程拆分：`Tests.Contracts`（Guard/Security，fail-fast）→ `Fast.Tests` → `Tests`（并行单元）→ `Tests.Stateful`（Localization/Settings/ProcessState/PowerMode，集合不并行）。`TestCategories` 仅保留 `Security` / `Guard` / `Unit`，每个类最多一个 Category；CI 按工程选择，不再使用 `Coverage` / `Plugin` / `Smoke` 过滤。插件系统已在 6.1 退役。Electron 门禁为 `npm run lint`、`npm run typecheck`、`npm test`。

NuGet 还原通过各项目已提交的 `packages.lock.json` 保证可复现（`Directory.Build.props` 中启用了 `RestorePackagesWithLockFile`）。CI 始终使用 `dotnet restore … --locked-mode`。本地对齐 CI 时请带上该参数；仅在有意更新包版本后刷新锁文件时省略，并将更新后的 `packages.lock.json` 一并提交。`Make.bat` 与多数本地脚本依赖构建/发布时的隐式还原，不会强制 `--locked-mode`，因此一般离线构建不会因锁文件严格校验而中断。

解决方案共有 23 个项目（22 个 .NET + Electron 启动器）。请顺序构建（`-m:1`），以避免 VBCSCompiler 锁冲突。完整项目结构见 [Docs/DEPLOYMENT.md](Docs/DEPLOYMENT.md) 的「Solution Structure」。

**目录命名。** 仓库目录统一使用 PascalCase（`Assets/`、`Docs/`、`Packaging/`、`Resources/`、`Scripts/`、`Site/`、`Tools/`、`UniversalDeviceToolkit.*/`）。只有存在外部既定拼写的子目录例外：`Packaging/winget`、`Packaging/scoop` 是工具名，`Docs/Skills/udt-hardware-cli` 是 skill 标识，`UniversalDeviceToolkit.Electron/` 内部遵循 Node 项目布局（`src/`、`tests/`、`resources/`）。`Resources/` 发布到 GitHub Pages 时路径保持小写 `/resources/`，因为已安装的客户端按该 URL 拉取（`AppIdentity.ResourcesBaseUrl`），不要改动发布路径。

<br/>

**1. 在报告 Issue 前请仔细阅读 README**

绝大多数常见问题的解决方法和重要信息都已在 [README](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/blob/master/README_zh-hans.md) 内阐明。请务必在报告 Issue 或发起讨论前通读其中的内容。

**2. 检查已被报告的 Issues**

请检查项目仓库下的 [Issues](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/issues?q=is%3Aissue) 和 [Discussions](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/discussions?discussions_q=) 栏目。请不要报告重复的 Issue 或发起重复的讨论。即使你找到的 Issue 已经被关闭，你一样可以在那里留言。

**3. 使用英语**

这会让所有人之间的交流都更加便利。

译者提示：若你无法流利地使用英语表达，你可以在使用中文完成草稿后使用百度翻译或 [DeepL](https://www.deepl.com/zh/translator) 等翻译网站或软件将草稿翻译为英语后提交。

**4. 尊重项目目标**

这不是一个万能的应用。愿景是：对已有可测 provider 的机器做可信本机硬件控制（联想拯救者优先，其他品牌仅在有真实 provider 时声称），并给人与 Agent 共用 CLI。请勿提通用 Windows 工具类需求。新品牌需要可测 provider，而不是一条功能请求 Issue。

**5. 在新建 Issue 前审查你的问题**

请确保 Bug 确实是 UDT 的 Bug。这不是一个免费的系统故障排除论坛，如果你在使用被修改过的 Windows 版本或你的系统本身已经出现问题，请自行解决。

**6. 尽所能详细描述你的问题**

详细的描述是解决问题的关键所在。请在新建 Issue 时填写表单内的所有项目并提供日志文件。只有提供良好的描述我们才能更快地解决问题。

**7. 为你的 Issue 或讨论起一个好的标题**

这样可以极大方便浏览 Issue 和讨论列表。“使用 UDT 时出现问题”并不是一个好的标题。

**8. 围绕主题**

不要发表与主题无关或无意义的留言。

**9. 一个 Issue 一个问题**

请不要在一个 Issue 内同时报告多个问题或请求添加多个功能。请为每一个问题、主题或想法新建一个单独的 Issue 或讨论，这会让后期跟进更加容易。

**10. 翻译**

我们使用 [Crowdin](https://crowdin.com/project/llt) 作为软件翻译平台。如果你想为翻译做出贡献，请在那里申请访问项目的权限。

**11. Pull requests**

我们欢迎你提交 PR（当然了）。除非你提交了一个非常简单易懂的 PR，请先创建一个 Issue 并描述你正在解决的问题。为一个会被拒绝的点子花时间并没有什么意义，因为这不符合本项目的愿景。同时请遵循现有的代码风格和项目组织。

<br/>

再次感谢你花时间帮助项目变得更好！
