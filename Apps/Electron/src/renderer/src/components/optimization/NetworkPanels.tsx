import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AutoComplete,
  Modal,
  Popconfirm,
  Switch,
  message
} from 'antd'
import {
  Checkmark24Regular,
  Beaker24Regular,
  Info24Regular,
  Search24Regular,
  Star24Filled,
  ArrowSync24Regular,
  ArrowUndo24Regular
} from '../icons/fluent'
import TrendChart from '../dashboard/TrendChart'
import {
  optimizationApi,
  type NetworkDomainGroup,
  type NetworkDomainSubItem
} from '../../api/optimization'
import { localizeHostError } from '../../api/bridge'
import { notify } from '../../notifications'
import { useOptimizationStore } from '../../stores/optimizationStore'
import { presentActionNotification, resolveActionError } from '../../utils/optimizationPresentation'
import './optimization.css'

// ── Formatting helpers (mirror NetworkAccelerationControl) ──────

function formatRate(bytesPerSecond: number): string {
  if (bytesPerSecond < 1024) return `${Math.max(0, bytesPerSecond).toFixed(0)} B/s`
  if (bytesPerSecond < 1024 * 1024) return `${(bytesPerSecond / 1024).toFixed(1)} KB/s`
  if (bytesPerSecond < 1024 * 1024 * 1024) return `${(bytesPerSecond / (1024 * 1024)).toFixed(1)} MB/s`
  return `${(bytesPerSecond / (1024 * 1024 * 1024)).toFixed(1)} GB/s`
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes.toFixed(0)} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`
}

// ── Brand icons (port of BrandIconGeometry + CreateBrandIcon) ───

const BRAND_GEOMETRY: Record<string, string> = {
  SteamLogo:
    'M11.979,0 C5.678,0 0.511,4.86 0.022,11.037 L6.454,13.695 C6.999,13.324 7.657,13.105 8.366,13.105 C8.429,13.105 8.491,13.109 8.554,13.111 L11.415,8.969 L11.415,8.91 C11.415,6.415 13.443,4.386 15.939,4.386 C18.433,4.386 20.463,6.417 20.463,8.913 C20.463,11.408 18.433,13.438 15.939,13.438 L15.834,13.438 L11.758,16.349 C11.758,16.401 11.762,16.454 11.762,16.508 C11.762,18.383 10.247,19.904 8.372,19.904 C6.737,19.904 5.356,18.731 5.041,17.177 L0.436,15.27 C1.862,20.307 6.486,24 11.979,24 C18.606,24 23.978,18.627 23.978,12 C23.978,5.373 18.605,0 11.979,0 Z M7.54,18.21 L6.067,17.6 C6.329,18.143 6.781,18.599 7.381,18.85 C8.678,19.389 10.174,18.774 10.713,17.475 C10.976,16.845 10.977,16.156 10.718,15.526 C10.459,14.896 9.968,14.405 9.341,14.143 C8.717,13.883 8.051,13.894 7.463,14.113 L8.986,14.743 C9.942,15.143 10.395,16.243 9.995,17.198 C9.598,18.155 8.497,18.608 7.54,18.21 Z M18.955,8.907 C18.955,7.245 17.602,5.892 15.94,5.892 C14.275,5.892 12.925,7.245 12.925,8.907 C12.925,10.572 14.275,11.922 15.94,11.922 C17.603,11.922 18.955,10.572 18.955,8.907 Z M13.682,8.902 C13.682,7.65 14.695,6.636 15.947,6.636 C17.196,6.636 18.213,7.65 18.213,8.902 C18.213,10.153 17.196,11.167 15.947,11.167 C14.694,11.167 13.682,10.153 13.682,8.902 Z',
  GitHubLogo:
    'M12,0.297 C5.37,0.297 0,5.67 0,12.297 C0,17.6 3.438,22.097 8.205,23.682 C8.805,23.795 9.025,23.424 9.025,23.105 C9.025,22.82 9.015,22.065 9.01,21.065 C5.672,21.789 4.968,19.455 4.968,19.455 C4.422,18.07 3.633,17.7 3.633,17.7 C2.546,16.956 3.717,16.971 3.717,16.971 C4.922,17.055 5.555,18.207 5.555,18.207 C6.625,20.042 8.364,19.512 9.05,19.205 C9.158,18.429 9.467,17.9 9.81,17.6 C7.145,17.3 4.344,16.268 4.344,11.67 C4.344,10.36 4.809,9.29 5.579,8.45 C5.444,8.147 5.039,6.927 5.684,5.274 C5.684,5.274 6.689,4.952 8.984,6.504 C9.944,6.237 10.964,6.105 11.984,6.099 C13.004,6.105 14.024,6.237 14.984,6.504 C17.264,4.952 18.269,5.274 18.269,5.274 C18.914,6.927 18.509,8.147 18.389,8.45 C19.154,9.29 19.619,10.36 19.619,11.67 C19.619,16.28 16.814,17.295 14.144,17.59 C14.564,17.95 14.954,18.686 14.954,19.81 C14.954,21.416 14.939,22.706 14.939,23.096 C14.939,23.411 15.149,23.786 15.764,23.666 C20.565,22.092 24,17.592 24,12.297 C24,5.67 18.627,0.297 12,0.297 Z',
  TwitchLogo:
    'M11.571,4.714 L13.286,4.714 L13.286,9.857 L11.57,9.857 Z M16.286,4.714 L18,4.714 L18,9.857 L16.286,9.857 Z M6,0 L1.714,4.286 L1.714,19.714 L6.857,19.714 L6.857,24 L11.143,19.714 L14.571,19.714 L22.286,12 L22.286,0 Z M20.571,11.143 L17.143,14.571 L13.714,14.571 L10.714,17.571 L10.714,14.571 L6.857,14.571 L6.857,1.714 L20.571,1.714 Z',
  RobloxLogo:
    'M18.926,23.998 L0,18.892 L5.075,0.002 L24,5.108 Z M15.348,10.09 L10.066,8.637 L8.652,13.91 L13.934,15.363 Z',
  CdnLogo:
    'M12,2 C6.477,2 2,6.477 2,12 C2,17.523 6.477,22 12,22 C17.523,22 22,17.523 22,12 C22,6.477 17.523,2 12,2 Z M2.5,9 H21.5 M2.5,15 H21.5 M12,2 C9.5,4.8 8.3,8.1 8.3,12 C8.3,15.9 9.5,19.2 12,22 M12,2 C14.5,4.8 15.7,8.1 15.7,12 C15.7,15.9 14.5,19.2 12,22'
}

const BRAND_BACKGROUND: Record<string, string> = {
  SteamLogo: '#478fe8',
  GitHubLogo: '#626262',
  TwitchLogo: '#0a72c5',
  RobloxLogo: '#eff1f3',
  CdnLogo: '#eff1f3'
}

const BRAND_FOREGROUND: Record<string, string> = {
  SteamLogo: '#ffffff',
  GitHubLogo: '#ffffff',
  TwitchLogo: '#ffffff',
  RobloxLogo: '#656a71',
  CdnLogo: '#656a71'
}

function BrandIcon({ iconKey, displayName }: { iconKey: string | null; displayName: string }): React.JSX.Element {
  const background = iconKey ? BRAND_BACKGROUND[iconKey] ?? 'var(--udt-control-fill-secondary)' : 'var(--udt-control-fill-secondary)'
  const foreground = iconKey ? BRAND_FOREGROUND[iconKey] ?? '#ffffff' : '#ffffff'
  const geometry = iconKey ? BRAND_GEOMETRY[iconKey] : undefined
  const isCdn = iconKey === 'CdnLogo'
  return (
    <span
      className="udt-network-brand"
      style={{ background }}
      title={displayName}
    >
      {geometry ? (
        <svg viewBox="0 0 24 24" width="22" height="22">
          <path
            d={geometry}
            fill={isCdn ? 'none' : foreground}
            stroke={isCdn ? foreground : undefined}
            strokeWidth={isCdn ? 1.4 : undefined}
          />
        </svg>
      ) : (
        <span style={{ color: foreground }}>
          {(iconKey ?? '?').slice(0, 1).toUpperCase()}
        </span>
      )}
    </span>
  )
}

// ── Domain group helpers ─────────────────────────────────────────

function getGroupDomains(group: NetworkDomainGroup): string[] {
  const domains = [...(group.domains ?? []), ...(group.subItems ?? []).map((sub) => sub.domain)]
    .map((domain) => (domain ?? '').trim().replace(/\.$/, '').toLowerCase())
    .filter(Boolean)
  return [...new Set(domains)]
}

function getGroupTargetCount(group: NetworkDomainGroup): number {
  return getGroupDomains(group).length
}

function getGroupSelectedCount(group: NetworkDomainGroup): number {
  if (!group.enabled) return 0
  const direct = (group.domains ?? []).filter((domain) => domain.trim().length > 0).length
  const subItems = (group.subItems ?? []).filter((sub) => sub.enabled).length
  return direct + subItems
}

const RECOMMENDED_GROUP_IDS = new Set(['steam', 'github', 'public-cdn', 'twitch', 'roblox'])

function reportNetworkStoreError(
  t: (key: string, options?: Record<string, unknown>) => string,
  fallbackKey: string
): void {
  const fallback = t(fallbackKey)
  const err = useOptimizationStore.getState().error
  const localized = localizeHostError(resolveActionError(err, fallback), t)
  const notif = presentActionNotification(localized, fallback)
  notify({
    title: notif.title,
    message: notif.message ?? '',
    severity: 'Error'
  })
}

function getRecommendedTargetGroups(groups: NetworkDomainGroup[]): NetworkDomainGroup[] {
  return groups
    .filter((group) => group.isFavorite || RECOMMENDED_GROUP_IDS.has(group.id))
    .slice(0, 8)
}

// ── Targets card (service group tree) ────────────────────────────

function NetworkTargetsCard(): React.JSX.Element {
  const { t } = useTranslation()
  const networkStatus = useOptimizationStore((s) => s.networkStatus)
  const setNetworkGroupEnabled = useOptimizationStore((s) => s.setNetworkGroupEnabled)
  const setNetworkSubItemEnabled = useOptimizationStore((s) => s.setNetworkSubItemEnabled)
  const runtimeSnapshot = useOptimizationStore((s) => s.runtimeSnapshot)
  const [searchText, setSearchText] = useState('')
  const [expandedIds, setExpandedIds] = useState<string[]>([])
  const [recommendedOpen, setRecommendedOpen] = useState(false)

  const groups = networkStatus?.config.domainGroups ?? []
  const query = searchText.trim().toLowerCase()

  const filteredGroups = groups
    .filter((group) => {
      if (!query) return true
      if (group.displayName.toLowerCase().includes(query)) return true
      return (group.subItems ?? []).some(
        (sub) =>
          sub.displayName.toLowerCase().includes(query) ||
          (sub.domain ?? '').toLowerCase().includes(query)
      )
    })
    .sort((a, b) => Number(b.isFavorite) - Number(a.isFavorite))

  const toggleExpanded = (id: string): void => {
    setExpandedIds((prev) =>
      prev.includes(id) ? prev.filter((g) => g !== id) : [...prev, id]
    )
  }

  const groupRuntime = (group: NetworkDomainGroup): string => {
    const selected = getGroupSelectedCount(group)
    const total = getGroupTargetCount(group)
    let active = 0
    if (runtimeSnapshot) {
      const hosts = new Set(getGroupDomains(group))
      active = runtimeSnapshot.destinations
        .filter((destination) => hosts.has(destination.host.toLowerCase()))
        .reduce((sum, destination) => sum + destination.activeConnections, 0)
    }
    return t('optimization.network.groupRuntime', { selected, total, active })
  }

  const handleGroupToggle = (group: NetworkDomainGroup, enabled: boolean): void => {
    void (async () => {
      const ok = await setNetworkGroupEnabled(group.id, enabled)
      if (!ok) reportNetworkStoreError(t, 'optimization.network.saveFailed')
    })()
  }

  // Electron three-state CheckBox cycle is true → indeterminate → false → true;
  // clicking an indeterminate group checkbox clears the whole group, while a
  // plain checkbox would flip to checked (select all).
  const handleGroupCheckboxChange = (
    group: NetworkDomainGroup,
    someEnabled: boolean,
    event: React.ChangeEvent<HTMLInputElement>
  ): void => {
    handleGroupToggle(group, someEnabled ? false : event.target.checked)
  }

  const handleSubItemToggle = (group: NetworkDomainGroup, sub: NetworkDomainSubItem, enabled: boolean): void => {
    void (async () => {
      const ok = await setNetworkSubItemEnabled(group.id, sub.id, enabled)
      if (!ok) reportNetworkStoreError(t, 'optimization.network.saveFailed')
    })()
  }

  const recommendedGroups = getRecommendedTargetGroups(groups)
  const recommendedSelection = new Set(
    recommendedGroups.filter((group) => getGroupSelectedCount(group) > 0).map((group) => group.id)
  )

  const handleRecommendedToggle = (group: NetworkDomainGroup, enabled: boolean): void => {
    void (async () => {
      const ok = await setNetworkGroupEnabled(group.id, enabled)
      if (!ok) reportNetworkStoreError(t, 'optimization.network.saveFailed')
    })()
  }

  return (
    <div className="udt-card">
      <div className="udt-network-heading">
        <div>
          <div className="udt-card__title">{t('optimization.network.targetsHeading')}</div>
          <div className="udt-card__desc">{t('optimization.network.domainGroupsHint')}</div>
        </div>
        <div className="udt-network-heading__actions">
          <div className="udt-network-recommended">
            <button
              type="button"
              className="udt-btn udt-btn--secondary"
              disabled={recommendedGroups.length === 0}
              onClick={() => setRecommendedOpen(!recommendedOpen)}
            >
              <Star24Filled /> {t('optimization.network.recommendedMenu')}
            </button>
            {recommendedOpen && (
              <>
                <div className="udt-network-recommended__backdrop" onClick={() => setRecommendedOpen(false)} />
                <div className="udt-network-recommended__popup">
                  {recommendedGroups.length === 0 && (
                    <div className="udt-network-recommended__empty">
                      {t('optimization.network.domainGroupsEmptyTitle')}
                    </div>
                  )}
                  {recommendedGroups.map((group) => (
                    <label key={group.id} className="udt-network-recommended__item">
                      <input
                        type="checkbox"
                        checked={recommendedSelection.has(group.id)}
                        onChange={(e) => handleRecommendedToggle(group, e.target.checked)}
                      />
                      <span>{group.displayName}</span>
                    </label>
                  ))}
                </div>
              </>
            )}
          </div>
          <div className="udt-network-search">
            <Search24Regular />
            <input
              type="text"
              placeholder={t('optimization.network.searchTargets')}
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
            />
          </div>
        </div>
      </div>

      {filteredGroups.length === 0 ? (
        <div className="udt-empty">
          <div className="udt-empty__title">{t('optimization.network.domainGroupsEmptyTitle')}</div>
          <div className="udt-empty__description">{t('optimization.network.domainGroupsEmptyDescription')}</div>
        </div>
      ) : (
        <div className="udt-network-groups">
          {filteredGroups.map((group) => {
            const selectedCount = getGroupSelectedCount(group)
            const totalCount = getGroupTargetCount(group)
            const allEnabled = totalCount > 0 && selectedCount === totalCount
            const someEnabled = selectedCount > 0 && !allEnabled
            const expanded = expandedIds.includes(group.id)

            return (
              <div key={group.id} className="udt-network-group">
                <div className="udt-network-group__header">
                  <label className="udt-checkbox">
                    <input
                      type="checkbox"
                      checked={allEnabled}
                      ref={(el) => {
                        if (el) el.indeterminate = someEnabled
                      }}
                      onChange={(e) => handleGroupCheckboxChange(group, someEnabled, e)}
                    />
                    <span className="udt-checkbox__box">
                      <Checkmark24Regular />
                    </span>
                  </label>
                  <BrandIcon iconKey={group.iconKey} displayName={group.displayName} />
                  <span className="udt-network-group__name">{group.displayName}</span>
                  {group.isFavorite && <Star24Filled className="udt-network-group__fav" />}
                  <span className="udt-network-group__runtime">{groupRuntime(group)}</span>
                  <button
                    type="button"
                    className={`udt-network-group__chevron${expanded ? ' udt-network-group__chevron--open' : ''}`}
                    onClick={() => toggleExpanded(group.id)}
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                      <path d="M6 9l6 6 6-6" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </button>
                </div>
                {expanded && (
                  <div className="udt-network-group__body">
                    {(group.subItems ?? []).map((sub) => (
                      <div key={sub.id} className="udt-network-subitem">
                        <label className="udt-checkbox">
                          <input
                            type="checkbox"
                            checked={sub.enabled}
                            onChange={(e) => handleSubItemToggle(group, sub, e.target.checked)}
                          />
                          <span className="udt-checkbox__box">
                            <Checkmark24Regular />
                          </span>
                        </label>
                        <span className="udt-network-subitem__name">{sub.displayName}</span>
                        {sub.isBeta && (
                          <span className="udt-network-subitem__beta">
                            <Beaker24Regular /> Beta
                          </span>
                        )}
                        <span className="udt-network-subitem__domain" title={sub.domain}>
                          {sub.domain}
                        </span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      )}

      <div className="udt-card__desc udt-network-selection-hint">
        {t('optimization.network.selectionHint')}
      </div>
    </div>
  )
}

// ── Traffic card ─────────────────────────────────────────────────

function NetworkTrafficCard(): React.JSX.Element | null {
  const { t } = useTranslation()
  const isRunning = useOptimizationStore((s) => s.networkStatus?.isRunning === true)
  const trafficSnapshot = useOptimizationStore((s) => s.trafficSnapshot)
  const runtimeSnapshot = useOptimizationStore((s) => s.runtimeSnapshot)

  const [uploadSamples, setUploadSamples] = useState<number[]>([])
  const [downloadSamples, setDownloadSamples] = useState<number[]>([])
  const [uploadRate, setUploadRate] = useState(0)
  const [downloadRate, setDownloadRate] = useState(0)
  const prevRef = useRef<{ snapshot: { bytesUploaded: number; bytesDownloaded: number }; at: number } | null>(null)

  useEffect(() => {
    if (!trafficSnapshot) return
    const prev = prevRef.current
    if (prev && trafficSnapshot.bytesUploaded < prev.snapshot.bytesUploaded) {
      setUploadSamples([])
      setDownloadSamples([])
    }
    let nextUpload = 0
    let nextDownload = 0
    if (prev) {
      const elapsed = Math.max(0.25, (Date.now() - prev.at) / 1000)
      nextUpload = Math.max(0, (trafficSnapshot.bytesUploaded - prev.snapshot.bytesUploaded) / elapsed)
      nextDownload = Math.max(0, (trafficSnapshot.bytesDownloaded - prev.snapshot.bytesDownloaded) / elapsed)
    }
    setUploadRate(nextUpload)
    setDownloadRate(nextDownload)
    setUploadSamples((samples) => [...samples, nextUpload / 1024].slice(-60))
    setDownloadSamples((samples) => [...samples, nextDownload / 1024].slice(-60))
    prevRef.current = { snapshot: trafficSnapshot, at: Date.now() }
  }, [trafficSnapshot])

  useEffect(() => {
    if (!isRunning) {
      setUploadSamples([])
      setDownloadSamples([])
      setUploadRate(0)
      setDownloadRate(0)
      prevRef.current = null
    }
  }, [isRunning])

  // Chart inputs are memoized so the 1 Hz traffic poll reuses stable series
  // objects (TrendChart applies data updates incrementally).
  const trafficSeries = useMemo(
    () => [
      { name: t('optimization.network.metrics.upload'), color: '#ee9146', data: uploadSamples },
      { name: t('optimization.network.metrics.download'), color: '#4da6e8', data: downloadSamples }
    ],
    [uploadSamples, downloadSamples, t]
  )
  const labels = useMemo(() => uploadSamples.map((_, index) => `${index}`), [uploadSamples])

  if (!isRunning) return null

  const health = formatHealth(runtimeSnapshot?.healthStatus ?? 'unknown', t)

  return (
    <div className="udt-card">
      <div className="udt-card__title">{t('optimization.network.trafficHeading')}</div>
      <div className="udt-network-traffic">
        <div className="udt-network-traffic__metrics">
          <div className="udt-network-traffic__metric">
            <span className="udt-network-traffic__label">{t('optimization.network.metrics.upload')}</span>
            <span className="udt-network-traffic__value">{formatRate(uploadRate)}</span>
          </div>
          <div className="udt-network-traffic__metric">
            <span className="udt-network-traffic__label">{t('optimization.network.metrics.download')}</span>
            <span className="udt-network-traffic__value">{formatRate(downloadRate)}</span>
          </div>
          <div className="udt-network-traffic__metric">
            <span className="udt-network-traffic__label">{t('optimization.network.metrics.connections')}</span>
            <span className="udt-network-traffic__value">
              {trafficSnapshot
                ? `${trafficSnapshot.activeConnections} / ${trafficSnapshot.totalConnections}`
                : '—'}
            </span>
          </div>
          <div className="udt-network-traffic__metric">
            <span className="udt-network-traffic__label">{t('optimization.network.metrics.total')}</span>
            <span className="udt-network-traffic__value">
              {trafficSnapshot
                ? formatBytes(trafficSnapshot.bytesUploaded + trafficSnapshot.bytesDownloaded)
                : '—'}
            </span>
          </div>
          <div className="udt-network-traffic__metric">
            <span className="udt-network-traffic__label">{t('optimization.network.metrics.health')}</span>
            <span className="udt-network-traffic__value">{health}</span>
          </div>
        </div>

        <div className="udt-network-traffic__chart">
          <TrendChart series={trafficSeries} labels={labels} height={156} />
        </div>

        <div className="udt-network-traffic__legend">
          <span className="udt-network-traffic__legend-dot udt-network-traffic__legend-dot--upload" />
          <span>{t('optimization.network.metrics.upload')}</span>
          <span className="udt-network-traffic__legend-dot udt-network-traffic__legend-dot--download" />
          <span>{t('optimization.network.metrics.download')}</span>
        </div>

        <div className="udt-network-traffic__status">
          {t('optimization.network.trafficLive')}
        </div>

        <div className="udt-network-traffic__lists">
          <div className="udt-network-traffic__list">
            <div className="udt-network-traffic__list-title">{t('optimization.network.connectionsHeading')}</div>
            <div className="udt-network-traffic__list-summary">
              {runtimeSnapshot
                ? t('optimization.network.connectionSummary', {
                    active: runtimeSnapshot.traffic.activeConnections,
                    total: runtimeSnapshot.traffic.totalConnections
                  })
                : t('optimization.network.connectionsWaiting')}
            </div>
            <div className="udt-network-traffic__rows">
              {(runtimeSnapshot?.connections ?? []).slice(0, 8).map((connection, index) => {
                const stateKey = ['active', 'completed', 'blocked', 'failed', 'stopped'].includes(
                  connection.state
                )
                  ? connection.state
                  : 'unknown'
                return (
                  <div key={`${connection.host}:${connection.port}:${index}`} className="udt-network-traffic__row">
                    <span className="udt-network-traffic__row-title" title={`${connection.host}:${connection.port}`}>
                      {connection.host || t('optimization.network.unknownHost')}:{connection.port}
                    </span>
                    <span
                      className={`udt-network-traffic__row-detail${connection.state === 'failed' || connection.state === 'blocked' ? ' udt-network-traffic__row-detail--critical' : ''}`}
                    >
                      {t(`optimization.network.connectionStates.${stateKey}`)}{' '}
                      {connection.connectLatencyMs != null ? `${connection.connectLatencyMs} ms` : '-'}
                    </span>
                  </div>
                )
              })}
            </div>
          </div>
          <div className="udt-network-traffic__list">
            <div className="udt-network-traffic__list-title">{t('optimization.network.destinationsHeading')}</div>
            <div className="udt-network-traffic__list-summary">
              {runtimeSnapshot
                ? t('optimization.network.destinationSummary', { count: runtimeSnapshot.destinations.length })
                : t('optimization.network.destinationsWaiting')}
            </div>
            <div className="udt-network-traffic__rows">
              {(runtimeSnapshot?.destinations ?? []).slice(0, 8).map((destination, index) => (
                <div key={`${destination.host}:${destination.port}:${index}`} className="udt-network-traffic__row">
                  <span className="udt-network-traffic__row-title" title={`${destination.host}:${destination.port}`}>
                    {destination.host}:{destination.port}
                  </span>
                  <span className="udt-network-traffic__row-detail">
                    {t('optimization.network.destinationRow', {
                      count: destination.totalConnections,
                      latency:
                        destination.lastConnectLatencyMs != null
                          ? `${destination.lastConnectLatencyMs} ms`
                          : '-'
                    })}
                  </span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

function formatHealth(health: string, t: (key: string, options?: Record<string, unknown>) => string): string {
  if (health === 'healthy') return t('optimization.network.health.healthy')
  if (health === 'degraded') return t('optimization.network.health.degraded')
  if (health === 'stopped') return t('optimization.network.health.stopped')
  return t('optimization.network.health.unknown')
}

// ── Diagnostics row (NAT / DNS / IPv6) ───────────────────────────

const STUN_SERVERS = ['stun.miwifi.com', 'stun.l.google.com', 'stun.cloudflare.com']
const DNS_SERVERS = ['223.5.5.5', '223.6.6.6', '119.29.29.29', '1.1.1.1', '8.8.8.8']
const DOH_URLS = [
  'https://doh.pub/dns-query',
  'https://dns.alidns.com/dns-query',
  'https://cloudflare-dns.com/dns-query'
]

function NetworkDiagnosticsRow(): React.JSX.Element {
  const { t } = useTranslation()
  const [activePopup, setActivePopup] = useState<'nat' | 'dns' | 'ipv6' | null>(null)
  const [natBusy, setNatBusy] = useState(false)
  const [dnsBusy, setDnsBusy] = useState(false)
  const [ipv6Busy, setIpv6Busy] = useState(false)

  const [stunServer, setStunServer] = useState(STUN_SERVERS[0])
  const [natResult, setNatResult] = useState<{ summary: string; natType: string; localIp: string; publicIp: string; internet: string } | null>(null)

  const [dnsDomain, setDnsDomain] = useState('store.steampowered.com')
  const [dnsServer, setDnsServer] = useState(DNS_SERVERS[0])
  const [dohEnabled, setDohEnabled] = useState(true)
  const [dohUrl, setDohUrl] = useState(DOH_URLS[0])
  const [dnsResult, setDnsResult] = useState<{ latency: string; addresses: string } | null>(null)

  const [ipv6Result, setIpv6Result] = useState<{ supported: boolean; address: string } | null>(null)

  const handleNatDetect = async (): Promise<void> => {
    setNatBusy(true)
    try {
      const result = await optimizationApi.networkDetectNat(stunServer.trim() || STUN_SERVERS[0])
      if (result.error) {
        setNatResult({ summary: result.error, natType: '—', localIp: '—', publicIp: '—', internet: '—' })
      } else {
        const natTypeText = t(`optimization.network.diag.natTypes.${result.natType}`)
        setNatResult({
          summary: natTypeText,
          natType: natTypeText,
          localIp: result.localIp ?? '—',
          publicIp: result.publicIp ?? '—',
          internet: result.internetAvailable
            ? t('optimization.network.diag.internetConnected')
            : t('optimization.network.diag.internetUnreachable')
        })
      }
    } catch (error) {
      setNatResult({ summary: (error as Error).message, natType: '—', localIp: '—', publicIp: '—', internet: '—' })
    } finally {
      setNatBusy(false)
    }
  }

  const handleDnsDetect = async (): Promise<void> => {
    setDnsBusy(true)
    try {
      const result = await optimizationApi.networkDetectDns({
        domain: dnsDomain.trim() || 'store.steampowered.com',
        dnsServer: dnsServer.trim() || undefined,
        dohEnabled,
        dohUrl: dohUrl.trim() || undefined
      })
      if (result.error) {
        setDnsResult({ latency: result.error, addresses: result.error })
      } else if (result.success) {
        setDnsResult({
          latency: t('optimization.network.diag.latencyFormat', { ms: result.elapsedMs }),
          addresses: result.addresses.join(', ') || t('optimization.network.diag.failed')
        })
      } else {
        setDnsResult({ latency: t('optimization.network.diag.failed'), addresses: t('optimization.network.diag.failed') })
      }
    } catch (error) {
      const text = (error as Error).message
      setDnsResult({ latency: text, addresses: text })
    } finally {
      setDnsBusy(false)
    }
  }

  const handleIpv6Detect = async (): Promise<void> => {
    setIpv6Busy(true)
    try {
      const result = await optimizationApi.networkDetectIpv6()
      if (result.error) {
        setIpv6Result({ supported: false, address: result.error })
      } else {
        setIpv6Result({ supported: result.supported, address: result.address ?? '—' })
      }
    } catch (error) {
      setIpv6Result({ supported: false, address: (error as Error).message })
    } finally {
      setIpv6Busy(false)
    }
  }

  return (
    <>
      <div className="udt-network-diag">
        <button type="button" className="udt-network-diag__card" onClick={() => setActivePopup('nat')}>
          <Info24Regular />
          <span className="udt-network-diag__title">{t('optimization.network.diag.natTitle')}</span>
          <span className="udt-network-diag__summary">{natResult?.summary ?? t('optimization.network.diag.unknown')}</span>
        </button>
        <button type="button" className="udt-network-diag__card" onClick={() => setActivePopup('dns')}>
          <Info24Regular />
          <span className="udt-network-diag__title">{t('optimization.network.diag.dnsTitle')}</span>
          <span className="udt-network-diag__summary">{dnsResult?.latency ?? t('optimization.network.diag.unknown')}</span>
        </button>
        <button type="button" className="udt-network-diag__card" onClick={() => setActivePopup('ipv6')}>
          <Info24Regular />
          <span className="udt-network-diag__title">{t('optimization.network.diag.ipv6Title')}</span>
          <span className="udt-network-diag__summary">
            {ipv6Result
              ? ipv6Result.supported
                ? t('optimization.network.diag.ipv6SupportedFull')
                : ipv6Result.address
              : t('optimization.network.diag.unknown')}
          </span>
        </button>
      </div>

      <Modal
        centered
        open={activePopup === 'nat'}
        title={t('optimization.network.diag.natTitle')}
        footer={null}
        onCancel={() => setActivePopup(null)}
      >
        <div className="udt-network-diag-popup">
          <AutoComplete
            value={stunServer}
            options={STUN_SERVERS.map((value) => ({ value }))}
            onChange={setStunServer}
            placeholder="stun.miwifi.com"
          />
          <button type="button" className="udt-btn udt-btn--secondary" disabled={natBusy} onClick={() => void handleNatDetect()}>
            <ArrowSync24Regular /> {t('optimization.network.diag.detect')}
          </button>
          <div className="udt-network-diag-popup__grid">
            <span>{t('optimization.network.diag.natType')}</span>
            <strong>{natResult?.natType ?? t('optimization.network.diag.unknown')}</strong>
            <span>{t('optimization.network.diag.localIp')}</span>
            <strong>{natResult?.localIp ?? '—'}</strong>
            <span>{t('optimization.network.diag.publicIp')}</span>
            <strong>{natResult?.publicIp ?? '—'}</strong>
            <span>{t('optimization.network.diag.internet')}</span>
            <strong>{natResult?.internet ?? '—'}</strong>
          </div>
        </div>
      </Modal>

      <Modal
        centered
        open={activePopup === 'dns'}
        title={t('optimization.network.diag.dnsTitle')}
        footer={null}
        onCancel={() => setActivePopup(null)}
      >
        <div className="udt-network-diag-popup">
          <div className="udt-network-diag-popup__row">
            <span>{t('optimization.network.diag.dnsDomain')}</span>
            <input
              type="text"
              value={dnsDomain}
              onChange={(e) => setDnsDomain(e.target.value)}
              className="udt-network-diag-popup__input"
            />
          </div>
          <div className="udt-network-diag-popup__row">
            <span>{t('optimization.network.diag.customDns')}</span>
            <AutoComplete
              value={dnsServer}
              options={DNS_SERVERS.map((value) => ({ value }))}
              onChange={setDnsServer}
              className="udt-network-diag-popup__input"
            />
          </div>
          <div className="udt-network-diag-popup__row">
            <span>{t('optimization.network.diag.enableDoh')}</span>
            <Switch size="small" checked={dohEnabled} onChange={setDohEnabled} />
          </div>
          <div className="udt-network-diag-popup__row">
            <span>{t('optimization.network.diag.dohUrl')}</span>
            <AutoComplete
              value={dohUrl}
              options={DOH_URLS.map((value) => ({ value }))}
              onChange={setDohUrl}
              className="udt-network-diag-popup__input"
            />
          </div>
          <div className="udt-network-diag-popup__actions">
            <button type="button" className="udt-btn udt-btn--secondary" disabled={dnsBusy} onClick={() => void handleDnsDetect()}>
              <ArrowSync24Regular /> {t('optimization.network.diag.detect')}
            </button>
          </div>
          <div className="udt-network-diag-popup__grid">
            <span>{t('optimization.network.diag.latency')}</span>
            <strong>{dnsResult?.latency ?? '—'}</strong>
            <span>{t('optimization.network.diag.resolvedAddress')}</span>
            <strong className="udt-network-diag-popup__wrap">{dnsResult?.addresses ?? '—'}</strong>
          </div>
        </div>
      </Modal>

      <Modal
        centered
        open={activePopup === 'ipv6'}
        title={t('optimization.network.diag.ipv6Title')}
        footer={null}
        onCancel={() => setActivePopup(null)}
      >
        <div className="udt-network-diag-popup">
          <div className="udt-network-diag-popup__actions">
            <button type="button" className="udt-btn udt-btn--secondary" disabled={ipv6Busy} onClick={() => void handleIpv6Detect()}>
              <ArrowSync24Regular /> {t('optimization.network.diag.detect')}
            </button>
          </div>
          <div className="udt-network-diag-popup__grid">
            <span>{t('optimization.network.diag.ipv6Support')}</span>
            <strong>
              {ipv6Result
                ? ipv6Result.supported
                  ? t('optimization.network.diag.ipv6SupportedFull')
                  : t('optimization.network.diag.notSupported')
                : '—'}
            </strong>
            <span>{t('optimization.network.diag.ipv6Address')}</span>
            <strong>{ipv6Result?.address ?? '—'}</strong>
          </div>
        </div>
      </Modal>
    </>
  )
}

// ── Advanced panel (mode / port / restore) ───────────────────────

function NetworkAdvancedPanel(): React.JSX.Element | null {
  const { t } = useTranslation()
  const networkStatus = useOptimizationStore((s) => s.networkStatus)
  const restoreNetwork = useOptimizationStore((s) => s.restoreNetwork)
  const [expanded, setExpanded] = useState(false)
  const [restoring, setRestoring] = useState(false)

  if (!networkStatus) return null

  const modeText = t(`optimization.network.modeFull.${networkStatus.config.mode}`)

  const handleRestore = async (): Promise<void> => {
    setRestoring(true)
    try {
      const ok = await restoreNetwork()
      if (ok) message.success(t('optimization.network.restored'))
      else reportNetworkStoreError(t, 'optimization.network.saveFailed')
    } finally {
      setRestoring(false)
    }
  }

  return (
    <div className="udt-card">
      <button type="button" className="udt-network-advanced__header" onClick={() => setExpanded(!expanded)}>
        <span className="udt-card__title">{t('optimization.network.advancedHeading')}</span>
        <span className={`udt-category__chevron${expanded ? ' udt-category__chevron--open' : ''}`}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
            <path d="M6 9l6 6 6-6" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </span>
      </button>
      {expanded && (
        <div className="udt-network-advanced__body">
          <div className="udt-card__desc">{t('optimization.network.advancedBody')}</div>
          <div className="udt-network-advanced__text">
            {t('optimization.network.modeLabel')}: {modeText}
          </div>
          <div className="udt-network-advanced__text">
            {t('optimization.network.portFormat', { port: networkStatus.config.listenPort })}
          </div>
          <div className="udt-network-advanced__danger">
            <div className="udt-card__title">{t('optimization.network.dangerZoneHeading')}</div>
            <div className="udt-card__desc">{t('optimization.network.restoreHint')}</div>
            <Popconfirm
              title={t('optimization.network.restoreConfirm')}
              onConfirm={() => void handleRestore()}
            >
              <button type="button" className="udt-btn udt-btn--danger" disabled={restoring}>
                <ArrowUndo24Regular /> {t('optimization.network.restoreNetwork')}
              </button>
            </Popconfirm>
          </div>
        </div>
      )}
    </div>
  )
}

export function NetworkPanels(): React.JSX.Element {
  return (
    <div className="udt-network-panels">
      <NetworkTrafficCard />
      <NetworkTargetsCard />
      <NetworkDiagnosticsRow />
      <NetworkAdvancedPanel />
    </div>
  )
}
