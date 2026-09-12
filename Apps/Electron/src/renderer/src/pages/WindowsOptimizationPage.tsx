import { useEffect, useMemo, useRef, useState } from 'react'
import {
  Checkmark24Regular,
  Info24Regular,
  Play24Regular,
  PlayCircle24Regular,
  Star24Filled,
  Star24Regular,
  Stop24Regular
} from '../components/icons/fluent'
import { Select, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import i18n from '../i18n'
import type {
  NetworkAccelerationConfig,
  NetworkAccelerationMode,
  OptimizationActionDefinition,
  OptimizationCategoryDefinition
} from '../api/optimization'
import { useDriverStore } from '../stores/driverStore'
import { isInstallerOptionalFeatureEnabled } from '../../../shared/installer-selection'
import { localizeHostError } from '../api/bridge'
import { useOptimizationStore } from '../stores/optimizationStore'
import { SkeletonCard, SkeletonList } from '../components/Skeleton'
import CardExpander from '../components/CardExpander'
import CleanupRulesPanel from '../components/optimization/CleanupRulesPanel'
import CursorPointerPanel from '../components/optimization/CursorPointerPanel'
import DriverDownloadPanel from '../components/optimization/DriverDownloadPanel'
import GameBoostPanel from '../components/optimization/GameBoostPanel'
import { NetworkPanels } from '../components/optimization/NetworkPanels'
import { presentCategoryActions } from '../utils/optimizationToggle'
import { subscribeUiVisibility } from '../utils/uiVisibility'
import { notify } from '../notifications'
import {
  NETWORK_ACCELERATION_MODES,
  collectRecommendedActionKeys,
  getActionSelectionPresentation,
  getNetworkSelectedTargetCount,
  isFailedCleanupEstimate,
  isOptimizationPlayDisabled,
  presentActionNotification,
  resolveActionError,
  runExclusivePoll,
  shouldShowEmptyPlaceholder,
  visibleOptimizationTabs,
  type OptimizationTabKey
} from '../utils/optimizationPresentation'
import { openActionDetails } from '../components/utils/ActionDetailsModal'
import '../components/optimization/optimization.css'

const NETWORK_RECOMMENDED_GROUP_IDS = new Set(['steam', 'github', 'public-cdn', 'twitch', 'roblox'])

function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B'
  const gb = bytes / 1024 ** 3
  if (gb >= 1) return `${gb.toFixed(2)} GB`
  const mb = bytes / 1024 ** 2
  if (mb >= 1) return `${mb.toFixed(1)} MB`
  return `${bytes.toFixed(0)} B`
}

/**
 * Electron LocalizationHelper.GetStringOrEnglish: the host sends resource keys as
 * the category/action titles (e.g. "WindowsOptimization_Category_Explorer_Title");
 * translate them when a matching i18n key exists, otherwise show as-is.
 *
 * Locale files store some keys verbatim under `wpf.*` and the rest as the
 * migrated camelCase form (`WindowsOptimization_Category_CleanupCache_Title`
 * → `wpf.windowsOptimizationcategorycleanupCachetitle`).
 */
function wpfResxToI18nKey(resourceKey: string): string {
  return resourceKey
    .split('_')
    .map((part) => (part.length === 0 ? part : part[0].toLowerCase() + part.slice(1)))
    .join('')
}

function localizeText(t: TFunction, text: string): string {
  if (!text) return text
  const candidates = [text, `wpf.${text}`, `wpf.${wpfResxToI18nKey(text)}`]
  for (const key of candidates) {
    if (i18n.exists(key)) return t(key)
  }
  return text
}

type TabKey = OptimizationTabKey

function reportStoreError(t: TFunction, fallbackKey: string, error: string | null | undefined): void {
  const fallback = t(fallbackKey)
  const localized = localizeHostError(resolveActionError(error, fallback), t)
  const notif = presentActionNotification(localized, fallback)
  notify({
    title: notif.title,
    message: notif.message ?? '',
    severity: 'Error'
  })
}

function ActionRow({
  action,
  selected,
  disabled,
  onToggle
}: {
  action: OptimizationActionDefinition
  selected: boolean
  disabled: boolean
  onToggle: (key: string) => void
}): React.JSX.Element {
  const { t } = useTranslation()
  const selection = getActionSelectionPresentation(action, selected)
  const showDetails = (): void => {
    void openActionDetails({
      actionKey: action.key,
      title: action.title,
      description: action.description
    })
  }
  return (
    <div className="udt-action-row" onClick={() => !disabled && onToggle(action.key)}>
      <label className="udt-checkbox" onClick={(event) => event.stopPropagation()}>
        <input
          type="checkbox"
          checked={selection.checked}
          disabled={disabled}
          ref={(el) => {
            if (el) el.indeterminate = selection.indeterminate
          }}
          onChange={() => onToggle(action.key)}
        />
        <span className="udt-checkbox__box">
          <Checkmark24Regular />
        </span>
      </label>
      <span className={`udt-action-row__title${disabled ? ' udt-action-row__title--muted' : ''}`}>
        {localizeText(t, action.title)}
      </span>
      <span className="udt-action-row__badge">
        {action.recommended ? (
          <span className="udt-badge">
            <Star24Filled /> {t('optimization.recommended')}
          </span>
        ) : null}
      </span>
      <span className="udt-action-row__info">
        <Tooltip title={t('wpf.actionDetailsWindowtitle')}>
          <button
            type="button"
            className="udt-icon-btn"
            aria-label={t('wpf.actionDetailsWindowtitle')}
            onClick={(event) => {
              event.stopPropagation()
              showDetails()
            }}
          >
            <Info24Regular />
          </button>
        </Tooltip>
      </span>
    </div>
  )
}

function CategoryCard({
  category,
  selectedKeys,
  busy,
  onToggle,
  summary
}: {
  category: OptimizationCategoryDefinition
  selectedKeys: string[]
  busy: boolean
  onToggle: (key: string) => void
  summary?: string
}): React.JSX.Element {
  const [expanded, setExpanded] = useState(true)
  const { t } = useTranslation()
  const presentation = useMemo(
    () => presentCategoryActions(category.actions, busy),
    [category.actions, busy]
  )
  return (
    <div className="udt-card udt-category">
      <button type="button" className="udt-category__header" onClick={() => setExpanded(!expanded)}>
        <div className="udt-card__copy">
          <div className="udt-card__title">{localizeText(t, category.title)}</div>
          <div className="udt-card__desc">{localizeText(t, category.description)}</div>
        </div>
        {summary && <span className="udt-category__summary">{summary}</span>}
        <span className={`udt-category__chevron${expanded ? ' udt-category__chevron--open' : ''}`}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
            <path d="M6 9l6 6 6-6" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </span>
      </button>
      {expanded && (
        <div className="udt-category__body">
          {presentation.visible.map(({ action, editable }) => (
            <ActionRow
              key={action.key}
              action={action}
              selected={selectedKeys.includes(action.key)}
              disabled={!editable}
              onToggle={onToggle}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function OptimizationTab({
  selectedKeys,
  busy,
  onToggle
}: {
  selectedKeys: string[]
  busy: boolean
  onToggle: (key: string) => void
}): React.JSX.Element {
  const categories = useOptimizationStore((s) => s.categories)
  const loading = useOptimizationStore((s) => s.loading)
  const error = useOptimizationStore((s) => s.error)

  const optimizationCategories = categories.filter((c) => !c.key.startsWith('cleanup.'))
  const showEmptyError =
    optimizationCategories.length === 0 &&
    !shouldShowEmptyPlaceholder({ loading, itemCount: 0, error })

  return (
    <div className="udt-optimization-layout udt-optimization-layout--solo">
      <div className="udt-optimization-layout__main">
        {loading && <SkeletonList rows={3} />}
        {showEmptyError ? null : optimizationCategories.map((category) => {
          const visible = presentCategoryActions(category.actions, busy).visible
          const appliedCount = visible.filter(({ action }) => action.applied === true).length
          return (
            <CategoryCard
              key={category.key}
              category={category}
              selectedKeys={selectedKeys}
              busy={busy}
              onToggle={onToggle}
              summary={`${appliedCount} / ${visible.length}`}
            />
          )
        })}
      </div>
    </div>
  )
}

function CleanupTab({
  selectedKeys,
  onSelectedKeysChange
}: {
  selectedKeys: string[]
  onSelectedKeysChange: (keys: string[]) => void
}): React.JSX.Element {
  const { t } = useTranslation()
  const categories = useOptimizationStore((s) => s.categories)
  const estimate = useOptimizationStore((s) => s.estimate)
  const [estimateBytes, setEstimateBytes] = useState<number | null>(null)
  const [estimating, setEstimating] = useState(false)

  const cleanupCategories = categories.filter(
    (c) => c.key.startsWith('cleanup.') && c.key !== 'cleanup.custom'
  )

  const toggleSelection = (key: string): void => {
    onSelectedKeysChange(
      selectedKeys.includes(key) ? selectedKeys.filter((k) => k !== key) : [...selectedKeys, key]
    )
  }

  const handleEstimate = async (): Promise<void> => {
    if (selectedKeys.length === 0) return
    setEstimating(true)
    try {
      const priorError = useOptimizationStore.getState().error
      const bytes = await estimate(selectedKeys)
      const nextError = useOptimizationStore.getState().error
      const freshError = nextError !== priorError ? nextError : null
      if (isFailedCleanupEstimate(bytes, freshError)) {
        reportStoreError(t, 'optimization.cleanupFailed', freshError)
        return
      }
      setEstimateBytes(bytes)
    } finally {
      setEstimating(false)
    }
  }

  const cleanupInfoDescription = estimating
    ? t('wpf.windowsOptimizationPageestimatedCleanupSizepending', {
        defaultValue: t('optimization.estimate')
      })
    : estimateBytes !== null
      ? t('wpf.windowsOptimizationPageestimatedCleanupSize', {
          defaultValue: `${t('optimization.estimateResult')}: {0}`
        }).replace('{0}', formatBytes(estimateBytes))
      : t('wpf.windowsOptimizationPagecleanupDescription', {
          defaultValue: t('optimization.cleanupHint')
        })

  return (
    <div className="udt-optimization-layout udt-optimization-layout--cleanup">
      <div className="udt-optimization-layout__main">
        {cleanupCategories.map((category) => {
          const presentation = presentCategoryActions(category.actions, estimating)
          const selectedCount = presentation.visible.filter(
            ({ action }) => selectedKeys.includes(action.key) || action.applied === true
          ).length
          const summary = t('wpf.windowsOptimizationcategoryselectionSummary', {
            defaultValue: t('optimization.selectedActions') + ' {0}/{1}'
          })
            .replace('{0}', String(selectedCount))
            .replace('{1}', String(presentation.visible.length))
          return (
            <CardExpander
              key={category.key}
              header={localizeText(t, category.title)}
              description={localizeText(t, category.description)}
              accessory={<span className="udt-category__summary">{summary}</span>}
              defaultExpanded={true}
            >
              {presentation.visible.map(({ action, editable }) => (
                <ActionRow
                  key={action.key}
                  action={action}
                  selected={selectedKeys.includes(action.key)}
                  disabled={!editable}
                  onToggle={toggleSelection}
                />
              ))}
            </CardExpander>
          )
        })}
      </div>
      <div className="udt-optimization-layout__divider" aria-hidden="true" />
      <div className="udt-optimization-layout__side">
        <div className="udt-card udt-side-card udt-cleanup-info">
          <div className="udt-card__title">
            {t('wpf.windowsOptimizationPagecleanupInfo', { defaultValue: t('optimization.estimate') })}
          </div>
          <div className="udt-card__desc">{cleanupInfoDescription}</div>
          <button
            type="button"
            className="udt-btn udt-btn--primary udt-cleanup-scan"
            disabled={selectedKeys.length === 0 || estimating}
            onClick={() => void handleEstimate()}
          >
            {t('wpf.windowsOptimizationPagescanbutton', { defaultValue: t('optimization.estimate') })}
          </button>
        </div>
        <CleanupRulesPanel />
      </div>
    </div>
  )
}

const NETWORK_MODE_I18N_KEYS: Record<NetworkAccelerationMode, string> = {
  Off: 'optimization.network.modes.off',
  SystemProxy: 'optimization.network.modes.systemProxy',
  Hosts: 'optimization.network.modes.hosts',
  DiagnosticsOnly: 'optimization.network.modes.diagnosticsOnly'
}

function NetworkTab(): React.JSX.Element {
  const { t } = useTranslation()
  const networkStatus = useOptimizationStore((s) => s.networkStatus)
  const networkError = useOptimizationStore((s) => s.error)
  const saveNetworkConfig = useOptimizationStore((s) => s.saveNetworkConfig)
  const startNetwork = useOptimizationStore((s) => s.startNetwork)
  const stopNetwork = useOptimizationStore((s) => s.stopNetwork)
  const loadTraffic = useOptimizationStore((s) => s.loadTraffic)
  const loadRuntime = useOptimizationStore((s) => s.loadRuntime)
  const [config, setConfig] = useState<NetworkAccelerationConfig | null>(null)
  const [saving, setSaving] = useState(false)
  const [starting, setStarting] = useState(false)
  const [stopping, setStopping] = useState(false)

  const isRunning = networkStatus?.isRunning === true

  useEffect(() => {
    if (!isRunning) return
    const trafficInFlight = { current: false }
    const runtimeInFlight = { current: false }
    let trafficTimer: ReturnType<typeof setInterval> | undefined
    let runtimeTimer: ReturnType<typeof setInterval> | undefined
    const startPolls = (): void => {
      if (trafficTimer != null) return
      trafficTimer = setInterval(() => {
        void runExclusivePoll(trafficInFlight, loadTraffic)
      }, 1000)
      runtimeTimer = setInterval(() => {
        void runExclusivePoll(runtimeInFlight, loadRuntime)
      }, 2000)
    }
    const stopPolls = (): void => {
      if (trafficTimer != null) clearInterval(trafficTimer)
      if (runtimeTimer != null) clearInterval(runtimeTimer)
      trafficTimer = undefined
      runtimeTimer = undefined
    }
    if (!document.hidden) startPolls()
    const unsubscribeVisibility = subscribeUiVisibility((active) => {
      if (active) startPolls()
      else stopPolls()
    })
    return () => {
      unsubscribeVisibility()
      stopPolls()
    }
  }, [isRunning, loadTraffic, loadRuntime])

  const editableConfig = config ?? networkStatus?.config ?? null

  const ensureConfig = (): NetworkAccelerationConfig | null => {
    if (config) return config
    if (!networkStatus) return null
    const next: NetworkAccelerationConfig = {
      ...networkStatus.config,
      domainGroups: [...networkStatus.config.domainGroups]
    }
    setConfig(next)
    return next
  }

  const handleSave = async (): Promise<void> => {
    const current = ensureConfig()
    if (!current) return
    setSaving(true)
    try {
      const ok = await saveNetworkConfig(current)
      if (!ok) {
        reportStoreError(t, 'optimization.network.saveFailed', useOptimizationStore.getState().error)
      }
    } finally {
      setSaving(false)
    }
  }

  const handleStart = async (): Promise<void> => {
    setStarting(true)
    try {
      const current = ensureConfig()
      if (current) {
        const saved = await saveNetworkConfig(current)
        if (!saved) {
          reportStoreError(t, 'optimization.network.saveFailed', useOptimizationStore.getState().error)
          return
        }
      }
      const ok = await startNetwork()
      if (!ok) {
        reportStoreError(t, 'optimization.network.startFailed', useOptimizationStore.getState().error)
      }
    } finally {
      setStarting(false)
    }
  }

  const handleStop = async (): Promise<void> => {
    setStopping(true)
    try {
      const ok = await stopNetwork()
      if (!ok) {
        reportStoreError(t, 'optimization.network.stopFailed', useOptimizationStore.getState().error)
      }
    } finally {
      setStopping(false)
    }
  }

  if (!networkStatus || !editableConfig) {
    if (networkError) return <></>
    return <SkeletonCard lines={3} withIcon />
  }

  const updateConfig = (patch: Partial<NetworkAccelerationConfig>): void => {
    const current = ensureConfig()
    if (!current) return
    setConfig({ ...current, ...patch })
  }

  const selectedTargets = getNetworkSelectedTargetCount(editableConfig)

  return (
    <div className="udt-network-layout">
      <div className="udt-card udt-card--row">
        <span
          className={`udt-status-dot${networkStatus.isRunning ? ' udt-status-dot--on' : ''}`}
        />
        <div className="udt-card__copy">
          <div className="udt-card__title">
            {networkStatus.isRunning ? t('optimization.network.running') : t('optimization.network.stopped')}
          </div>
          <div className="udt-card__desc">{networkStatus.statusText}</div>
        </div>
        <div className="udt-card__desc">
          {networkStatus.isBackendReady
            ? t('optimization.network.backendReady')
            : t('optimization.network.backendNotReady')}
        </div>
        <div className="udt-card__desc">
          {t('optimization.network.targetsLabel')}: {selectedTargets}
        </div>
        <div className="udt-card__desc">
          {t('optimization.network.portLabel')}: {editableConfig.listenPort}
        </div>
      </div>

      <div className="udt-card udt-network-config">
        <div className="udt-card__title">{t('optimization.network.config')}</div>
        <div className="udt-network-config__fields">
          <div className="udt-network-field udt-network-field--switch">
            <span className="udt-network-field__label">{t('optimization.network.accelerationEnabled')}</span>
            <label className="udt-switch">
              <input
                type="checkbox"
                checked={editableConfig.accelerationEnabled}
                onChange={(e) => updateConfig({ accelerationEnabled: e.target.checked })}
              />
              <span className="udt-switch__track" />
            </label>
          </div>
          <div className="udt-network-field">
            <span className="udt-network-field__label">{t('optimization.network.mode')}</span>
            <Select<NetworkAccelerationMode>
              aria-label={t('optimization.network.mode')}
              className="udt-network-select"
              popupMatchSelectWidth={false}
              value={editableConfig.mode}
              onChange={(mode) => updateConfig({ mode })}
              options={NETWORK_ACCELERATION_MODES.map((mode) => ({
                value: mode,
                label: t(NETWORK_MODE_I18N_KEYS[mode])
              }))}
            />
          </div>
        </div>
        <div className="udt-network-config__actions">
          <button type="button" className="udt-btn udt-btn--primary" disabled={saving} onClick={() => void handleSave()}>
            {t('optimization.network.save')}
          </button>
          <button
            type="button"
            className="udt-btn udt-btn--secondary"
            disabled={
              !networkStatus.isBackendReady ||
              editableConfig.mode === 'Hosts' ||
              starting
            }
            onClick={() => void handleStart()}
          >
            <PlayCircle24Regular /> {t('optimization.network.start')}
          </button>
          <button
            type="button"
            className="udt-btn udt-btn--danger"
            disabled={stopping}
            onClick={() => void handleStop()}
          >
            <Stop24Regular /> {t('optimization.network.stop')}
          </button>
        </div>
      </div>

      <NetworkPanels />
    </div>
  )
}

const TAB_I18N_KEYS: Record<TabKey, string> = {
  optimization: 'wpf.windowsOptimizationPagetaboptimization',
  cleanup: 'wpf.windowsOptimizationPagetabcleanup',
  driverDownload: 'wpf.windowsOptimizationPagetabdriverDownload',
  networkAcceleration: 'wpf.windowsOptimizationPagetabnetworkAcceleration',
  gameBoost: 'optimization.tabs.gameBoost',
  cursor: 'mouse.title'
}

const TAB_FALLBACK_KEYS: Record<TabKey, string> = {
  optimization: 'optimization.tabs.optimization',
  cleanup: 'optimization.tabs.cleanup',
  driverDownload: 'optimization.tabs.driverDownload',
  networkAcceleration: 'optimization.tabs.networkAcceleration',
  gameBoost: 'optimization.tabs.gameBoost',
  cursor: 'mouse.title'
}

const TABS: OptimizationTabKey[] = [
  'optimization',
  'cleanup',
  'driverDownload',
  'networkAcceleration',
  'gameBoost',
  'cursor'
]

export default function WindowsOptimizationPage(): React.JSX.Element {
  const { t } = useTranslation()
  const load = useOptimizationStore((s) => s.load)
  const loadNetwork = useOptimizationStore((s) => s.loadNetwork)
  const categories = useOptimizationStore((s) => s.categories)
  const applyRecommended = useOptimizationStore((s) => s.applyRecommended)
  const apply = useOptimizationStore((s) => s.apply)
  const runCleanup = useOptimizationStore((s) => s.runCleanup)
  const startNetwork = useOptimizationStore((s) => s.startNetwork)
  const setNetworkGroupEnabled = useOptimizationStore((s) => s.setNetworkGroupEnabled)
  const networkStatus = useOptimizationStore((s) => s.networkStatus)
  const driverSelectedCount = useDriverStore((s) => s.selectedIds.length)
  const [tab, setTab] = useState<TabKey>('optimization')
  const [optSelectedKeys, setOptSelectedKeys] = useState<string[]>([])
  const [cleanupSelectedKeys, setCleanupSelectedKeys] = useState<string[]>([])
  const [chromeBusy, setChromeBusy] = useState(false)
  const cleanupDefaultsApplied = useRef(false)
  const networkAccelerationInstalled = isInstallerOptionalFeatureEnabled(
    window.bridge?.installerSelection?.features,
    'networkAcceleration'
  )
  const visibleTabs = useMemo(
    () => visibleOptimizationTabs(TABS, networkAccelerationInstalled),
    [networkAccelerationInstalled]
  )

  useEffect(() => {
    void load()
    if (networkAccelerationInstalled) void loadNetwork()
  }, [load, loadNetwork, networkAccelerationInstalled])

  useEffect(() => {
    if (!visibleTabs.includes(tab)) setTab(visibleTabs[0] ?? 'optimization')
  }, [tab, visibleTabs])

  useEffect(() => {
    if (cleanupDefaultsApplied.current || categories.length === 0) return
    cleanupDefaultsApplied.current = true
    setCleanupSelectedKeys(collectRecommendedActionKeys(categories, (key) => key.startsWith('cleanup.')))
  }, [categories])

  const toggleOptSelection = (key: string): void => {
    setOptSelectedKeys((prev) => (prev.includes(key) ? prev.filter((k) => k !== key) : [...prev, key]))
  }

  const starTitle = useMemo(() => {
    if (tab === 'networkAcceleration') {
      return t('wpf.networkAccelerationPageselectionFavoriteTooltip', {
        defaultValue: t('optimization.selectRecommended')
      })
    }
    if (tab === 'driverDownload') {
      return t('optimization.driver.selectRecommended', { defaultValue: t('optimization.selectRecommended') })
    }
    return t('wpf.windowsOptimizationPageselectRecommendedbutton', {
      defaultValue: t('optimization.selectRecommended')
    })
  }, [t, tab])

  const playTitle = useMemo(() => {
    if (tab === 'networkAcceleration') {
      return t('optimization.network.start', { defaultValue: 'Start' })
    }
    if (tab === 'driverDownload') {
      return t('optimization.driver.startAll', { defaultValue: t('optimization.applyRecommended') })
    }
    if (tab === 'cleanup') {
      return t('optimization.runCleanup')
    }
    return t('optimization.applyRecommended')
  }, [t, tab])

  const handleStar = async (): Promise<void> => {
    if (chromeBusy) return
    if (tab === 'driverDownload') {
      useDriverStore.getState().selectRecommended()
      return
    }
    if (tab === 'networkAcceleration') {
      setChromeBusy(true)
      try {
        const groups = networkStatus?.config.domainGroups ?? []
        const recommended = groups.filter(
          (group) => group.isFavorite || NETWORK_RECOMMENDED_GROUP_IDS.has(group.id)
        )
        for (const group of recommended) {
          const ok = await setNetworkGroupEnabled(group.id, true)
          if (!ok) {
            reportStoreError(
              t,
              'optimization.network.saveFailed',
              useOptimizationStore.getState().error
            )
            return
          }
        }
      } finally {
        setChromeBusy(false)
      }
      return
    }
    if (tab === 'cleanup') {
      setCleanupSelectedKeys(collectRecommendedActionKeys(categories, (key) => key.startsWith('cleanup.')))
      return
    }
    setOptSelectedKeys(collectRecommendedActionKeys(categories, (key) => !key.startsWith('cleanup.')))
  }

  const handlePlay = async (): Promise<void> => {
    if (chromeBusy) return
    setChromeBusy(true)
    try {
      if (tab === 'networkAcceleration') {
        const status = useOptimizationStore.getState().networkStatus
        if (status) {
          const saved = await useOptimizationStore.getState().saveNetworkConfig(status.config)
          if (!saved) {
            reportStoreError(
              t,
              'optimization.network.saveFailed',
              useOptimizationStore.getState().error
            )
            return
          }
        }
        const ok = await startNetwork()
        if (!ok) {
          reportStoreError(
            t,
            'optimization.network.startFailed',
            useOptimizationStore.getState().error
          )
        }
        return
      }
      if (tab === 'driverDownload') {
        await useDriverStore.getState().startSelected()
        return
      }
      if (tab === 'cleanup') {
        if (cleanupSelectedKeys.length === 0) return
        const ok = await runCleanup(cleanupSelectedKeys)
        if (ok) {
          setCleanupSelectedKeys(
            collectRecommendedActionKeys(
              useOptimizationStore.getState().categories,
              (key) => key.startsWith('cleanup.')
            )
          )
        } else {
          reportStoreError(t, 'optimization.cleanupFailed', useOptimizationStore.getState().error)
        }
        return
      }
      if (optSelectedKeys.length > 0) {
        const ok = await apply(optSelectedKeys)
        if (ok) setOptSelectedKeys([])
        else reportStoreError(t, 'optimization.applyFailed', useOptimizationStore.getState().error)
        return
      }
      const ok = await applyRecommended()
      if (!ok) {
        reportStoreError(t, 'optimization.applyFailed', useOptimizationStore.getState().error)
      }
    } finally {
      setChromeBusy(false)
    }
  }

  const playDisabled = isOptimizationPlayDisabled({
    tab,
    busy: chromeBusy,
    cleanupSelectedCount: cleanupSelectedKeys.length,
    driverSelectedCount,
    networkStatus
  })

  return (
    <div className="udt-page udt-optimization-page udt-content-column udt-content-fill">
      <h1 className="udt-page__title">{t('optimization.title')}</h1>
      <p className="udt-page__subtitle">{t('optimization.info')}</p>

      <div className="udt-opt-chrome">
        <div className="udt-segmented-nav" role="tablist" aria-label={t('optimization.title')}>
          {visibleTabs.map((key) => (
            <button
              key={key}
              type="button"
              role="tab"
              aria-selected={tab === key}
              className={`udt-segmented-nav__item${tab === key ? ' udt-segmented-nav__item--active' : ''}`}
              onClick={() => setTab(key)}
            >
              {t(TAB_I18N_KEYS[key], { defaultValue: t(TAB_FALLBACK_KEYS[key]) })}
              <span className="udt-segmented-nav__indicator" />
            </button>
          ))}
        </div>
        <div className="udt-opt-chrome__actions">
          <button
            type="button"
            className="udt-opt-chrome__icon-btn"
            title={starTitle}
            aria-label={starTitle}
            disabled={chromeBusy}
            onClick={() => void handleStar()}
          >
            <Star24Regular />
          </button>
          <button
            type="button"
            className="udt-opt-chrome__icon-btn"
            title={playTitle}
            aria-label={playTitle}
            disabled={playDisabled}
            onClick={() => void handlePlay()}
          >
            <Play24Regular />
          </button>
        </div>
      </div>

      <div className="udt-tab-content" key={tab}>
        {tab === 'optimization' && (
          <OptimizationTab
            selectedKeys={optSelectedKeys}
            busy={chromeBusy}
            onToggle={toggleOptSelection}
          />
        )}
        {tab === 'cleanup' && (
          <CleanupTab
            selectedKeys={cleanupSelectedKeys}
            onSelectedKeysChange={setCleanupSelectedKeys}
          />
        )}
        {tab === 'driverDownload' && <DriverDownloadPanel />}
        {tab === 'networkAcceleration' && <NetworkTab />}
        {tab === 'gameBoost' && <GameBoostPanel />}
        {tab === 'cursor' && <CursorPointerPanel />}
      </div>
    </div>
  )
}
