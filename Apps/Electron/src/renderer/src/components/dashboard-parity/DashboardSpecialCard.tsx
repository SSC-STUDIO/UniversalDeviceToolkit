import { memo, useCallback, useMemo, useState } from 'react'
import { Button, Dropdown, Popover, Switch, Tooltip, message } from 'antd'
import {
  ChevronDown16Regular,
  Copy20Regular,
  Desktop24Regular,
  DeveloperBoard24Regular,
  DeveloperBoardLightning20Regular,
  Settings24Regular,
  Sleep24Regular,
  TaskListSquareLtr20Regular
} from '@fluentui/react-icons'
import { useTranslation } from 'react-i18next'
import { copyLines } from '../../utils/clipboard'
import {
  dashboardHardwareApi,
  type DashboardHardwareState,
  type DiscreteGpuState
} from '../../api/dashboardHardware'
import type { DashboardItem } from '../../api/dashboard'
import { isSpecialItemSupported, requireHardwareOk } from './dashboardHardwareSupport'
import OverclockProfilesModal from './OverclockProfilesModal'

export type SpecialDashboardItem = Extract<
  DashboardItem,
  'DiscreteGpu' | 'OverclockDiscreteGpu' | 'TurnOffMonitors'
>

interface DashboardSpecialCardProps {
  item: SpecialDashboardItem
  hardware: DashboardHardwareState
  error: string | null
  onChanged: () => Promise<void>
}

function statusTone(state: DiscreteGpuState): string {
  if (state === 'Active' || state === 'MonitorConnected') return 'success'
  if (state === 'Inactive') return 'warning'
  return 'neutral'
}

function CardShell({
  icon,
  title,
  description,
  children,
  error
}: {
  icon: React.ReactNode
  title: string
  description: string
  children: React.ReactNode
  error: string | null
}): React.JSX.Element {
  return (
    <article className="udt-parity-feature-card">
      <div className="udt-parity-feature-card__body">
        <span className="udt-parity-feature-card__icon" aria-hidden="true">{icon}</span>
        <div className="udt-parity-feature-card__copy">
          <div className="udt-parity-feature-card__title" title={title}>{title}</div>
          <div className="udt-parity-feature-card__description" title={description}>{description}</div>
          {error != null && <div className="udt-parity-feature-card__warning" title={error}>{error}</div>}
        </div>
        {children}
      </div>
    </article>
  )
}

function DiscreteGpuCard({
  hardware,
  error,
  onChanged
}: Omit<DashboardSpecialCardProps, 'item'>): React.JSX.Element {
  const { t } = useTranslation()
  const [busy, setBusy] = useState(false)
  const [copying, setCopying] = useState(false)
  const gpu = hardware.discreteGpu
  const title = t('dashboardHardware.discreteGpu.title')
  const description = t('dashboardHardware.discreteGpu.description')
  const status = t(`dashboardHardware.discreteGpu.status.${gpu.state}`, { defaultValue: gpu.state })
  const canRestart = gpu.state === 'Active' || gpu.state === 'Inactive'
  const canKill = gpu.state === 'Active'
  const actionFailed = t('dashboardHardware.actionFailed', {
    defaultValue: 'The action did not complete.'
  })

  const run = useCallback(async (action: () => Promise<{ ok: boolean }>): Promise<void> => {
    setBusy(true)
    try {
      const result = await action()
      requireHardwareOk(result, actionFailed)
      await onChanged()
    } catch (reason) {
      void message.error((reason as Error).message)
    } finally {
      setBusy(false)
    }
  }, [actionFailed, onChanged])

  // Port of Electron ClipboardExtensions.SetProcesses: copy the GPU process list.
  async function copyProcessList(): Promise<void> {
    setCopying(true)
    try {
      const copied = await copyLines(gpu.processes)
      if (copied) {
        void message.success(t('clipboard.copySuccess', { defaultValue: 'Process list copied to clipboard' }))
      } else {
        void message.error(t('clipboard.copyFailed', { defaultValue: 'Failed to copy process list' }))
      }
    } catch {
      void message.error(t('clipboard.copyFailed', { defaultValue: 'Failed to copy process list' }))
    } finally {
      setCopying(false)
    }
  }

  const processKey = gpu.processes.join('\n')
  const details = useMemo(
    () => {
      const processes = processKey.length === 0 ? [] : processKey.split('\n')
      return (
        <div className="udt-parity-gpu-tooltip">
          <strong>{t('dashboardHardware.discreteGpu.performance')}</strong>
          <div>{gpu.performanceState || '-'}</div>
          <strong>{t('dashboardHardware.discreteGpu.processes')}</strong>
          {processes.length > 0
            ? processes.map((process) => <div key={process}>{process}</div>)
            : <div>{t('dashboardHardware.discreteGpu.noProcesses')}</div>}
        </div>
      )
    },
    [gpu.performanceState, processKey, t]
  )

  const menu = useMemo(
    () => ({
      items: [
        {
          key: 'kill',
          label: t('dashboardHardware.discreteGpu.killProcesses'),
          disabled: !canKill
        },
        {
          key: 'restart',
          label: t('dashboardHardware.discreteGpu.restart'),
          disabled: !canRestart
        }
      ],
      onClick: ({ key }: { key: string }) => {
        if (key === 'kill') void run(() => dashboardHardwareApi.killGpuProcesses())
        if (key === 'restart') void run(() => dashboardHardwareApi.restartGpu())
      }
    }),
    [canKill, canRestart, run, t]
  )

  return (
    <CardShell
      icon={<DeveloperBoard24Regular />}
      title={title}
      description={description}
      error={error}
    >
      <div className="udt-parity-gpu-accessory">
        <div className="udt-parity-gpu-accessory__status-row">
          <span className={`udt-parity-status-dot udt-parity-status-dot--${statusTone(gpu.state)}`} />
          <span className="udt-parity-gpu-accessory__status">{status}</span>
          <Tooltip title={t('clipboard.copyProcesses', { defaultValue: 'Copy process list' })}>
            <Button
              aria-label={t('clipboard.copyProcesses', { defaultValue: 'Copy process list' })}
              className="udt-parity-icon-button"
              icon={<Copy20Regular />}
              disabled={gpu.processes.length === 0}
              loading={copying}
              onClick={() => void copyProcessList()}
            />
          </Tooltip>
          <Popover
            content={details}
            placement="topRight"
            overlayClassName="udt-parity-gpu-popover"
          >
            <Tooltip title={t('dashboardHardware.discreteGpu.processesTooltip', { defaultValue: '查看 GPU 状态及占用进程' })}>
              <Button
                aria-label={t('dashboardHardware.discreteGpu.information', { defaultValue: '查看占用 GPU 的进程列表与状态' })}
                className="udt-parity-icon-button"
                icon={<TaskListSquareLtr20Regular />}
              />
            </Tooltip>
          </Popover>
        </div>
        <Dropdown
          disabled={!canRestart || busy}
          menu={menu}
          placement="bottomRight"
          trigger={['click']}
        >
          <Button
            type="primary"
            disabled={!canRestart}
            loading={busy}
            icon={<Sleep24Regular />}
          >
            {t('dashboardHardware.discreteGpu.deactivate')}
            <ChevronDown16Regular />
          </Button>
        </Dropdown>
      </div>
    </CardShell>
  )
}

function OverclockGpuCard({
  hardware,
  error,
  onChanged
}: Omit<DashboardSpecialCardProps, 'item'>): React.JSX.Element {
  const { t } = useTranslation()
  const overclock = hardware.overclockDiscreteGpu
  const [busy, setBusy] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)

  async function setEnabled(enabled: boolean): Promise<void> {
    setBusy(true)
    try {
      const result = await dashboardHardwareApi.setOverclockEnabled(enabled)
      requireHardwareOk(
        result,
        t('dashboardHardware.actionFailed', { defaultValue: 'The action did not complete.' })
      )
      await onChanged()
    } catch (reason) {
      void message.error((reason as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const title = t('dashboardHardware.overclock.title')
  return (
    <>
      <CardShell
        icon={<DeveloperBoardLightning20Regular />}
        title={title}
        description={t('dashboardHardware.overclock.description')}
        error={error}
      >
        <div className="udt-parity-feature-card__accessory udt-parity-overclock-accessory">
          <Switch
            aria-label={title}
            checked={overclock.enabled}
            disabled={busy}
            loading={busy}
            onChange={(enabled) => void setEnabled(enabled)}
          />
          <Tooltip title={t('dashboardHardware.overclock.settings')}>
            <Button
              aria-label={t('dashboardHardware.overclock.settings')}
              className="udt-parity-icon-button"
              icon={<Settings24Regular />}
              onClick={() => setModalOpen(true)}
            />
          </Tooltip>
        </div>
      </CardShell>
      <OverclockProfilesModal
        open={modalOpen}
        hardware={overclock}
        onClose={() => setModalOpen(false)}
        onApplied={() => void onChanged()}
      />
    </>
  )
}

function TurnOffMonitorsCard({ error }: Omit<DashboardSpecialCardProps, 'item'>): React.JSX.Element {
  const { t } = useTranslation()
  const [busy, setBusy] = useState(false)
  const title = t('dashboardHardware.turnOffMonitors.title')

  async function turnOff(): Promise<void> {
    setBusy(true)
    try {
      const result = await dashboardHardwareApi.turnOffMonitors()
      requireHardwareOk(
        result,
        t('dashboardHardware.actionFailed', { defaultValue: 'The action did not complete.' })
      )
    } catch (reason) {
      void message.error((reason as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <CardShell
      icon={<Desktop24Regular />}
      title={title}
      description={t('dashboardHardware.turnOffMonitors.description')}
      error={error}
    >
      <div className="udt-parity-feature-card__accessory">
        <Button loading={busy} onClick={() => void turnOff()}>
          {t('dashboardHardware.turnOffMonitors.action')}
        </Button>
      </div>
    </CardShell>
  )
}

function DashboardSpecialCard(props: DashboardSpecialCardProps): React.JSX.Element | null {
  if (!isSpecialItemSupported(props.item, props.hardware)) return null
  if (props.item === 'DiscreteGpu') return <DiscreteGpuCard {...props} />
  if (props.item === 'OverclockDiscreteGpu') return <OverclockGpuCard {...props} />
  if (props.item === 'TurnOffMonitors') return <TurnOffMonitorsCard {...props} />
  return null
}

export default memo(DashboardSpecialCard)
