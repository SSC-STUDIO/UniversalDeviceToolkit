import { useEffect, useRef, useState } from 'react'
import { Button, Input, Modal, Select, Slider, Spin, message } from 'antd'
import { Delete24Regular, Edit24Regular, Add24Regular } from '../icons/fluent'
import { useTranslation } from 'react-i18next'
import { dashboardHardwareApi, type DashboardHardwareState } from '../../api/dashboardHardware'
import { settingsApi } from '../../api/settings'
import { requireHardwareOk } from './dashboardHardwareSupport'

/**
 * Parity modal for Electron Windows/Dashboard/OverclockDiscreteGPUSettingsWindow:
 * profile list (add / rename / delete / switch), core & memory frequency
 * offset sliders, and Apply / Apply & Close (or Save when overclocking is off).
 */

const MHZ = 'MHz'

interface OverclockInfo {
  coreDeltaMhz: number
  memoryDeltaMhz: number
}

interface OverclockProfile {
  name: string
  info: OverclockInfo
}

interface OverclockStore {
  enabled: boolean
  info: OverclockInfo
  activeProfileId: string
  profiles: Record<string, OverclockProfile>
}

function readInfo(value: unknown): OverclockInfo {
  const record = (value ?? {}) as Record<string, unknown>
  return {
    coreDeltaMhz: typeof record.CoreDeltaMhz === 'number' ? record.CoreDeltaMhz : 0,
    memoryDeltaMhz: typeof record.MemoryDeltaMhz === 'number' ? record.MemoryDeltaMhz : 0
  }
}

function parseStore(value: unknown): OverclockStore {
  const record = (value ?? {}) as Record<string, unknown>
  const profilesRecord = (record.Profiles ?? {}) as Record<string, unknown>
  const profiles: Record<string, OverclockProfile> = {}
  for (const [id, profileValue] of Object.entries(profilesRecord)) {
    const profile = profileValue as Record<string, unknown>
    profiles[id] = {
      name: typeof profile.Name === 'string' ? profile.Name : 'Custom',
      info: readInfo(profile.Info)
    }
  }
  return {
    enabled: record.Enabled === true,
    info: readInfo(record.Info),
    activeProfileId: typeof record.ActiveProfileId === 'string' ? record.ActiveProfileId : '',
    profiles
  }
}

function serializeStore(store: OverclockStore): Record<string, unknown> {
  const profiles: Record<string, unknown> = {}
  for (const [id, profile] of Object.entries(store.profiles)) {
    profiles[id] = {
      Name: profile.name,
      Info: { CoreDeltaMhz: profile.info.coreDeltaMhz, MemoryDeltaMhz: profile.info.memoryDeltaMhz }
    }
  }
  return {
    Enabled: store.enabled,
    Info: { CoreDeltaMhz: store.info.coreDeltaMhz, MemoryDeltaMhz: store.info.memoryDeltaMhz },
    ActiveProfileId: store.activeProfileId,
    Profiles: profiles
  }
}

function formatDelta(value: number): string {
  return `${value > 0 ? '+' : value < 0 ? '-' : ''}${value} ${MHZ}`
}

interface NamePromptState {
  mode: 'add' | 'rename'
}

interface OverclockProfilesModalProps {
  open: boolean
  hardware: DashboardHardwareState['overclockDiscreteGpu']
  onClose: () => void
  onApplied?: () => void
}

export default function OverclockProfilesModal({
  open,
  hardware,
  onClose,
  onApplied
}: OverclockProfilesModalProps): React.JSX.Element {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [store, setStore] = useState<OverclockStore | null>(null)
  const [coreDeltaMhz, setCoreDeltaMhz] = useState(hardware.coreDeltaMhz)
  const [memoryDeltaMhz, setMemoryDeltaMhz] = useState(hardware.memoryDeltaMhz)
  const [namePrompt, setNamePrompt] = useState<NamePromptState | null>(null)
  const [nameInput, setNameInput] = useState('')
  const hardwareRef = useRef(hardware)
  hardwareRef.current = hardware

  useEffect(() => {
    if (!open) return
    let cancelled = false
    const snapshot = hardwareRef.current
    setLoading(true)
    setCoreDeltaMhz(snapshot.coreDeltaMhz)
    setMemoryDeltaMhz(snapshot.memoryDeltaMhz)
    settingsApi
      .get('gpuOverclock')
      .then((result) => {
        if (cancelled) return
        const loaded = parseStore(result.value)
        setStore(loaded)
        const active = loaded.profiles[loaded.activeProfileId]
        if (active != null) {
          setCoreDeltaMhz(active.info.coreDeltaMhz)
          setMemoryDeltaMhz(active.info.memoryDeltaMhz)
        }
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          setStore(null)
          void message.error((reason as Error).message)
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [open])

  const profileList = store == null
    ? []
    : Object.entries(store.profiles).sort((a, b) => a[1].name.localeCompare(b[1].name))

  const currentInfo = (): OverclockInfo => ({ coreDeltaMhz, memoryDeltaMhz })

  /** Save the working offsets into the active profile (Electron SaveProfile). */
  function saveProfile(target: OverclockStore): OverclockStore {
    const active = target.profiles[target.activeProfileId]
    if (active == null) return target
    return {
      ...target,
      profiles: {
        ...target.profiles,
        [target.activeProfileId]: { ...active, info: currentInfo() }
      }
    }
  }

  async function persist(next: OverclockStore): Promise<void> {
    await settingsApi.set('gpuOverclock', serializeStore(next))
    const saved = await settingsApi.save(['gpuOverclock'])
    if (!saved.saved.includes('gpuOverclock')) {
      throw new Error(t('overclock.saveFailed', {
        defaultValue: 'Failed to save overclock profiles.'
      }))
    }
    setStore(next)
  }

  async function handleProfileSwitch(id: string): Promise<void> {
    if (store == null || id === store.activeProfileId) return
    const saved = saveProfile(store)
    const next = { ...saved, activeProfileId: id }
    try {
      await persist(next)
      const active = next.profiles[id]
      if (active != null) {
        setCoreDeltaMhz(active.info.coreDeltaMhz)
        setMemoryDeltaMhz(active.info.memoryDeltaMhz)
      }
    } catch (reason) {
      void message.error((reason as Error).message)
    }
  }

  function openNamePrompt(mode: 'add' | 'rename'): void {
    if (store == null) return
    if (mode === 'rename') {
      setNameInput(store.profiles[store.activeProfileId]?.name ?? '')
    } else {
      setNameInput(t('overclock.newProfileName'))
    }
    setNamePrompt({ mode })
  }

  async function confirmNamePrompt(): Promise<void> {
    if (namePrompt == null || store == null) return
    const name = nameInput.trim()
    setNamePrompt(null)
    if (name.length === 0) return

    try {
      if (namePrompt.mode === 'add') {
        const id = crypto.randomUUID()
        const next: OverclockStore = {
          ...saveProfile(store),
          activeProfileId: id,
          profiles: {
            ...saveProfile(store).profiles,
            [id]: { name, info: currentInfo() }
          }
        }
        await persist(next)
      } else {
        const active = store.profiles[store.activeProfileId]
        if (active == null) return
        await persist({
          ...store,
          profiles: {
            ...store.profiles,
            [store.activeProfileId]: { ...active, name }
          }
        })
      }
    } catch (reason) {
      void message.error((reason as Error).message)
    }
  }

  async function handleDeleteProfile(): Promise<void> {
    if (store == null || Object.keys(store.profiles).length <= 1) return
    const presets: Record<string, OverclockProfile> = {}
    for (const [id, profile] of Object.entries(store.profiles)) {
      if (id !== store.activeProfileId) presets[id] = profile
    }
    const nextActiveId = Object.entries(presets)
      .sort((a, b) => a[1].name.localeCompare(b[1].name))
      .map(([id]) => id)[0]
    if (nextActiveId == null) return
    try {
      await persist({ ...store, activeProfileId: nextActiveId, profiles: presets })
      const active = presets[nextActiveId]
      setCoreDeltaMhz(active.info.coreDeltaMhz)
      setMemoryDeltaMhz(active.info.memoryDeltaMhz)
    } catch (reason) {
      void message.error((reason as Error).message)
    }
  }

  /** Electron Save(): persist enabled state, active profile and current offsets. */
  async function saveAll(): Promise<void> {
    if (store == null) return
    const saved = saveProfile(store)
    await persist({
      ...saved,
      enabled: hardware.enabled,
      info: currentInfo()
    })
  }

  async function handleApply(): Promise<boolean> {
    setBusy(true)
    try {
      await saveAll()
      const result = await dashboardHardwareApi.setOverclock(coreDeltaMhz, memoryDeltaMhz)
      requireHardwareOk(
        result,
        t('overclock.applyFailed', { defaultValue: 'Failed to apply GPU overclock.' })
      )
      onApplied?.()
      return true
    } catch (reason) {
      void message.error((reason as Error).message)
      return false
    } finally {
      setBusy(false)
    }
  }

  async function handleApplyAndClose(): Promise<void> {
    if (await handleApply()) onClose()
  }

  async function handleSave(): Promise<void> {
    setBusy(true)
    try {
      await saveAll()
      onApplied?.()
      onClose()
    } catch (reason) {
      void message.error((reason as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal
      centered
      open={open}
      title={t('overclock.title')}
      width={520}
      footer={
        hardware.enabled ? (
          [
            <Button key="apply" loading={busy} onClick={() => void handleApply()}>
              {t('common.apply')}
            </Button>,
            <Button
              key="apply-close"
              type="primary"
              loading={busy}
              onClick={() => void handleApplyAndClose()}
            >
              {t('common.applyAndClose')}
            </Button>
          ]
        ) : (
          [
            <Button key="save" type="primary" loading={busy} onClick={() => void handleSave()}>
              {t('common.save')}
            </Button>
          ]
        )
      }
      onCancel={onClose}
    >
      {loading ? (
        <div className="udt-dashboard-edit__loading">
          <Spin size="large" />
        </div>
      ) : store == null ? (
        <div className="udt-dashboard-edit__error">{t('overclock.loadError')}</div>
      ) : (
        <div>
          <div className="udt-overclock__preset-label">{t('overclock.preset')}</div>
          <div className="udt-overclock__preset-row">
            <Select
              className="udt-overclock__preset-select"
              aria-label={t('overclock.preset')}
              value={store.activeProfileId}
              options={profileList.map(([id, profile]) => ({ value: id, label: profile.name }))}
              onChange={(value) => void handleProfileSwitch(value)}
            />
            <Button
              icon={<Edit24Regular />}
              title={t('common.rename')}
              onClick={() => openNamePrompt('rename')}
            />
            <Button
              icon={<Delete24Regular />}
              title={t('common.delete')}
              disabled={Object.keys(store.profiles).length <= 1}
              onClick={() => void handleDeleteProfile()}
            />
            <Button type="primary" icon={<Add24Regular />} onClick={() => openNamePrompt('add')}>
              {t('common.add')}
            </Button>
          </div>

          <div className="udt-overclock__offset">
            <label htmlFor="overclock-core-offset">{t('overclock.coreOffset')}</label>
            <div className="udt-overclock__offset-row">
              <Slider
                id="overclock-core-offset"
                min={0}
                max={hardware.maxCoreDeltaMhz}
                step={1}
                value={coreDeltaMhz}
                onChange={setCoreDeltaMhz}
              />
              <span className="udt-overclock__offset-value">{formatDelta(coreDeltaMhz)}</span>
            </div>
          </div>

          <div className="udt-overclock__offset">
            <label htmlFor="overclock-memory-offset">{t('overclock.memoryOffset')}</label>
            <div className="udt-overclock__offset-row">
              <Slider
                id="overclock-memory-offset"
                min={0}
                max={hardware.maxMemoryDeltaMhz}
                step={1}
                value={memoryDeltaMhz}
                onChange={setMemoryDeltaMhz}
              />
              <span className="udt-overclock__offset-value">{formatDelta(memoryDeltaMhz)}</span>
            </div>
          </div>
        </div>
      )}

      <Modal
        centered
        open={namePrompt != null}
        title={namePrompt?.mode === 'rename' ? t('common.rename') : t('common.add')}
        okText={t('common.ok')}
        cancelText={t('common.cancel')}
        onOk={() => void confirmNamePrompt()}
        onCancel={() => setNamePrompt(null)}
        destroyOnHidden
      >
        <Input
          autoFocus
          value={nameInput}
          onChange={(event) => setNameInput(event.target.value)}
          onPressEnter={() => void confirmNamePrompt()}
        />
      </Modal>
    </Modal>
  )
}
