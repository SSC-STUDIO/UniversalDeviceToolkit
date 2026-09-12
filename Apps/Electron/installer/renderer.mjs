import { defaultFeatures, normalizeFeatures, OPTIONAL_FEATURES } from './features.mjs'
import { installerText } from './i18n.mjs'

const api = window.installerApi
const appRoot = document.querySelector('#app')
const languageOptions = [
  ['en', 'English'], ['zh-CN', '简体中文'], ['zh-Hant', '繁體中文'], ['ja', '日本語'],
  ['de', 'Deutsch'], ['fr', 'Français'], ['es', 'Español'], ['it', 'Italiano'],
  ['pt-BR', 'Português (Brasil)'], ['pt', 'Português'], ['ru', 'Русский'], ['uk', 'Українська'],
  ['pl', 'Polski'], ['cs', 'Čeština'], ['sk', 'Slovenčina'], ['hu', 'Magyar'],
  ['ro', 'Română'], ['bg', 'Български'], ['tr', 'Türkçe'], ['el', 'Ελληνικά'],
  ['ar', 'العربية'], ['lv', 'Latviešu'], ['nl-NL', 'Nederlands'], ['vi', 'Tiếng Việt'],
  ['uz-Latn-UZ', 'O‘zbekcha']
]

const state = {
  page: api.isUninstaller ? 'uninstall' : 'welcome',
  info: null,
  destination: '',
  language: 'zh-CN',
  deviceMode: 'auto',
  features: defaultFeatures(),
  installing: false,
  installedExecutable: '',
  progress: { percent: 0, file: '', message: '' },
  error: '',
  themeMode: 'dark',
  accentColor: '#ff2a38',
  scale: 1.0
}

function formatBytes(value) {
  if (typeof value !== 'number' || !Number.isFinite(value) || value < 0) return '检测中...'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let number = value
  let unit = 0
  while (number >= 1024 && unit < units.length - 1) { number /= 1024; unit += 1 }
  return `${number.toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, (character) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]))
}

function windowsIcon() {
  return `<svg class="platform-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M2 4.5 10.8 3.3v8.4H2V4.5Zm10.1-1.4L22 1.7v10h-9.9V3.1ZM2 12.9h8.8v8.4L2 20.1v-7.2Zm10.1 0H22v10l-9.9-1.4v-8.6Z" fill="currentColor"/></svg>`
}

function minimizeIcon() {
  return `<svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true"><path d="M0 5h10" stroke="currentColor" stroke-width="1.1" stroke-linecap="round"/></svg>`
}

function closeIcon() {
  return `<svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true"><path d="M1 1l8 8m0-8l-8 8" stroke="currentColor" stroke-width="1.1" stroke-linecap="round"/></svg>`
}

function accentForeground(accent) {
  const weights = [0.2126, 0.7152, 0.0722]
  const luminance = [1, 3, 5]
    .map((offset) => Number.parseInt(accent.slice(offset, offset + 2), 16) / 255)
    .map((channel) => channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4)
    .reduce((sum, channel, index) => sum + channel * weights[index], 0)
  return luminance > 0.48 ? '#111111' : '#ffffff'
}

function applyTheme(theme) {
  const mode = theme?.mode === 'light' ? 'light' : 'dark'
  const accent = typeof theme?.accent === 'string' && /^#[0-9a-f]{6}$/i.test(theme.accent)
    ? theme.accent
    : '#ff2a38'
  state.themeMode = mode
  state.accentColor = accent
  document.documentElement.dataset.theme = mode
  document.documentElement.style.setProperty('--accent', accent)
  document.documentElement.style.setProperty('--accent-foreground', accentForeground(accent))
}

function shell() {
  const info = state.info ?? { version: '6.1.1', architecture: 'Windows x64', payloadBytes: 0, availableBytes: null, logoData: '' }
  const logo = info.logoData
    ? `<div class="brand-logo-frame"><img class="brand-logo" src="${escapeHtml(info.logoData)}" alt="Universal Device Toolkit" /></div>`
    : '<div class="brand-mark" aria-label="Universal Device Toolkit">U</div>'
  return `<div class="window">
    <div class="titlebar"><div class="window-controls"><button class="scale-toggle" data-action="toggle-scale" aria-label="调整界面大小" title="点击切换界面缩放（标准 100% / 紧凑 85% / 放大 115%）">${Math.round(state.scale * 100)}%</button><button data-action="minimize" aria-label="最小化">${minimizeIcon()}</button><button class="close" data-action="close" aria-label="关闭">${closeIcon()}</button></div></div>
    <aside class="brand-panel"><div class="brand-content">
      ${logo}
      <h1 class="brand-name">Universal Device Toolkit</h1><div class="brand-rule"></div><div class="brand-version">${escapeHtml(info.version)}</div>
      <div class="brand-badges"><div class="brand-badge">${windowsIcon()}<span>${escapeHtml(info.architecture)}</span></div><div class="brand-badge"><span class="dot"></span> ${info.isOnline ? '在线安装' : '本地运行'}</div></div>
    </div></aside>
    <main class="content-panel"><div class="content-inner">${renderPage(info)}</div></main>
  </div>`
}

function text(key) {
  return installerText(state.language, key)
}

function renderSteps(active) {
  const steps = [
    ['welcome', '位置'],
    ['language', '语言'],
    ['device', '设备'],
    ['features', text('stepFeatures')],
    ['install', '安装']
  ]
  const activeIndex = steps.findIndex(([id]) => id === active)
  return `<nav class="steps" aria-label="安装进度">${steps.map(([, name], index) => `<div class="step ${index === activeIndex ? 'active' : ''} ${index < activeIndex ? 'done' : ''}" data-step="${index < activeIndex ? '' : index + 1}">${name}</div>`).join('')}</nav>`
}

function renderPage(info) {
  if (state.page === 'uninstall') return renderUninstall(info)
  if (state.page === 'language') return renderLanguage()
  if (state.page === 'device') return renderDevice()
  if (state.page === 'features') return renderFeatures()
  if (state.page === 'install') return renderInstall(info)
  if (state.page === 'complete') return renderComplete()
  return renderWelcome(info)
}

function renderError() {
  if (!state.error) return ''
  return `<div class="error-banner" role="alert"><span class="error-icon" aria-hidden="true">⚠</span><div class="error-text">${escapeHtml(state.error)}</div></div>`
}

function renderWelcome(info) {
  return `<h2 class="heading">准备安装</h2><p class="subtitle">选择安装位置，然后开始安装。</p><div class="divider"></div>${renderSteps('welcome')}
    <section class="page"><h3 class="section-title">安装位置</h3><label class="field-label" for="destination">选择 Universal Device Toolkit 要安装的文件夹</label>
      <div class="path-row"><input id="destination" class="path-input" value="${escapeHtml(state.destination)}" spellcheck="false"/><button class="button-secondary" data-action="browse">浏览</button></div>
      <div class="space-card"><div class="space-item"><div class="space-label">需要空间</div><div class="space-value">${formatBytes(info.payloadBytes)}</div></div><div class="space-item"><div class="space-label">可用空间</div><div class="space-value">${formatBytes(info.availableBytes)}</div></div></div>
      <div class="info-card admin"><span class="info-icon">i</span><span>需要管理员权限</span></div>
    </section>${renderError()}<div class="footer"><button class="button-secondary" data-action="close">取消</button><button class="button-secondary" data-action="next">自定义选项</button><button class="button-primary" data-action="quick-install">立即安装</button></div>`
}

function renderLanguage() {
  return `<h2 class="heading">语言选择</h2><p class="subtitle">选择安装后首次启动使用的语言。</p><div class="divider"></div>${renderSteps('language')}<section class="page"><div class="language-grid">${languageOptions.map(([id, label]) => `<button class="language-option ${state.language === id ? 'selected' : ''}" data-language="${id}" aria-pressed="${state.language === id}">${label}</button>`).join('')}</div></section>${renderError()}${footer('下一步', true)}`
}

function renderDevice() {
  return `<h2 class="heading">设备选择</h2><p class="subtitle">选择启动时使用的设备支持模式。</p><div class="divider"></div>${renderSteps('device')}<section class="page"><div class="choice-grid" role="radiogroup" aria-label="设备支持模式">
    <button class="choice-card ${state.deviceMode === 'auto' ? 'selected' : ''}" data-device="auto" role="radio" aria-checked="${state.deviceMode === 'auto'}"><span class="choice-radio"></span><span><span class="choice-name">自动检测设备</span><span class="choice-description">启用完整的硬件监控和设备功能。</span></span><span class="choice-meta">推荐</span></button>
    <button class="choice-card ${state.deviceMode === 'basic' ? 'selected' : ''}" data-device="basic" role="radio" aria-checked="${state.deviceMode === 'basic'}"><span class="choice-radio"></span><span><span class="choice-name">基础模式</span><span class="choice-description">不读取硬件传感器，适用于受限环境。</span></span><span class="choice-meta">安全</span></button>
  </div></section>${renderError()}${footer('下一步', true)}`
}

function featureRow(id, locked) {
  const selected = locked || state.features[id] === true
  const disabled = locked || (id === 'networkAcceleration' && !state.features.windowsOptimization)
  const classes = ['feature-row', selected ? 'selected' : '', locked ? 'locked' : '', disabled && !locked ? 'disabled' : '']
    .filter(Boolean)
    .join(' ')
  return `<button type="button" class="${classes}" data-feature="${locked ? '' : id}" ${disabled ? 'disabled' : ''} role="checkbox" aria-checked="${selected}" aria-disabled="${disabled}">
    <span class="feature-check" aria-hidden="true"></span>
    <span><span class="feature-name">${escapeHtml(text(id))}</span><span class="feature-desc">${escapeHtml(text(`${id}Desc`))}</span></span>
  </button>`
}

function renderFeatures() {
  const required = ['hostRuntime', 'electronShell', 'dashboard', 'settings', 'about']
  const optional = OPTIONAL_FEATURES
  return `<h2 class="heading">${escapeHtml(text('featuresTitle'))}</h2><p class="subtitle">${escapeHtml(text('featuresSubtitle'))}</p><div class="divider"></div>${renderSteps('features')}
    <section class="page"><div class="feature-groups">
      <div class="feature-group"><div class="feature-group-title">${escapeHtml(text('requiredGroup'))}</div><div class="feature-group-hint">${escapeHtml(text('requiredHint'))}</div>${required.map((id) => featureRow(id, true)).join('')}</div>
      <div class="feature-group"><div class="feature-group-title">${escapeHtml(text('optionalGroup'))}</div><div class="feature-group-hint">${escapeHtml(text('optionalHint'))}</div>${optional.map((id) => featureRow(id, false)).join('')}</div>
    </div></section>${renderError()}${footer(text('startInstall'), true)}`
}

function featuresSummaryLabel() {
  const omitted = OPTIONAL_FEATURES.filter((id) => state.features[id] !== true)
  return omitted.length === 0 ? text('featuresSummaryFull') : text('featuresSummaryCustom')
}

function renderInstall(info) {
  const progress = Math.max(0, Math.min(100, state.progress.percent))
  return `<h2 class="heading">正在安装</h2><p class="subtitle">请稍候，Universal Device Toolkit 正在准备运行环境。</p><div class="divider"></div>${renderSteps('install')}<section class="page"><div class="install-summary"><div class="summary-line"><span>安装位置</span><strong>${escapeHtml(state.destination)}</strong></div><div class="summary-line"><span>语言</span><strong>${escapeHtml(languageOptions.find(([id]) => id === state.language)?.[1] ?? state.language)}</strong></div><div class="summary-line"><span>设备模式</span><strong>${state.deviceMode === 'auto' ? '自动检测设备' : '基础模式'}</strong></div><div class="summary-line"><span>${escapeHtml(text('featuresSummary'))}</span><strong>${escapeHtml(featuresSummaryLabel())}</strong></div></div><div class="progress-track"><div class="progress-bar" style="width:${progress}%"></div></div><div class="progress-caption"><span>${escapeHtml(state.progress.file || '正在复制应用文件...')}</span><span>${progress.toFixed(0)}%</span></div><div class="log-line">${escapeHtml(state.progress.message || `需要空间 ${formatBytes(info.payloadBytes)}`)}</div></section>${renderError()}<div class="footer"><button class="button-secondary" data-action="close" ${state.installing ? 'disabled' : ''}>取消</button></div>`
}

function renderComplete() {
  return `<section class="page success"><div><div class="success-mark">✓</div><h2>安装完成</h2><p>Universal Device Toolkit 已安装到你的设备。</p>${renderError()}<div class="footer"><button class="button-secondary" data-action="close">关闭</button><button class="button-primary" data-action="launch">启动应用</button></div></div></section>`
}

function renderUninstall() {
  return `<h2 class="heading">卸载 Universal Device Toolkit</h2><p class="subtitle">移除应用及其快捷方式。</p><div class="divider"></div><section class="page"><div class="info-card"><span class="info-icon">i</span><span>应用程序文件和开始菜单、桌面快捷方式将被移除。</span></div><div class="info-card admin"><span class="info-icon">!</span><span>卸载需要管理员权限</span></div></section>${renderError()}<div class="footer"><button class="button-secondary" data-action="close">取消</button><button class="button-primary" data-action="uninstall">卸载</button></div>`
}

function footer(primary, back) {
  return `<div class="footer">${back ? '<button class="button-secondary button-back" data-action="back">返回</button>' : ''}<button class="button-secondary" data-action="close">取消</button><button class="button-primary" data-action="next">${primary}</button></div>`
}

function render() {
  appRoot.innerHTML = shell()
  if (state.page === 'welcome') document.querySelector('#destination')?.focus()
}

async function startInstall() {
  state.error = ''
  state.features = normalizeFeatures(state.features)
  state.page = 'install'
  state.installing = true
  render()
  try {
    const result = await api.install({
      destination: state.destination,
      language: state.language,
      deviceMode: state.deviceMode,
      features: state.features
    })
    state.installedExecutable = result.executable
    state.installing = false
    state.page = 'complete'
    render()
  } catch (error) {
    state.installing = false
    state.page = 'welcome'
    state.error = error instanceof Error ? error.message : String(error)
    render()
  }
}

async function next() {
  state.error = ''
  if (state.page === 'welcome') {
    state.destination = document.querySelector('#destination')?.value.trim() || state.destination
    if (!state.destination) { state.error = '请选择安装位置。'; render(); return }
    state.page = 'language'
  } else if (state.page === 'language') state.page = 'device'
  else if (state.page === 'device') state.page = 'features'
  else if (state.page === 'features') {
    await startInstall()
    return
  }
  render()
}

function back() {
  state.error = ''
  if (state.page === 'language') state.page = 'welcome'
  else if (state.page === 'device') state.page = 'language'
  else if (state.page === 'features') state.page = 'device'
  render()
}

function toggleFeature(id) {
  if (!OPTIONAL_FEATURES.includes(id)) return
  if (id === 'networkAcceleration' && !state.features.windowsOptimization) return
  state.features[id] = !state.features[id]
  if (id === 'windowsOptimization' && !state.features.windowsOptimization) {
    state.features.networkAcceleration = false
  }
}

appRoot.addEventListener('click', async (event) => {
  const target = event.target instanceof Element ? event.target.closest('[data-action], [data-language], [data-device], [data-feature]') : null
  if (!(target instanceof HTMLElement)) return
  if (target.dataset.action === 'minimize') api.minimize()
  else if (target.dataset.action === 'close') api.close()
  else if (target.dataset.action === 'toggle-scale') {
    const scales = [1.0, 0.85, 1.15]
    const nextIndex = (scales.indexOf(state.scale) + 1) % scales.length
    state.scale = scales[nextIndex]
    document.documentElement.style.zoom = String(state.scale)
    render()
  } else if (target.dataset.action === 'browse') {
    const selected = await api.chooseDirectory()
    if (selected) { state.destination = selected; render() }
  } else if (target.dataset.action === 'quick-install') {
    state.destination = document.querySelector('#destination')?.value.trim() || state.destination
    if (!state.destination) { state.error = '请选择安装位置。'; render(); return }
    await startInstall()
  } else if (target.dataset.action === 'next') await next()
  else if (target.dataset.action === 'back') back()
  else if (target.dataset.action === 'launch') { await api.launch(state.installedExecutable); api.close() }
  else if (target.dataset.action === 'uninstall') {
    target.setAttribute('disabled', 'true')
    try { await api.uninstall(); state.error = '卸载已开始，窗口将关闭。'; render(); setTimeout(() => api.close(), 500) }
    catch (error) { state.error = error instanceof Error ? error.message : String(error); render() }
  } else if (target.dataset.language) { state.language = target.dataset.language; render() }
  else if (target.dataset.device) { state.deviceMode = target.dataset.device; render() }
  else if (target.dataset.feature) { toggleFeature(target.dataset.feature); render() }
})

appRoot.addEventListener('input', (event) => {
  if (event.target instanceof HTMLInputElement && event.target.id === 'destination') state.destination = event.target.value
})

api.onProgress((progress) => {
  state.progress = {
    percent: Number(progress.percent ?? 0),
    file: String(progress.file ?? ''),
    message: progress.phase === 'warning' ? String(progress.message ?? '') : String(progress.phase ?? '')
  }
  if (progress.phase === 'warning') state.error = state.progress.message
  const bar = document.querySelector('.progress-bar')
  const caption = document.querySelector('.progress-caption')
  const log = document.querySelector('.log-line')
  if (bar instanceof HTMLElement) bar.style.width = `${state.progress.percent}%`
  if (caption instanceof HTMLElement) caption.innerHTML = `<span>${escapeHtml(state.progress.file || '正在复制应用文件...')}</span><span>${state.progress.percent.toFixed(0)}%</span>`
  if (log instanceof HTMLElement) log.textContent = state.progress.message || `正在安装 ${state.progress.file}`
})

api.onThemeChanged((theme) => { applyTheme(theme); render() })
api.getInfo().then((info) => {
  state.info = info
  state.destination = info.defaultPath
  applyTheme(info.theme)
  if (info.isUninstaller) state.page = 'uninstall'
  render()
}).catch((error) => { state.error = error instanceof Error ? error.message : String(error); render() })
