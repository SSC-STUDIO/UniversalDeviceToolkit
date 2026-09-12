const catalogs = {
  'en-US': {
    featuresTitle: 'Choose features',
    featuresSubtitle: 'Required components stay installed. Optional modules can be left out.',
    stepFeatures: 'Features',
    requiredGroup: 'Required',
    optionalGroup: 'Optional',
    requiredHint: 'Always installed so the app can start.',
    optionalHint: 'Included by default. Uncheck a module to omit it from this install.',
    hostRuntime: 'Host runtime',
    hostRuntimeDesc: 'Background service for settings, hardware, and device features.',
    electronShell: 'Desktop shell',
    electronShellDesc: 'The Universal Device Toolkit window.',
    dashboard: 'Console',
    dashboardDesc: 'Home dashboard.',
    settings: 'Settings',
    settingsDesc: 'Application settings.',
    about: 'About',
    aboutDesc: 'Version and project information.',
    windowsOptimization: 'System optimization',
    windowsOptimizationDesc: 'Cleanup, driver download, and related tools.',
    networkAcceleration: 'Network acceleration',
    networkAccelerationDesc: 'Optional local proxy worker. Requires System optimization.',
    automation: 'Automation',
    automationDesc: 'Automation pipelines.',
    macro: 'Custom macro',
    macroDesc: 'Keyboard and mouse macros.',
    keyboard: 'Keyboard',
    keyboardDesc: 'Keyboard backlight controls.',
    startInstall: 'Install',
    featuresSummary: 'Features',
    featuresSummaryFull: 'Full install',
    featuresSummaryCustom: 'Custom selection'
  },
  'zh-CN': {
    featuresTitle: '选择功能',
    featuresSubtitle: '基础组件会始终安装。可选模块可以取消勾选。',
    stepFeatures: '功能',
    requiredGroup: '必选',
    optionalGroup: '可选',
    requiredHint: '应用启动所必需，无法取消。',
    optionalHint: '默认全部勾选。取消勾选后，安装的应用不会显示对应界面。',
    hostRuntime: '主机运行时',
    hostRuntimeDesc: '后台服务，负责设置、硬件和设备功能。',
    electronShell: '桌面程序',
    electronShellDesc: 'Universal Device Toolkit 主窗口。',
    dashboard: '控制台',
    dashboardDesc: '主页仪表盘。',
    settings: '设置',
    settingsDesc: '应用程序设置。',
    about: '关于',
    aboutDesc: '版本与项目信息。',
    windowsOptimization: '系统优化',
    windowsOptimizationDesc: '清理、驱动下载及相关工具。',
    networkAcceleration: '网络加速',
    networkAccelerationDesc: '可选的本机代理组件。需要同时安装系统优化。',
    automation: '自动化',
    automationDesc: '自动化流程。',
    macro: '自定义宏',
    macroDesc: '键盘和鼠标宏。',
    keyboard: '键盘',
    keyboardDesc: '键盘背光控制。',
    startInstall: '开始安装',
    featuresSummary: '功能',
    featuresSummaryFull: '完整安装',
    featuresSummaryCustom: '自定义选择'
  }
}

export function installerLocale(language) {
  return language === 'zh-CN' ? 'zh-CN' : 'en-US'
}

export function installerText(language, key) {
  const locale = installerLocale(language)
  return catalogs[locale][key] ?? catalogs['en-US'][key] ?? key
}
