import { useCallback, useEffect, useRef, useState } from 'react'
import { Select, Slider, Spin, Switch } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  ArrowSync24Regular,
  Cursor24Regular,
  PaintBrush24Regular
} from '../icons/fluent'
import { localizeHostError } from '../../api/bridge'
import {
  CURSOR_THEME_MODES,
  type CursorThemeMode
} from '../../api/mouse'
import { notify } from '../../notifications'
import { useMouseStore } from '../../stores/mouseStore'
import { createDebounceDispatcher } from '../../utils/debounce'
import { SkeletonCard } from '../Skeleton'

const POINTER_APPLY_DEBOUNCE_MS = 600

/** Marks shown under the speed slider (Windows range 1..20, default 10). */
const SPEED_MARKS: Record<number, string> = {
  1: '1',
  5: '5',
  10: '10',
  15: '15',
  20: '20'
}

function themeModeLabelKey(mode: CursorThemeMode): string {
  switch (mode) {
    case CURSOR_THEME_MODES.light:
      return 'mouse.themeModeLight'
    case CURSOR_THEME_MODES.dark:
      return 'mouse.themeModeDark'
    case CURSOR_THEME_MODES.windowsDefault:
      return 'mouse.themeModeWindowsDefault'
    default:
      return 'mouse.themeModeAuto'
  }
}

/** Select options order (labels resolved through i18n at render time). */
const THEME_MODE_VALUES: CursorThemeMode[] = [
  CURSOR_THEME_MODES.auto,
  CURSOR_THEME_MODES.light,
  CURSOR_THEME_MODES.dark,
  CURSOR_THEME_MODES.windowsDefault
]

/**
 * Cursor & pointer panel inside System Optimization (absorbed the retired
 * CustomMouse plugin page). Host state lives in the store; this component only
 * keeps unsaved local edits ("drafts") which are cleared again once the
 * matching host write succeeds.
 */
export default function CursorPointerPanel(): React.JSX.Element {
  const { t } = useTranslation()
  const state = useMouseStore((s) => s.state)
  const loading = useMouseStore((s) => s.loading)
  const loadError = useMouseStore((s) => s.error)
  const writing = useMouseStore((s) => s.writing)
  const load = useMouseStore((s) => s.load)

  // Unsaved edits; null = follow the host-reported value.
  const [speedDraft, setSpeedDraft] = useState<number | null>(null)
  const [swapDraft, setSwapDraft] = useState<boolean | null>(null)
  const [modeDraft, setModeDraft] = useState<CursorThemeMode | null>(null)

  const pointerApplyDebounce = useRef(createDebounceDispatcher())

  // Cancel any pending debounced pointer apply when leaving the tab.
  useEffect(() => () => pointerApplyDebounce.current.cancel(), [])

  useEffect(() => {
    void load()
  }, [load])

  const reportError = useCallback(
    (messageKey: string, error: unknown): void => {
      notify({
        title: t('mouse.actionFailed'),
        message: localizeHostError(error, t) || t(messageKey),
        severity: 'Error'
      })
    },
    [t]
  )

  const commitPointer = useCallback(
    async (nextSpeed: number, nextSwap: boolean): Promise<void> => {
      let ok: boolean
      try {
        ok = await useMouseStore.getState().applyPointer(nextSpeed, nextSwap)
      } catch (reason) {
        reportError('mouse.applyFailed', reason)
        return
      }
      if (!ok) {
        notify({
          title: t('mouse.actionFailed'),
          message: t('mouse.applyFailed'),
          severity: 'Error'
        })
        return
      }
      // Clear only the drafts this apply actually consumed.
      setSpeedDraft((prev) => (prev === nextSpeed ? null : prev))
      setSwapDraft((prev) => (prev === nextSwap ? null : prev))
      notify({
        title: t('mouse.appliedTitle'),
        message: t('mouse.appliedMessage'),
        severity: 'Success'
      })
    },
    [reportError, t]
  )

  const handleSpeedChange = useCallback(
    (value: number): void => {
      setSpeedDraft(value)
      pointerApplyDebounce.current.debounce(POINTER_APPLY_DEBOUNCE_MS, () => {
        void commitPointer(value, swapDraft ?? state?.swapButtons ?? false)
      })
    },
    [commitPointer, state?.swapButtons, swapDraft]
  )

  const handleSwapButtonsChange = useCallback(
    (checked: boolean): void => {
      pointerApplyDebounce.current.cancel()
      setSwapDraft(checked)
      void commitPointer(speedDraft ?? state?.pointerSpeed ?? 10, checked)
    },
    [speedDraft, state?.pointerSpeed, commitPointer]
  )

  const handleThemeModeChange = useCallback(
    async (mode: CursorThemeMode): Promise<void> => {
      pointerApplyDebounce.current.cancel()
      setModeDraft(mode)
      let ok: boolean
      try {
        ok = await useMouseStore.getState().changeThemeMode(mode)
      } catch (reason) {
        setModeDraft(null)
        reportError(
          mode === CURSOR_THEME_MODES.windowsDefault
            ? 'mouse.restoreFailed'
            : 'mouse.themeModeFailed',
          reason
        )
        return
      }
      if (!ok) {
        setModeDraft(null)
        notify({
          title: t('mouse.actionFailed'),
          message: t(
            mode === CURSOR_THEME_MODES.windowsDefault
              ? 'mouse.restoreFailed'
              : 'mouse.themeModeFailed'
          ),
          severity: 'Error'
        })
        return
      }
      setModeDraft(null)
      notify({
        title:
          mode === CURSOR_THEME_MODES.windowsDefault
            ? t('mouse.windowsDefaultRestored')
            : t('mouse.themeModeApplied'),
        severity: 'Success'
      })
    },
    [reportError, t]
  )

  const handleApplyCursorStyleNow = useCallback(async (): Promise<void> => {
    let ok: boolean
    try {
      ok = await useMouseStore.getState().applyCursorStyleNow()
    } catch (reason) {
      reportError('mouse.cursorThemeFailed', reason)
      return
    }
    if (!ok) {
      notify({
        title: t('mouse.actionFailed'),
        message: t('mouse.cursorThemeFailed'),
        severity: 'Error'
      })
      return
    }
    notify({ title: t('mouse.cursorThemeApplied'), severity: 'Success' })
  }, [reportError, t])

  const handleSyncFromWindows = useCallback(async (): Promise<void> => {
    let ok: boolean
    try {
      ok = await useMouseStore.getState().refreshFromWindows()
    } catch (reason) {
      reportError('mouse.syncFailed', reason)
      return
    }
    if (!ok) {
      notify({
        title: t('mouse.actionFailed'),
        message: t('mouse.syncFailed'),
        severity: 'Error'
      })
      return
    }
    // Host values are authoritative after a sync.
    setSpeedDraft(null)
    setSwapDraft(null)
    setModeDraft(null)
    notify({ title: t('mouse.syncedFromWindows'), severity: 'Success' })
  }, [reportError, t])

  if (loading && state === null) {
    return (
      <div className="udt-cursor-layout">
        <SkeletonCard lines={3} withIcon />
        <SkeletonCard lines={2} />
      </div>
    )
  }

  if (loadError != null || state === null) {
    return (
      <div className="udt-cursor-layout">
        <section className="udt-card udt-cursor-layout__sync">
          <div className="udt-card__copy">
            <div className="udt-card__title">{t('mouse.loadFailed')}</div>
            <div className="udt-card__desc">{loadError ?? ''}</div>
          </div>
          <button
            type="button"
            className="udt-btn udt-btn--secondary"
            disabled={loading}
            onClick={() => void load()}
          >
            {t('mouse.retry')}
          </button>
        </section>
      </div>
    )
  }

  // Effective values: unsaved drafts win over the host snapshot.
  const speed = speedDraft ?? state.pointerSpeed
  const swapButtons = swapDraft ?? state.swapButtons
  const themeMode = modeDraft ?? state.cursorThemeMode
  const showLastApplied =
    themeMode !== CURSOR_THEME_MODES.windowsDefault &&
    (state.lastAppliedTheme === 'light' || state.lastAppliedTheme === 'dark')

  return (
    <div className="udt-cursor-layout">
      <section className="udt-card">
        <div className="udt-card__copy">
          <div className="udt-card__title">{t('mouse.pointerSection')}</div>
          <div className="udt-card__desc">{t('mouse.pointerSectionDesc')}</div>
        </div>
        <div className="udt-network-field">
          <span className="udt-network-field__label">{t('mouse.pointerSpeed')}</span>
          <Slider
            min={1}
            max={20}
            step={1}
            marks={SPEED_MARKS}
            disabled={writing}
            value={speed}
            onChange={(value) => handleSpeedChange(value)}
          />
          <span className="udt-card__desc">{t('mouse.pointerSpeedHint')}</span>
        </div>
        <div className="udt-network-field udt-network-field--switch">
          <span className="udt-network-field__label">{t('mouse.swapButtons')}</span>
          <Switch
            aria-label={t('mouse.swapButtons')}
            className="udt-settings-switch"
            checked={swapButtons}
            disabled={writing}
            onChange={handleSwapButtonsChange}
          />
        </div>
        <div className="udt-card__desc">{t('mouse.swapButtonsDesc')}</div>
      </section>

      <section className="udt-card">
        <div className="udt-card__copy">
          <div className="udt-card__title">
            <Cursor24Regular /> {t('mouse.themeSection')}
          </div>
          <div className="udt-card__desc">{t('mouse.themeSectionDesc')}</div>
        </div>
        <div className="udt-network-field">
          <span className="udt-network-field__label">{t('mouse.themeMode')}</span>
          <Select<CursorThemeMode>
            aria-label={t('mouse.themeMode')}
            className="udt-network-select"
            popupMatchSelectWidth={false}
            disabled={writing}
            value={themeMode}
            onChange={(mode) => void handleThemeModeChange(mode)}
            options={THEME_MODE_VALUES.map((value) => ({
              value,
              label: t(themeModeLabelKey(value))
            }))}
          />
        </div>
        {showLastApplied ? (
          <div className="udt-card__desc">
            {t('mouse.lastApplied')}:{' '}
            {state.lastAppliedTheme === 'dark'
              ? t('mouse.lastAppliedDark')
              : t('mouse.lastAppliedLight')}
          </div>
        ) : null}
        {themeMode !== CURSOR_THEME_MODES.windowsDefault ? (
          <button
            type="button"
            className="udt-btn udt-btn--primary"
            disabled={writing}
            onClick={() => void handleApplyCursorStyleNow()}
          >
            <PaintBrush24Regular /> {t('mouse.applyCursorThemeNow')}
          </button>
        ) : null}
      </section>

      <section className="udt-card udt-cursor-layout__sync">
        <div className="udt-card__desc">{t('mouse.syncFromWindowsDesc')}</div>
        <button
          type="button"
          className="udt-btn udt-btn--secondary"
          disabled={writing}
          onClick={() => void handleSyncFromWindows()}
        >
          {writing ? <Spin size="small" /> : <ArrowSync24Regular />}{' '}
          {t('mouse.syncFromWindows')}
        </button>
      </section>
    </div>
  )
}
