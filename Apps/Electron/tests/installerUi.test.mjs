import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'
import test from 'node:test'

const installerSource = readFileSync(
  fileURLToPath(new URL('../buildResources/installer.nsh', import.meta.url)),
  'utf8'
)
const buildScriptSource = readFileSync(
  fileURLToPath(new URL('../scripts/build-custom-installer.mjs', import.meta.url)),
  'utf8'
)
const builderHookSource = readFileSync(
  fileURLToPath(new URL('../scripts/installer-builder-hooks.mjs', import.meta.url)),
  'utf8'
)
const customInstallerConfig = readFileSync(
  fileURLToPath(new URL('../custom-installer.yml', import.meta.url)),
  'utf8'
)
const mainSource = readFileSync(
  fileURLToPath(new URL('../installer/main.mjs', import.meta.url)),
  'utf8'
)
const rendererSource = readFileSync(
  fileURLToPath(new URL('../installer/renderer.mjs', import.meta.url)),
  'utf8'
)
const styleSource = readFileSync(
  fileURLToPath(new URL('../installer/styles.css', import.meta.url)),
  'utf8'
)
const i18nSource = readFileSync(
  fileURLToPath(new URL('../installer/i18n.mjs', import.meta.url)),
  'utf8'
)
const featuresSource = readFileSync(
  fileURLToPath(new URL('../installer/features.mjs', import.meta.url)),
  'utf8'
)

test('installer uses a real Windows four-pane icon for the platform badge', () => {
  assert.match(rendererSource, /class="platform-icon"/)
  assert.match(rendererSource, /viewBox="0 0 24 24"/)
  assert.doesNotMatch(rendererSource, /▦/)
  assert.match(styleSource, /\.platform-icon\s*\{[^}]*width:\s*17px/)
  assert.match(rendererSource, /brand-logo-frame/)
  assert.match(rendererSource, /logoData/)
})

test('installer keeps the reference visual language and setup choices', () => {
  assert.match(rendererSource, /准备安装/)
  assert.match(rendererSource, /语言选择/)
  assert.match(rendererSource, /设备选择/)
  assert.match(rendererSource, /featuresTitle/)
  assert.match(rendererSource, /data-feature/)
  assert.match(rendererSource, /需要管理员权限/)
  assert.doesNotMatch(rendererSource, /不会修改设备设置，安装后由你控制/)
  assert.doesNotMatch(rendererSource, /安装器只保存你的选择，不会修改设备配置/)
  assert.doesNotMatch(installerSource, /不会修改设备设置，安装后由你控制/)
  assert.match(rendererSource, /onThemeChanged/)
  assert.match(rendererSource, /dataset\.theme/)
  assert.match(rendererSource, /--accent-foreground/)
  assert.match(rendererSource, /aria-checked/)
  assert.match(mainSource, /systemPreferences\.getAccentColor/)
  assert.match(mainSource, /nativeTheme\.themeSource = previewThemeMode \?\? 'system'/)
  assert.match(mainSource, /systemPreferences\.on\('color-changed'/)
  assert.match(styleSource, /--accent:\s*#ff2a38/)
  assert.match(styleSource, /--accent-soft:\s*color-mix\(in srgb, var\(--accent\)/)
  assert.match(styleSource, /data-theme="dark"/)
  assert.match(styleSource, /prefers-reduced-motion:\s*reduce/)
  assert.match(styleSource, /\.choice-description\s*\{[^}]*display:\s*block/)
  assert.match(styleSource, /\.brand-panel\s*\{/)
})

test('custom installer feature page persists optional modules and can omit NetworkProxy', () => {
  assert.match(i18nSource, /选择功能/)
  assert.match(i18nSource, /Choose features/)
  assert.match(i18nSource, /系统优化/)
  assert.match(i18nSource, /网络加速/)
  assert.match(featuresSource, /windowsOptimization/)
  assert.match(featuresSource, /networkAcceleration/)
  assert.match(mainSource, /normalizeFeatures/)
  assert.match(mainSource, /isNetworkProxySidecarFile/)
  assert.match(mainSource, /installer-selection.ini/)
  assert.match(installerSource, /Page custom UdtFeaturesPage UdtFeaturesLeave/)
  assert.match(installerSource, /windowsOptimization/)
  assert.match(installerSource, /UniversalDeviceToolkit.NetworkProxy.exe/)
  assert.match(styleSource, /\.feature-groups\s*\{/)
})

const customOnlineInstallerConfig = readFileSync(
  fileURLToPath(new URL('../custom-online-installer.yml', import.meta.url)),
  'utf8'
)

test('custom installer rebuilds its application payload before packaging', () => {
  const payloadBuild = buildScriptSource.indexOf("'electron-builder.yml', '--win', 'dir'")
  const installerBuild = buildScriptSource.indexOf("'custom-installer.yml', '--win', 'portable'")
  assert.ok(payloadBuild >= 0)
  assert.ok(installerBuild > payloadBuild)
  assert.match(customInstallerConfig, /beforeBuild:\s*\.\/scripts\/installer-builder-hooks\.mjs/)
  assert.match(customInstallerConfig, /requestedExecutionLevel:\s*asInvoker/)
  assert.match(customInstallerConfig, /requestExecutionLevel:\s*user/)
  assert.match(customOnlineInstallerConfig, /requestedExecutionLevel:\s*asInvoker/)
  assert.match(customOnlineInstallerConfig, /requestExecutionLevel:\s*user/)
  assert.match(mainSource, /isSilentSetupArgument/)
  assert.match(mainSource, /shouldRelaunchElevated/)
  assert.match(mainSource, /Start-Process -FilePath/)
  assert.match(mainSource, /setupIsSilent/)
  assert.match(mainSource, /downloadPayloadArchive/)
  assert.match(mainSource, /extractPayloadArchive/)
  assert.match(builderHookSource, /return false/)
})
