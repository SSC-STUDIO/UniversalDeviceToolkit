import { useMemo } from 'react'
import { create } from 'zustand'
import { useTranslation } from 'react-i18next'
import { useUtilsDialog } from './useUtilsDialog'
import './utils.css'

/**
 * Port of Electron ActionDetailsWindow: shows the action title/description plus the
 * technical implementation details (commands, registry tweaks or service
 * management entries) for a Windows optimization action key.
 *
 * The details mapping mirrors ActionDetailsWindow.GetActionImplementationDetails
 * and its helper methods — keyed by the host action key, with the resource
 * strings resolved from the `wpf.*` i18n block.
 */

export interface ActionDetailsOptions {
  actionKey: string
  title: string
  description?: string
}

interface ActionDetailsRequest {
  id: number
  options: ActionDetailsOptions
}

let requestSeq = 0
let pendingResolve: (() => void) | null = null

interface ActionDetailsState {
  request: ActionDetailsRequest | null
  show: (options: ActionDetailsOptions) => void
  settle: () => void
}

const useActionDetailsStore = create<ActionDetailsState>((set) => ({
  request: null,
  show: (options) => set({ request: { id: ++requestSeq, options } }),
  settle: () => {
    pendingResolve?.()
    pendingResolve = null
    set({ request: null })
  }
}))

export function openActionDetails(options: ActionDetailsOptions): Promise<void> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useActionDetailsStore.getState().show(options)
  })
}

function getCleanupCommands(actionKey: string, t: (key: string) => string): string[] {
  switch (actionKey) {
    case 'cleanup.browserCache':
      return [
        'del /f /s /q "%LocalAppData%\\Microsoft\\Windows\\INetCache\\*" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\Microsoft\\Windows\\INetCookies\\*" >nul 2>&1'
      ]
    case 'cleanup.thumbnailCache':
      return [
        'del /f /s /q "%LocalAppData%\\Microsoft\\Windows\\Explorer\\thumbcache_*.db" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\Local\\D3DSCache\\*" >nul 2>&1'
      ]
    case 'cleanup.windowsUpdate':
      return [
        'del /f /s /q "%SystemRoot%\\SoftwareDistribution\\Download\\*" >nul 2>&1',
        'del /f /s /q "%SystemRoot%\\SoftwareDistribution\\DeliveryOptimization\\*" >nul 2>&1'
      ]
    case 'cleanup.tempFiles':
      return [
        'del /f /s /q "%SystemRoot%\\Temp\\*" >nul 2>&1',
        'del /f /s /q "%SystemDrive%\\Windows\\Temp\\*" >nul 2>&1',
        'del /f /s /q "%TEMP%\\*" >nul 2>&1'
      ]
    case 'cleanup.logs':
      return [
        'del /f /s /q "%SystemRoot%\\Logs\\*" >nul 2>&1',
        'del /f /s /q "%ProgramData%\\Microsoft\\Windows\\WER\\ReportQueue\\*" >nul 2>&1',
        'del /f /s /q "%ProgramData%\\Microsoft\\Diagnosis\\*" >nul 2>&1'
      ]
    case 'cleanup.crashDumps':
      return [
        'del /f /s /q "%SystemRoot%\\Minidump\\*.dmp" >nul 2>&1',
        'del /f /q "%SystemRoot%\\memory.dmp" >nul 2>&1',
        'del /f /s /q "%SystemDrive%\\*.dmp" >nul 2>&1'
      ]
    case 'cleanup.recycleBin':
      return ['rd /s /q "%SystemDrive%\\$Recycle.bin" >nul 2>&1']
    case 'cleanup.defender':
      return ['del /f /s /q "%ProgramData%\\Microsoft\\Windows Defender\\Scans\\*" >nul 2>&1']
    case 'cleanup.prefetch':
      return ['del /f /s /q "%SystemRoot%\\Prefetch\\*" >nul 2>&1']
    case 'cleanup.remoteDesktopCache':
      return ['del /f /s /q "%LocalAppData%\\Microsoft\\Terminal Server Client\\Cache\\*" >nul 2>&1']
    case 'cleanup.dotnetNative':
      return [
        'rd /s /q "%WinDir%\\assembly\\NativeImages_v4.0.30319_32" >nul 2>&1',
        'rd /s /q "%WinDir%\\assembly\\NativeImages_v4.0.30319_64" >nul 2>&1'
      ]
    case 'cleanup.shaderCache':
      return [
        'del /f /s /q "%LocalAppData%\\D3DSCache\\*" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\NVIDIA\\DXCache\\*" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\NVIDIA\\GLCache\\*" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\AMD\\DxCache\\*" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\Intel\\ShaderCache\\*" >nul 2>&1'
      ]
    case 'cleanup.instantMessaging':
      return [
        'del /f /s /q "%AppData%\\Tencent\\QQNT\\nt_qq\\nt_temp\\*" >nul 2>&1',
        'del /f /s /q "%AppData%\\Telegram Desktop\\tdata\\temp\\*" >nul 2>&1',
        'del /f /s /q "%AppData%\\DingTalk\\temp\\*" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\Tencent\\WeChat\\Caches\\*" >nul 2>&1'
      ]
    case 'cleanup.devCaches':
      return [
        'del /f /s /q "%LocalAppData%\\pip\\cache\\*" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\npm-cache\\*" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\Yarn\\Cache\\*" >nul 2>&1',
        'del /f /s /q "%LocalAppData%\\NuGet\\v3-cache\\*" >nul 2>&1'
      ]
    case 'network.optimization':
      return [
        t('wpf.actionDetailsWindownetworkFlushDNS'),
        t('wpf.actionDetailsWindownetworkResetWinsock'),
        t('wpf.actionDetailsWindownetworkResetTCPIP')
      ]
    default:
      return []
  }
}

function getRegistryTweaks(actionKey: string): string[] {
  switch (actionKey) {
    case 'explorer.taskbar':
      return [
        'HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced',
        '  - TaskbarDa: 0 (Disable taskbar animations)',
        '  - TaskbarAnimations: 0 (Disable taskbar animation effects)'
      ]
    case 'explorer.responsiveness':
      return [
        'HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced',
        '  - DesktopProcess: 1 (Optimize desktop process)',
        '  - DisablePreviewDesktop: 1 (Disable desktop preview)'
      ]
    case 'explorer.visibility':
      return [
        'HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced',
        "  - Hidden: 1 (Show hidden files)",
        "  - ShowSuperHidden: 0 (Don't show system protected files)"
      ]
    case 'explorer.suggestions':
      return [
        'HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced',
        '  - ShowTaskViewButton: 0 (Hide Task View button)',
        '  - ShowCortanaButton: 0 (Hide Cortana button)'
      ]
    case 'performance.multimedia':
      return [
        'HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Multimedia\\SystemProfile',
        '  - SystemResponsiveness: 0 (Optimize multimedia responsiveness)',
        '  - NetworkThrottlingIndex: 4294967295 (Disable network throttling)'
      ]
    case 'performance.memory':
      return [
        'HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management',
        '  - DisablePagingExecutive: 1 (Disable paging executive)',
        '  - LargeSystemCache: 0 (Optimize system cache)'
      ]
    case 'performance.telemetry':
      return [
        'HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection',
        '  - AllowTelemetry: 0 (Disable telemetry)'
      ]
    case 'performance.notifications':
      return [
        'HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings',
        '  - Disable various notification-related registry entries'
      ]
    case 'network.acceleration':
      return [
        'HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters',
        '  - TcpAckFrequency: 1 (Optimize TCP acknowledgment frequency)',
        '  - TCPNoDelay: 1 (Disable Nagle algorithm)',
        '  - Tcp1323Opts: 3 (Enable TCP timestamps and window scaling)',
        '  - DefaultTTL: 64 (Set default TTL)',
        '  - EnablePMTUDiscovery: 1 (Enable Path MTU Discovery)',
        '  - GlobalMaxTcpWindowSize: 65535 (Increase TCP window size)',
        '  - SackOpts: 1 (Enable selective acknowledgment)',
        'HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters',
        '  - MaxCacheTtl: 3600 (DNS cache max TTL)',
        '  - MaxNegativeCacheTtl: 300 (DNS negative cache TTL)'
      ]
    default:
      return []
  }
}

function getServiceDetails(actionKey: string): string[] {
  switch (actionKey) {
    case 'services.diagnostics':
      return [
        'Service name: DiagTrack',
        'Service name: diagnosticshub.standardcollector.service',
        'Service name: DoSvc',
        'Action: Disable and stop service'
      ]
    case 'services.sysmain':
      return ['Service name: SysMain (Superfetch)', 'Action: Disable and stop service']
    case 'services.search':
      return ['Service name: WSearch (Windows Search)', 'Action: Disable and stop service']
    default:
      return []
  }
}

function getPowerPlanCommands(): string[] {
  return ['powercfg -setactive SCHEME_MAX', 'powercfg -h off']
}

function getExplorerSpecialActions(actionKey: string): string[] {
  if (actionKey === 'explorer.startMenu') {
    return [
      'Registry modification:',
      '  HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced',
      '    - Start_NotifyNewApps: 0',
      'PowerShell script:',
      '  Disable Start Menu apps using Get-StartApps and Remove-AppxPackage'
    ]
  }
  if (actionKey === 'explorer.winKeySearch') {
    return [
      'Registry modification:',
      '  HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced',
      '    - Start_SearchFiles: 1 (Set Windows key to open search)',
      '',
      'System notification:',
      '  Send WM_SETTINGCHANGE message to notify system settings changes',
      '',
      'Explorer restart:',
      '  Restart Windows Explorer to apply changes immediately'
    ]
  }
  return []
}

function getComponentStoreCommands(): string[] {
  return [
    'dism /Online /Cleanup-Image /StartComponentCleanup /ResetBase',
    'del /f /s /q "%SystemRoot%\\WinSxS\\Temp\\*" >nul 2>&1'
  ]
}

function getImplementationDetails(
  actionKey: string,
  t: (key: string) => string
): { implementationType: string; details: string[] } {
  const unknown = t('wpf.actionDetailsWindowunknownImplementation')
  try {
    if (actionKey.toLowerCase().startsWith('cleanup.')) {
      if (actionKey === 'cleanup.registry') {
        return {
          implementationType: t('wpf.actionDetailsWindowregistryCleanup'),
          details: [t('wpf.actionDetailsWindowcleanupRegistry')]
        }
      }
      if (actionKey === 'cleanup.componentStore') {
        return {
          implementationType: t('wpf.actionDetailsWindowdISMCommand'),
          details: getComponentStoreCommands()
        }
      }
      return {
        implementationType: t('wpf.actionDetailsWindowcommandExecution'),
        details: getCleanupCommands(actionKey, t)
      }
    }
    if (actionKey.toLowerCase().startsWith('explorer.') || actionKey.toLowerCase().startsWith('performance.')) {
      if (actionKey === 'explorer.startMenu' || actionKey === 'explorer.winKeySearch') {
        return {
          implementationType: t('wpf.actionDetailsWindowregistryAndScript'),
          details: getExplorerSpecialActions(actionKey)
        }
      }
      return {
        implementationType: t('wpf.actionDetailsWindowregistryModification'),
        details: getRegistryTweaks(actionKey)
      }
    }
    if (actionKey.toLowerCase().startsWith('services.')) {
      return {
        implementationType: t('wpf.actionDetailsWindowserviceManagement'),
        details: getServiceDetails(actionKey)
      }
    }
    if (actionKey === 'performance.powerPlan') {
      return {
        implementationType: t('wpf.actionDetailsWindowcommandExecution'),
        details: getPowerPlanCommands()
      }
    }
  } catch {
    // Fall through to the unknown implementation default.
  }
  return { implementationType: unknown, details: [] }
}

export default function ActionDetailsModalHost(): React.JSX.Element {
  const { t } = useTranslation()
  const request = useActionDetailsStore((s) => s.request)
  const settle = useActionDetailsStore((s) => s.settle)
  const { dialogRef, titleId, dialogProps } = useUtilsDialog(request != null, settle)

  const content = useMemo(() => {
    if (!request) return null
    const { actionKey, title, description } = request.options
    const details = getImplementationDetails(actionKey, t)
    return {
      title,
      description: description?.trim().length ? description : t('wpf.actionDetailsWindownotFound'),
      implementationType: details.implementationType,
      details: details.details
    }
  }, [request, t])

  if (!request || !content) return <></>

  return (
    <div className="udt-utils-backdrop" onClick={settle}>
      <div
        ref={dialogRef}
        className="udt-utils-modal"
        style={{ width: 760, height: 560 }}
        onClick={(event) => event.stopPropagation()}
        {...dialogProps}
      >
        <div className="udt-utils-modal__title" id={titleId}>{t('wpf.actionDetailsWindowtitle')}</div>
        <div className="udt-utils-modal__body">
          <div style={{ fontSize: 16, fontWeight: 600, marginBottom: 6 }}>{content.title}</div>
          <p className="udt-utils-text" style={{ marginTop: 0, marginBottom: 16 }}>
            {content.description}
          </p>
          <div className="udt-utils-card">
            <div className="udt-utils-row" style={{ cursor: 'default' }}>
              <span className="udt-utils-row__label">
                {t('wpf.actionDetailsWindowimplementationType')}
              </span>
              <span className="udt-utils-row__value">{content.implementationType}</span>
            </div>
            <div className="udt-utils-details">
              {content.details.length === 0 ? (
                <div className="udt-utils-text">{t('wpf.actionDetailsWindownoDetailsAvailable')}</div>
              ) : (
                content.details.map((line, index) => (
                  <div key={index} className="udt-utils-mono" style={{ marginBottom: 8 }}>
                    {line}
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
        <div className="udt-utils-modal__actions">
          <button type="button" className="udt-utils-button udt-utils-button--primary" onClick={settle}>
            {t('wpf.actionDetailsWindowclosebutton')}
          </button>
        </div>
      </div>
    </div>
  )
}
