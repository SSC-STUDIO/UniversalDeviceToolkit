export const dashboardParityZhCN = {
  dashboardFeatureState: {
    off: '关闭',
    on: '开启',
    onWhenSleeping: '睡眠时开启',
    onAlways: '始终开启',
    acAdapter: '电源适配器',
    usbPowerDelivery: 'USB-PD',
    acAdapterAndUsbPowerDelivery: '电源适配器和 USB-PD',
    onIGPUOnly: '仅集成显卡',
    onAuto: '自动',
    default: '默认',
    iGPUOnly: '仅集成显卡',
    auto: '自动',
    none: '关闭',
    itsAuto: '智能散热',
    mmcCool: '节能',
    mmcPerformance: '性能',
    mmcGeek: '极客',
    low: '低',
    high: '高'
  },
  dashboardHardware: {
    apply: '应用',
    cancel: '取消',
    discreteGpu: {
      title: '独立显卡',
      description: '不需要时强制休眠显卡，可能导致应用崩溃。建议手动关闭占用进程来解除显卡占用。',
      information: '独立显卡信息',
      performance: '性能状态',
      processes: '占用进程',
      noProcesses: '没有进程正在占用独立显卡',
      deactivate: '强制休眠显卡',
      killProcesses: '结束占用进程',
      restart: '重启显卡',
      status: {
        Unknown: '-',
        NvidiaGpuNotFound: '未检测到',
        MonitorConnected: '活动中',
        Active: '活动中',
        Inactive: '空闲',
        PoweredOff: '已休眠'
      }
    },
    overclock: {
      title: '超频独立显卡',
      description: '超频独立显卡以提升性能。',
      settings: '独立显卡超频设置',
      coreOffset: '核心频率偏移',
      memoryOffset: '显存频率偏移'
    },
    turnOffMonitors: {
      title: '关闭显示器',
      description: '立即关闭所有连接的显示器。移动鼠标或按下按键即可重新唤醒。',
      action: '关闭显示器'
    }
  }
}

export const dashboardParityEnUS = {
  dashboardFeatureState: {
    off: 'Off',
    on: 'On',
    onWhenSleeping: 'On while sleeping',
    onAlways: 'Always on',
    acAdapter: 'Power adapter',
    usbPowerDelivery: 'USB-PD',
    acAdapterAndUsbPowerDelivery: 'Power adapter and USB-PD',
    onIGPUOnly: 'Integrated graphics only',
    onAuto: 'Automatic',
    default: 'Default',
    iGPUOnly: 'Integrated graphics only',
    auto: 'Automatic',
    none: 'Off',
    itsAuto: 'Intelligent cooling',
    mmcCool: 'Battery saving',
    mmcPerformance: 'Performance',
    mmcGeek: 'Geek mode',
    low: 'Low',
    high: 'High'
  },
  dashboardHardware: {
    apply: 'Apply',
    cancel: 'Cancel',
    discreteGpu: {
      title: 'Discrete GPU',
      description: 'Force the GPU to sleep when it is not needed. This may crash applications, so close GPU-using processes first.',
      information: 'Discrete GPU information',
      performance: 'Performance state',
      processes: 'Processes',
      noProcesses: 'No processes are using the discrete GPU',
      deactivate: 'Force GPU Sleep',
      killProcesses: 'Close GPU Processes',
      restart: 'Restart GPU',
      status: {
        Unknown: '-',
        NvidiaGpuNotFound: 'Not detected',
        MonitorConnected: 'Active',
        Active: 'Active',
        Inactive: 'Inactive',
        PoweredOff: 'Powered off'
      }
    },
    overclock: {
      title: 'Overclock Discrete GPU',
      description: 'Overclock the discrete GPU to improve performance.',
      settings: 'Discrete GPU Overclock Settings',
      coreOffset: 'Core frequency offset',
      memoryOffset: 'Memory frequency offset'
    },
    turnOffMonitors: {
      title: 'Turn Off Monitors',
      description: 'Immediately turn off all connected monitors. Move the mouse or press a key to wake them.',
      action: 'Turn Off'
    }
  }
}
