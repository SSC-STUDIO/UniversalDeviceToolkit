# buildResources — 各平台打包资源说明

本目录是 electron-builder 的 buildResources（见 `electron-builder.yml` 的
`directories.buildResources`）。平台无关的打包辅助文件都放在这里，统一从主仓
`Assets/`（单一事实来源）复制或生成，禁止在此维护第二份品牌源文件。

## 图标要求（按平台）

| 平台 | 必需资源 | 现状 | 说明 |
|---|---|---|---|
| Windows | `icon.ico`（至少 256x256，含 16/32/48/256） | ✅ 已有 | 来自 `Assets/Icon.ico`；nsis 安装器/卸载器图标同源 |
| Linux | `icons/` 目录（`<size>x<size>.png`，建议含 512） | ✅ 16–512 | 来自 `Assets/Brand/icon-*.png`；`512x512.png` 由 `buildResources/icon-512.png` 复制而来 |
| macOS | `icon.icns` | ✅ 已有 | 由 `buildResources/icon-512.png`（512x512 ARGB）经 `electron-icon-builder` 生成，含 ic07–ic14 全部所需尺寸 |
| macOS 托盘 | `resources/trayTemplate.png`（+ `trayTemplate@2x.png`） | ✅ 已有 | 来自 `resources/tray-light.png`（32x32）转为纯黑+透明单色 template 图；`src/main/tray.ts` 在 macOS 优先加载并 `setTemplateImage(true)` |

## macOS 图标生成记录

`buildResources/icon.icns` 已在 Windows 上用一次性工具生成（无需 macOS/iconutil）：

```powershell
npx -y electron-icon-builder --input=buildResources/icon-512.png --output=<临时目录>
# 生成的 <临时目录>/icons/mac/icon.icns 复制为 buildResources/icon.icns
```

若未来需要重新生成：用主仓 `Assets/Brand/` 更新后的最大尺寸 PNG（如
`buildResources/icon-512.png`）重复上述命令即可。

无需改动 `electron-builder.yml`——`mac` 块未显式指定 `icon`，
electron-builder 会自动探测 `buildResources/icon.icns`。

## TODO：macOS 签名 / 公证

`electron-builder.yml` 的 `mac` 块内已注释 `hardenedRuntime` /
`entitlements` / `notarize` 配置。取得 Apple Developer ID 证书
（`CSC_LINK` + `CSC_KEY_PASSWORD`）与公证凭据（`APPLE_ID` /
`APPLE_APP_SPECIFIC_PASSWORD`）后，按注释内容启用。

## 维护

- Linux 图标集更新：`Assets/Brand/` 更新后，重新同步 `icons/` 即可。
- 不要在此目录提交 `.icns` 以外的新品牌源文件；来源一律写 `Assets/` 路径。
