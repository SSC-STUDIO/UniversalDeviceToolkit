import { useEffect, useMemo, useState } from 'react'
import {
  ArrowDown24Regular,
  ArrowExport24Regular,
  ArrowImport24Regular,
  ArrowUp24Regular,
  Clock24Regular,
  Dismiss24Regular,
  ArrowEnterLeft24Regular,
  ArrowRepeatAll24Regular,
  ArrowRotateClockwise24Regular,
  ReOrder24Regular,
  Settings24Regular
} from '../components/icons/fluent'
import { Button, Select, Switch, Tooltip, message } from 'antd'
import { useTranslation } from 'react-i18next'
import type { MacroEvent, MacroRecordingMode, MacroSlot } from '../api/macro'
import { useMacroStore } from '../stores/macroStore'
import { useMacroRecorder } from '../hooks/useMacroRecorder'
import MacroRecordingModal from '../components/macro/MacroRecordingModal'
import CapabilityUnavailable from '../components/utils/CapabilityUnavailable'
import { useHostCapabilitiesStore } from '../stores/hostCapabilitiesStore'
import {
  appendCapturedEvents,
  createMacroEditorDraft,
  hasMacroEvents,
  macroVirtualKeyName,
  NUMPAD_LAYOUT
} from '../components/macro/macroHelpers'
import { SkeletonBone } from '../components/Skeleton'
import '../components/macro/macro.css'

interface MacroTexts {
  subtitle: string
  enableSubtitle: string
  ignoreDelays: string
  interruptOnOtherKey: string
  recordingOptions: string
  dontRepeat: string
  keyboardOnly: string
  keyboardMouse: string
  allInputs: string
  record: string
  recording: string
  recordingInterrupted: string
  keyboard: string
  mouse: string
  move: string
  wheelUp: string
  wheelDown: string
  wheelLeft: string
  wheelRight: string
  leftButton: string
  rightButton: string
  middleButton: string
  xButton: string
  button: string
}

const REPEAT_OPTIONS = Array.from({ length: 10 }, (_, i) => ({ value: i + 1, label: `${i + 1}` }))

/**
 * Loading skeleton mirroring the live layout: enable card (title/subtitle +
 * switch), then the workspace split of numpad key grid and sequence-side
 * cards. Reuses the live layout classes so the container breakpoints apply.
 */
function MacroSkeleton(): React.JSX.Element {
  return (
    <>
      <div className="udt-macro-card udt-macro-card--enable">
        <div className="udt-macro-card__body udt-macro-card__body--row">
          <div className="udt-macro-card__copy">
            <SkeletonBone delay={0} width={96} height={16} radius="small" />
            <SkeletonBone delay={1} width={240} height={12} radius="small" style={{ marginTop: 6 }} />
          </div>
          <SkeletonBone delay={2} className="udt-skeleton-switch" radius="round" />
        </div>
      </div>

      <div className="udt-macro-main">
        <aside className="udt-macro-numpad-panel">
          <div className="udt-macro-numpad">
            {NUMPAD_LAYOUT.flat().map((code, index) =>
              code === null ? (
                <span key={`sk-pad-${index}`} />
              ) : (
                <SkeletonBone
                  key={`sk-key-${index}`}
                  delay={3 + index}
                  className="udt-macro-skeleton__key"
                  radius="small"
                />
              )
            )}
          </div>
        </aside>

        <div className="udt-macro-sequence">
          <div className="udt-macro-card udt-macro-card--gap">
            <div className="udt-macro-card__header">
              <SkeletonBone delay={4} width={24} height={24} radius="small" />
              <div className="udt-macro-card__copy">
                <SkeletonBone delay={5} width={72} height={15} radius="small" />
              </div>
            </div>
            <div className="udt-macro-card__body">
              <SkeletonBone delay={6} className="udt-skeleton-select" style={{ marginLeft: 0 }} radius="control" />
            </div>
          </div>

          <div className="udt-macro-row">
            {Array.from({ length: 2 }, (_, card) => (
              <div key={card} className="udt-macro-card udt-macro-card--gap-lg">
                <div className="udt-macro-card__header">
                  <SkeletonBone delay={7 + card * 3} width={24} height={24} radius="small" />
                </div>
                <div className="udt-macro-card__body udt-macro-card__body--row">
                  <SkeletonBone delay={8 + card * 3} width={128} height={15} radius="small" />
                  <SkeletonBone delay={9 + card * 3} className="udt-skeleton-switch" radius="round" />
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </>
  )
}

const RECORDING_OPTIONS: {
  value: MacroRecordingMode
  labelKey: 'keyboardOnly' | 'keyboardMouse' | 'allInputs'
}[] = [
  { value: 'Keyboard', labelKey: 'keyboardOnly' },
  { value: 'KeyboardMouse', labelKey: 'keyboardMouse' },
  { value: 'KeyboardMouseMovement', labelKey: 'allInputs' }
]

function formatDelay(delayMs: number): string {
  if (delayMs < 1000) return `${Math.round(delayMs)} ms`
  return `${(delayMs / 1000).toFixed(1)} s`
}

function eventTitle(ev: MacroEvent, tx: MacroTexts): string {
  if (ev.source === 'Keyboard') return macroVirtualKeyName(ev.key)
  if (ev.direction === 'Move') return tx.move
  if (ev.direction === 'Wheel') return ev.key < 0 ? tx.wheelDown : tx.wheelUp
  if (ev.direction === 'HorizontalWheel') return ev.key < 0 ? tx.wheelLeft : tx.wheelRight
  if (ev.key >= 0xff) return `${tx.xButton} ${ev.key >> 16}`
  if (ev.key === 1) return tx.leftButton
  if (ev.key === 2) return tx.rightButton
  if (ev.key === 3) return tx.middleButton
  return `${tx.button} ${ev.key}`
}

function eventSubtitle(ev: MacroEvent, tx: MacroTexts): string {
  const source = ev.source === 'Keyboard' ? tx.keyboard : tx.mouse
  return `${source} → ${formatDelay(ev.delayMs)}`
}

function eventIcon(ev: MacroEvent): React.JSX.Element | null {
  switch (ev.direction) {
    case 'Up':
      return <ArrowUp24Regular />
    case 'Down':
      return <ArrowDown24Regular />
    case 'Wheel':
    case 'HorizontalWheel':
      return <ArrowRotateClockwise24Regular />
    case 'Move':
      return <ReOrder24Regular />
    default:
      return null
  }
}

export default function MacroPage(): React.JSX.Element {
  const { t } = useTranslation()
  const macroAvailable = useHostCapabilitiesStore((s) => s.capabilities?.capabilities.macro)
  if (macroAvailable === false) {
    return <CapabilityUnavailable title={t('nav.macro')} />
  }

  return <MacroPageContent />
}

function MacroPageContent(): React.JSX.Element {
  const { t } = useTranslation()
  const { state, loaded, loading: macroLoading, load, setEnabled, play, saveSequence, clearSequence } = useMacroStore()
  const tx = useMemo<MacroTexts>(
    () => ({
      subtitle: t('macro.subtitle'),
      enableSubtitle: t('macro.enableDesc'),
      ignoreDelays: t('macro.ignoreDelays'),
      interruptOnOtherKey: t('macro.interruptOnOtherKey'),
      recordingOptions: t('macro.recordingOptions'),
      dontRepeat: t('macro.dontRepeat'),
      keyboardOnly: t('macro.keyboardOnly'),
      keyboardMouse: t('macro.keyboardMouse'),
      allInputs: t('macro.allInputs'),
      record: t('macro.record'),
      recording: t('macro.recording.title'),
      recordingInterrupted: t('macro.recordingInterrupted'),
      keyboard: t('macro.keyboard'),
      mouse: t('macro.mouse'),
      move: t('macro.move'),
      wheelUp: t('macro.wheelUp'),
      wheelDown: t('macro.wheelDown'),
      wheelLeft: t('macro.wheelLeft'),
      wheelRight: t('macro.wheelRight'),
      leftButton: t('macro.leftButton'),
      rightButton: t('macro.rightButton'),
      middleButton: t('macro.middleButton'),
      xButton: t('macro.xButton'),
      button: t('macro.button')
    }),
    [t]
  )

  const [selectedKey, setSelectedKey] = useState<number>(0x60)
  const [repeatCount, setRepeatCount] = useState(1)
  const [ignoreDelays, setIgnoreDelays] = useState(false)
  const [interruptOnOtherKey, setInterruptOnOtherKey] = useState(false)
  const [recordingMode, setRecordingMode] = useState<MacroRecordingMode>('Keyboard')
  const [events, setEvents] = useState<MacroEvent[]>([])

  const recorder = useMacroRecorder((captured, interrupted) => {
    if (interrupted) {
      message.info(tx.recordingInterrupted)
      return
    }
    setEvents((current) => appendCapturedEvents(current, captured, interrupted))
  })
  const recording = recorder.state !== 'idle'

  const slot = (state?.slots ?? []).find((s) => s.key === selectedKey)
  const list = events
  const hasEvents = hasMacroEvents(list)

  // Sync the full editable sequence whenever the selected or reloaded slot changes.
  const [syncedSlot, setSyncedSlot] = useState<MacroSlot | undefined>(undefined)
  if (syncedSlot !== slot) {
    const draft = createMacroEditorDraft(selectedKey, state.slots)
    setSyncedSlot(slot)
    setRepeatCount(draft.repeatCount)
    setIgnoreDelays(draft.ignoreDelays)
    setInterruptOnOtherKey(draft.interruptOnOtherKey)
    setEvents(draft.events)
  }

  useEffect(() => {
    void load()
  }, [load])

  const selectKey = (code: number): void => {
    if (recorder.state !== 'idle') recorder.stop()
    const draft = createMacroEditorDraft(code, state.slots)
    const nextSlot = state.slots.find((candidate) => candidate.key === code)
    setSelectedKey(draft.key)
    setSyncedSlot(nextSlot)
    setRepeatCount(draft.repeatCount)
    setIgnoreDelays(draft.ignoreDelays)
    setInterruptOnOtherKey(draft.interruptOnOtherKey)
    setEvents(draft.events)
  }

  const handleSave = async (): Promise<void> => {
    try {
      const saved = await saveSequence({
        key: selectedKey,
        repeatCount,
        ignoreDelays,
        interruptOnOtherKey,
        events
      })
      if (saved !== true) {
        message.error(useMacroStore.getState().error ?? t('settings.saveFailed'))
        return
      }
      message.success(t('settings.saved'))
    } catch (error) {
      message.error((error as Error).message)
    }
  }

  const handleClear = async (): Promise<void> => {
    try {
      const cleared = await clearSequence(selectedKey)
      if (cleared !== true) {
        message.error(useMacroStore.getState().error ?? t('settings.saveFailed'))
        return
      }
      setEvents([])
    } catch (error) {
      message.error((error as Error).message)
    }
  }

  const handleEnabledChange = async (enabled: boolean): Promise<void> => {
    if ((await setEnabled(enabled)) === true) return
    message.error(useMacroStore.getState().error ?? t('settings.saveFailed'))
  }

  const handlePlay = async (): Promise<void> => {
    if ((await play(selectedKey)) === true) return
    message.error(useMacroStore.getState().error ?? t('settings.saveFailed'))
  }

  const handleRecord = (): void => {
    if (recorder.state !== 'idle') {
      recorder.stop()
      return
    }
    recorder.start(recordingMode)
  }

  const handleExportMacro = (): void => {
    if (!events || events.length === 0) {
      message.warning(t('macro.empty', { defaultValue: 'No macro sequence recorded.' }))
      return
    }
    const data = {
      key: selectedKey,
      repeatCount,
      ignoreDelays,
      interruptOnOtherKey,
      events
    }
    void navigator.clipboard.writeText(JSON.stringify(data, null, 2)).then(() => {
      message.success(t('macro.exportSuccess', { defaultValue: 'Macro sequence copied to clipboard!' }))
    })
  }

  const handleImportMacro = (): void => {
    void navigator.clipboard.readText().then((text) => {
      if (!text || !text.trim()) {
        message.error(t('macro.importEmpty', { defaultValue: 'Clipboard is empty.' }))
        return
      }
      try {
        const parsed = JSON.parse(text.trim())
        if (Array.isArray(parsed.events)) {
          setEvents(parsed.events)
          if (typeof parsed.repeatCount === 'number') setRepeatCount(parsed.repeatCount)
          if (typeof parsed.ignoreDelays === 'boolean') setIgnoreDelays(parsed.ignoreDelays)
          if (typeof parsed.interruptOnOtherKey === 'boolean') setInterruptOnOtherKey(parsed.interruptOnOtherKey)
          message.success(t('macro.importSuccess', { defaultValue: 'Macro loaded from clipboard! Click Save to persist.' }))
        } else {
          message.error(t('macro.importInvalid', { defaultValue: 'Invalid macro JSON format.' }))
        }
      } catch {
        message.error(t('macro.importInvalid', { defaultValue: 'Invalid macro JSON format.' }))
      }
    })
  }

  // First load only: the store state starts as a non-null default ({ slots: [] }),
  // so gate on the loaded flag instead of a never-null slots check.
  const showSkeleton = macroLoading && !loaded

  return (
    <div className="udt-macro-page udt-content-wide udt-content-fill">
      <h1 className="udt-macro-page__title">{t('macro.title')}</h1>
      <p className="udt-macro-page__subtitle">{tx.subtitle}</p>

      {showSkeleton ? (
        <MacroSkeleton />
      ) : (
        <>
      <div className="udt-macro-card udt-macro-card--enable">
        <div className="udt-macro-card__body udt-macro-card__body--row">
          <div className="udt-macro-card__copy">
            <div className="udt-macro-card__title">{t('macro.enable')}</div>
            <div className="udt-macro-card__subtitle">{tx.enableSubtitle}</div>
          </div>
          <span className="udt-macro-switch">
            <Switch
              checked={state?.isEnabled ?? false}
              onChange={(checked) => void handleEnabledChange(checked)}
            />
          </span>
        </div>
      </div>

      <div className="udt-macro-workspace">
      <div className="udt-macro-main">
        <aside className="udt-macro-numpad-panel">
        <div className="udt-macro-numpad">
          {NUMPAD_LAYOUT.flat().map((code, index) =>
            code === null ? (
              <span key={`placeholder-${index}`} />
            ) : (
              <button
                key={code}
                type="button"
                className={`udt-macro-numpad__key${
                  selectedKey === code ? ' udt-macro-numpad__key--selected' : ''
                }`}
                onClick={() => selectKey(code)}
              >
                {code - 0x60}
              </button>
            )
          )}
        </div>
        </aside>

        <div className="udt-macro-sequence">
          <div className="udt-macro-sequence__controls">
          <div
            className={`udt-macro-card udt-macro-card--gap${
              hasEvents ? '' : ' udt-macro-card--disabled'
            }`}
          >
            <div className="udt-macro-card__header">
              <span className="udt-macro-card__icon">
                <ArrowRepeatAll24Regular />
              </span>
              <div className="udt-macro-card__copy">
                <div className="udt-macro-card__title">{t('macro.repeat')}</div>
              </div>
            </div>
            <div className="udt-macro-card__body">
              <Select
                className="udt-macro-select"
                value={repeatCount}
                disabled={!hasEvents}
                options={REPEAT_OPTIONS.map((o) => ({
                  ...o,
                  label: o.value === 1 ? tx.dontRepeat : o.label
                }))}
                onChange={setRepeatCount}
              />
            </div>
          </div>

          <div className="udt-macro-row">
            <div
              className={`udt-macro-card udt-macro-card--gap-lg${
                hasEvents ? '' : ' udt-macro-card--disabled'
              }`}
            >
              <div className="udt-macro-card__header">
                <span className="udt-macro-card__icon">
                  <Clock24Regular />
                </span>
              </div>
              <div className="udt-macro-card__body udt-macro-card__body--row">
                <div className="udt-macro-card__title">{tx.ignoreDelays}</div>
                <span className="udt-macro-switch">
                  <Switch checked={ignoreDelays} disabled={!hasEvents} onChange={setIgnoreDelays} />
                </span>
              </div>
            </div>
            <div
              className={`udt-macro-card udt-macro-card--gap-lg${
                hasEvents ? '' : ' udt-macro-card--disabled'
              }`}
            >
              <div className="udt-macro-card__header">
                <span className="udt-macro-card__icon">
                  <ArrowEnterLeft24Regular />
                </span>
              </div>
              <div className="udt-macro-card__body udt-macro-card__body--row">
                <div className="udt-macro-card__title">{tx.interruptOnOtherKey}</div>
                <span className="udt-macro-switch">
                  <Switch
                    checked={interruptOnOtherKey}
                    disabled={!hasEvents}
                    onChange={setInterruptOnOtherKey}
                  />
                </span>
              </div>
            </div>
          </div>

          <div className="udt-macro-card udt-macro-card--gap-lg">
            <div className="udt-macro-card__header">
              <span className="udt-macro-card__icon">
                <Settings24Regular />
              </span>
              <div className="udt-macro-card__copy">
                <div className="udt-macro-card__title">{tx.recordingOptions}</div>
              </div>
            </div>
            <div className="udt-macro-card__body">
              <Select
                className="udt-macro-select"
                value={recordingMode}
                options={RECORDING_OPTIONS.map((o) => ({ value: o.value, label: tx[o.labelKey] }))}
                onChange={setRecordingMode}
              />
            </div>
          </div>

          <div className="udt-macro-actions">
            <Button onClick={() => void handleSave()}>{t('macro.save')}</Button>
            <Button onClick={() => void handlePlay()}>{t('macro.play')}</Button>
            <Tooltip title={t('macro.exportSequence', { defaultValue: 'Export Macro to Clipboard' })}>
              <Button
                icon={<ArrowExport24Regular />}
                disabled={!hasEvents}
                onClick={handleExportMacro}
              />
            </Tooltip>
            <Tooltip title={t('macro.importSequence', { defaultValue: 'Import Macro from Clipboard' })}>
              <Button
                icon={<ArrowImport24Regular />}
                onClick={handleImportMacro}
              />
            </Tooltip>
            <Tooltip title={t('macro.clear')}>
              <Button
                icon={<Dismiss24Regular />}
                onClick={() => void handleClear()}
              />
            </Tooltip>
            <Button
              type="primary"
              className="udt-macro-actions__record"
              onClick={handleRecord}
            >
              {recording ? tx.recording : tx.record}
            </Button>
          </div>
          </div>

          <div className="udt-macro-events">
            {hasEvents ? (
              list.map((ev, index) => (
                <div key={index} className="udt-macro-event">
                  <span className="udt-macro-event__icon">{eventIcon(ev)}</span>
                  <div className="udt-macro-card__copy">
                    <div className="udt-macro-event__title">{eventTitle(ev, tx)}</div>
                    <div className="udt-macro-event__subtitle">{eventSubtitle(ev, tx)}</div>
                  </div>
                </div>
              ))
            ) : (
              <div className="udt-macro-events__empty">{t('macro.empty')}</div>
            )}
          </div>
        </div>
      </div>
      </div>

      {recorder.state !== 'idle' && (
        <MacroRecordingModal preparing={recorder.state === 'preparing'} />
      )}
        </>
      )}
    </div>
  )
}
