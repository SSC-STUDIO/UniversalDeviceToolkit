# 中文发布文案

核验日期：2026-09-12。当前稳定版为 **v6.1.1**，安装入口为 [GitHub Releases](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest)。以下按维护者自荐撰写；发布状态见 [SUBMISSIONS.md](SUBMISSIONS.md)。

## 一句话介绍

我在维护 UDT，一款面向受支持联想拯救者、LOQ 等机型的开源 Windows 硬件工具，把性能模式、键盘灯和电池养护放在一个界面里，无需账号。

## B 站视频

**标题**：拯救者性能模式、RGB、电池养护，一个开源工具来管｜UDT

**简介**：

我是 Universal Device Toolkit（UDT）的维护者。这是基于 Lenovo Legion Toolkit 的独立维护项目，采用 GPL-3.0 开源协议。

在受支持的联想拯救者、LOQ、IdeaPad Gaming 机型上，可以管理性能模式、键盘灯、显卡模式和电池养护；具体选项取决于机型、固件和驱动。

UDT 无需账号，不收集遥测，也不安装独立 Windows 后台服务。需要持续运行的自动化等功能，请让应用留在托盘。不支持的硬件控制会隐藏。

项目与中文说明：https://github.com/SSC-STUDIO/UniversalDeviceToolkit/blob/master/README_zh-hans.md
下载 v6.1.1：https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest

欢迎在评论区留下「完整机型 + Windows 版本 + 想用的功能」。如果项目对你有用，欢迎给仓库点个 Star，方便以后找到更新。

**建议标签**：开源软件、联想拯救者、电脑工具、GitHub、笔记本

**素材**：[中文界面截图](../../Assets/Screenshot_zh-hans.png)、[现有中文宣传片](../../Assets/UDT_Promo_zh.mp4)、[现有封面](../../Assets/UDT_Promo_poster.jpg)。旧宣传片发布前逐句检查字幕与画面，避免把旧版界面、资源占用或兼容性承诺当成 v6.1.1 的实测结论。

### 新录 30 秒演示脚本

| 时间 | 画面 | 旁白 |
| --- | --- | --- |
| 0-5 秒 | UDT 主界面，注明真实机型与应用版本 | 拯救者的性能模式、键盘灯和电池养护，能不能放到一个工具里？ |
| 5-12 秒 | 展示当前机器实际可用的性能模式 | 我在维护的 UDT，是基于 Lenovo Legion Toolkit 的开源 Windows 工具。 |
| 12-20 秒 | 展示键盘灯、电池养护页面；仅演示支持的选项 | 支持哪些控制，要看你的机型和驱动。没有支持的硬件选项会隐藏。 |
| 20-26 秒 | 设置页和托盘 | 不用账号、不收集遥测。自动化要持续工作，就让应用留在托盘。 |
| 26-30 秒 | 仓库名称、Releases 入口 | 下载在简介里，欢迎反馈你的机型；有用的话给项目点个 Star。 |

### B 站动态

维护者自荐：我在做 Universal Device Toolkit（UDT），基于 Lenovo Legion Toolkit 的开源 Windows 硬件工具。

面向受支持的拯救者、LOQ 等机型，集中管理性能模式、RGB、显卡模式和电池养护。无需账号、不收集遥测；具体功能看机型和驱动。不支持的硬件控制会隐藏，自动化等常驻功能需要保留托盘运行。

当前稳定版 v6.1.1，中文截图和安装包都在仓库。欢迎带「机型 + Windows 版本 + 想用的功能」来提建议。有用的话，欢迎点个 Star。
https://github.com/SSC-STUDIO/UniversalDeviceToolkit

## V2EX 分享创造

**标题**：[自荐] UDT：给拯救者 / LOQ 管性能模式、键盘灯和电池的开源工具

**正文**：

大家好，我在维护 Universal Device Toolkit（UDT），一个基于 Lenovo Legion Toolkit 的独立开源项目，协议是 GPL-3.0。这次想找有实际机型的朋友试用，补充兼容性反馈。

它主要解决一个具体需求：在受支持的联想拯救者、LOQ、IdeaPad Gaming 笔记本上，把性能模式、键盘灯、显卡模式和电池养护放到一个界面里管理。可用控制取决于机型、固件和驱动。

目前稳定版是 v6.1.1，提供 Windows 安装包。UDT 无需账号、不收集遥测，也不安装独立 Windows 后台服务。自动化等需要持续运行的功能要让应用留在托盘，完全退出就不会继续工作。

几个试用前需要知道的点：

- 并非所有电脑都能完整控硬件；不支持的机器会进入基础模式，隐藏对应开关。
- UI 使用 Electron，硬件逻辑由 .NET Host 处理。资源占用请以自己机器的实测为准。
- 6.1 已移除旧插件系统，相关功能改为内置；下载请用 Releases。
- 正式安装包面向 Windows；macOS / Linux 目前是实验性开发支持。

![UDT 简体中文界面](https://raw.githubusercontent.com/SSC-STUDIO/UniversalDeviceToolkit/master/Assets/Screenshot_zh-hans.png)

项目：https://github.com/SSC-STUDIO/UniversalDeviceToolkit
下载：https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest
中文说明：https://github.com/SSC-STUDIO/UniversalDeviceToolkit/blob/master/README_zh-hans.md

欢迎回复「完整机型 + Windows 版本 + 希望控制的功能」，也欢迎直接开 Issue。请不要贴序列号等个人设备标识。如果项目对你有用，欢迎点个 Star；遇到问题的反馈同样有帮助。

## HelloGitHub 原投稿更新

原帖：https://github.com/521xueweihan/HelloGitHub/issues/3383 。编辑原帖；有实际版本更新时补充一次说明。不要重复开自荐 Issue。

以下为按投稿表单整理的完整正文。

<!-- hellogithub-body-start -->
### 项目地址

https://github.com/SSC-STUDIO/UniversalDeviceToolkit

### 类别

C#

### 项目标题

管理拯救者性能模式、键盘灯和电池的开源工具

### 项目描述

Universal Device Toolkit（UDT）是基于 Lenovo Legion Toolkit 的独立维护项目，采用 GPL-3.0 协议。在受支持的联想拯救者、LOQ、IdeaPad Gaming 机型上集中管理性能模式、键盘灯、显卡模式和电池养护。无需账号、不收集遥测，不安装独立 Windows 后台服务；不支持的硬件控制会隐藏。当前稳定版 v6.1.1 提供 Windows 安装包。

### 亮点

- 直接管理日常硬件功能：性能模式、RGB、显卡模式、电池养护；实际可用项取决于机型、固件和驱动。
- Electron 界面与 .NET Host 分工，源码包含硬件控制、JSON-RPC 通信和自动化实现，适合研究桌面工具开发。
- `udt` CLI 提供状态查询与支持的设备控制，Actions 整合自动化与宏。CLI 需要应用运行并在设置中启用。
- 6.1 已退役插件系统，相关功能改为内置；通过 Releases 获取安装包和校验文件。
- 不安装独立 Windows 后台服务。自动化等常驻功能需要让应用留在托盘。

### 示例代码

安装并运行 UDT 后，在「设置 → 集成」中开启 CLI，再运行：

```powershell
udt doctor --json
udt status --json
```

下载：https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest

### 截图或演示视频

![UDT 简体中文界面](https://raw.githubusercontent.com/SSC-STUDIO/UniversalDeviceToolkit/master/Assets/Screenshot_zh-hans.png)

[中文说明](https://github.com/SSC-STUDIO/UniversalDeviceToolkit/blob/master/README_zh-hans.md)

**作者声明**：我是 UDT 维护者 SSC-STUDIO，本次为自荐。UDT 是基于 Lenovo Legion Toolkit 的独立维护项目；上游曾获 HelloGitHub 推荐，不代表 UDT 已被收录。正式发布的桌面安装包面向 Windows，macOS / Linux 为实验性开发支持。

更新日期：2026-09-12。
<!-- hellogithub-body-end -->

## 常见问题回复

**支持我的机器吗？** 请提供完整机型、Windows 版本及具体功能。系列名称不足以判断所有开关都可用，需要核对硬件、固件和驱动。

**退出后还能运行自动化吗？** 完全退出就不会继续。UDT 不安装独立 Windows 后台服务，需要让应用留在托盘。

**如何安装？** 打开 Releases，普通 Windows 用户选择 Full Setup 安装包；同一版本提供 SHA256 校验文件。

**与原版 Legion Toolkit 的关系？** UDT 是基于该项目的独立维护项目，遵循 GPL-3.0；不代表上游作者或联想官方。
