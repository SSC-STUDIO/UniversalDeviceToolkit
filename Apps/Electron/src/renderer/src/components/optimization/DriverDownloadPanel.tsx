import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateForUi } from '../../utils/dateFormat'
import { Modal, Progress, Select, message } from 'antd'
import {
  ArrowDown24Regular,
  Checkmark24Regular,
  CheckmarkCircle24Filled,
  Clock24Regular,
  ChevronDown24Regular,
  ArrowDownload24Regular,
  EyeOff24Regular,
  Eye24Regular,
  FolderOpen24Regular,
  MoreVertical24Regular,
  PauseCircle24Regular,
  PlayCircle24Regular,
  BookOpen24Regular,
  Search24Regular,
  Star24Filled,
  Stop24Regular,
  Warning24Regular
} from '../icons/fluent'
import {
  optimizationApi,
  type DriverPackageDefinition,
  type DriverRebootType,
  type DriverSortMode,
  type DriverSourceType
} from '../../api/optimization'
import { useDriverStore } from '../../stores/driverStore'
import './optimization.css'

const SORT_OPTIONS: { value: DriverSortMode; i18nKey: string }[] = [
  { value: 'name', i18nKey: 'optimization.driver.sort.name' },
  { value: 'category', i18nKey: 'optimization.driver.sort.category' },
  { value: 'date', i18nKey: 'optimization.driver.sort.date' }
]

const OS_DISPLAY_KEYS: Record<string, string> = {
  Windows10: 'optimization.driver.osOptions.windows10',
  Windows11: 'optimization.driver.osOptions.windows11',
  Windows8: 'optimization.driver.osOptions.windows8',
  Windows7: 'optimization.driver.osOptions.windows7'
}

function formatDate(iso: string | null): string {
  if (!iso) return '—'
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso.slice(0, 10)
  return formatDateForUi(date)
}

function isRunning(packageItem: DriverPackageDefinition): boolean {
  return packageItem.status === 'Downloading' || packageItem.status === 'Installing'
}

// Row actions resolve the store at call time, so they stay referentially
// stable across renders and memoized PackageCard rows skip re-renders while
// the 2s download poll ticks.
function toggleSelectedPackage(id: string): void {
  useDriverStore.getState().toggleSelected(id)
}

function downloadPackage(id: string): void {
  void useDriverStore.getState().startPackage(id)
}

function installPackage(id: string): void {
  void useDriverStore.getState().installPackage(id)
}

function pausePackage(id: string): void {
  void useDriverStore.getState().pausePackage(id)
}

function hidePackage(id: string): void {
  void useDriverStore.getState().hidePackages([id])
}

function openPackageReadme(url: string): void {
  void optimizationApi.openUrl(url)
}

const PackageCard = memo(function PackageCard({
  packageItem,
  selected,
  onToggle,
  onDownload,
  onInstall,
  onPause,
  onHide,
  onHideAll,
  onOpenReadme
}: {
  packageItem: DriverPackageDefinition
  selected: boolean
  onToggle: (id: string) => void
  onDownload: (id: string) => void
  onInstall: (id: string) => void
  onPause: (id: string) => void
  onHide: (id: string) => void
  onHideAll: () => void
  onOpenReadme: (url: string) => void
}): React.JSX.Element {
  const { t } = useTranslation()
  const [menuOpen, setMenuOpen] = useState(false)
  const completed = packageItem.status === 'Completed'
  const running = isRunning(packageItem)

  const status = packageItem.status === 'NotStarted' && selected ? 'Queued' : packageItem.status
  const statusText = t(`optimization.driver.status.${status}`)
  const showRecommended = (packageItem.isRecommended || packageItem.isUpdate) && !completed

  const isOld = (() => {
    if (!packageItem.releaseDate) return false
    const date = new Date(packageItem.releaseDate)
    return !Number.isNaN(date.getTime()) && date.getTime() < Date.now() - 365 * 24 * 60 * 60 * 1000
  })()

  const rebootKey: Record<DriverRebootType, string | null> = {
    None: null,
    Delayed: 'optimization.driver.reboot.recommended',
    Requested: 'optimization.driver.reboot.recommended',
    Forced: 'optimization.driver.reboot.required',
    ForcedPowerOff: 'optimization.driver.reboot.shutdown'
  }

  const detailParts = [packageItem.version, packageItem.fileSize, packageItem.fileName].filter(
    (part): part is string => Boolean(part)
  )

  return (
    <div className={`udt-driver-card${selected ? ' udt-driver-card--selected' : ''}`}>
      <label className="udt-checkbox udt-driver-card__checkbox">
        <input type="checkbox" checked={selected} onChange={() => onToggle(packageItem.id)} />
        <span className="udt-checkbox__box">
          <Checkmark24Regular />
        </span>
      </label>

      <div className="udt-driver-card__main">
        <div className="udt-driver-card__title-row">
          <span
            className="udt-driver-card__title"
            title={packageItem.title}
            onContextMenu={(e) => {
              e.preventDefault()
              void navigator.clipboard.writeText(packageItem.title)
              message.success(t('common.copied'))
            }}
          >
            {packageItem.title}
          </span>
          {showRecommended && (
            <span className="udt-badge udt-badge--success">
              <Star24Filled /> {t('optimization.driver.recommended')}
            </span>
          )}
          {statusText && (
            <span className={`udt-driver-card__status udt-driver-card__status--${status}`}>
              {status === 'Completed' && <CheckmarkCircle24Filled />}
              {status === 'Queued' && <Clock24Regular />}
              {status === 'Downloading' && <ArrowDownload24Regular />}
              {status === 'Installing' && <PlayCircle24Regular />}
              {status === 'Error' && <Warning24Regular />}
              {statusText}
            </span>
          )}
        </div>

        <div className="udt-driver-card__meta">
          <span>{packageItem.category}</span>
          {detailParts.length > 0 && <span>|</span>}
          <span>{formatDate(packageItem.releaseDate)}</span>
        </div>

        {packageItem.description && (
          <div className="udt-driver-card__description">{packageItem.description}</div>
        )}

        {detailParts.length > 0 && (
          <div className="udt-driver-card__detail" title={detailParts.join('  |  ')}>
            {detailParts.join('  |  ')}
          </div>
        )}

        {(packageItem.isUpdate || rebootKey[packageItem.reboot]) && (
          <div className="udt-driver-card__flags">
            {packageItem.isUpdate && (
              <span className="udt-driver-card__flag udt-driver-card__flag--update">
                <ArrowDown24Regular /> {t('optimization.driver.isUpdate')}
              </span>
            )}
            {rebootKey[packageItem.reboot] && (
              <span className="udt-driver-card__flag udt-driver-card__flag--warning">
                <Warning24Regular /> {t(rebootKey[packageItem.reboot]!)}
              </span>
            )}
          </div>
        )}

        {isOld && (
          <div className="udt-driver-card__warning">
            <Warning24Regular /> {t('optimization.driver.oldPackageWarning')}
          </div>
        )}

        {packageItem.error && (
          <div className="udt-driver-card__error">{packageItem.error}</div>
        )}
      </div>

      <div className="udt-driver-card__actions">
        {running ? (
          <>
            <span className="udt-driver-card__progress-label">
              {Math.round(packageItem.progress * 100)}%
            </span>
            <Progress
              type="circle"
              size={26}
              percent={Math.round(packageItem.progress * 100)}
              strokeWidth={12}
              showInfo={false}
            />
            <button
              type="button"
              className="udt-action-btn"
              title={t('optimization.driver.pause')}
              onClick={() => onPause(packageItem.id)}
            >
              <Stop24Regular />
            </button>
          </>
        ) : completed ? (
          <span
            className="udt-driver-card__status udt-driver-card__status--Completed"
            title={t('optimization.driver.status.Completed')}
          >
            <CheckmarkCircle24Filled />
          </span>
        ) : (
          <>
            {packageItem.readmeUrl && (
              <button
                type="button"
                className="udt-action-btn"
                title={t('optimization.driver.openReadme')}
                onClick={() => onOpenReadme(packageItem.readmeUrl!)}
              >
                <BookOpen24Regular />
              </button>
            )}
            <button
              type="button"
              className="udt-action-btn"
              title={t('optimization.driver.download')}
              onClick={() => onDownload(packageItem.id)}
            >
              <ArrowDownload24Regular />
            </button>
          </>
        )}

        <div className="udt-driver-card__menu">
          <button
            type="button"
            className="udt-action-btn"
            title={t('common.moreActions')}
            onClick={() => setMenuOpen(!menuOpen)}
          >
            <MoreVertical24Regular />
          </button>
          {menuOpen && (
            <>
              <div className="udt-driver-card__menu-backdrop" onClick={() => setMenuOpen(false)} />
              <div className="udt-driver-card__menu-popup">
                <button
                  type="button"
                  onClick={() => {
                    setMenuOpen(false)
                    onInstall(packageItem.id)
                  }}
                >
                  <PlayCircle24Regular /> {t('optimization.driver.install')}
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setMenuOpen(false)
                    onHide(packageItem.id)
                  }}
                >
                  <EyeOff24Regular /> {t('optimization.driver.hide')}
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setMenuOpen(false)
                    onHideAll()
                  }}
                >
                  <EyeOff24Regular /> {t('optimization.driver.hideAll')}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
})

function DriverFilterBar({
  filterText,
  onFilterTextChange,
  onlyShowUpdates,
  onOnlyShowUpdatesChange,
  sortMode,
  onSortModeChange
}: {
  filterText: string
  onFilterTextChange: (value: string) => void
  onlyShowUpdates: boolean
  onOnlyShowUpdatesChange: (value: boolean) => void
  sortMode: DriverSortMode
  onSortModeChange: (value: DriverSortMode) => void
}): React.JSX.Element {
  const { t } = useTranslation()
  return (
    <div className="udt-driver-toolbar">
      <div className="udt-driver-toolbar__search">
        <Search24Regular />
        <input
          type="text"
          placeholder={t('optimization.driver.filter')}
          value={filterText}
          onChange={(e) => onFilterTextChange(e.target.value)}
        />
      </div>
      <label className="udt-checkbox udt-driver-toolbar__updates">
        <input
          type="checkbox"
          checked={onlyShowUpdates}
          onChange={(e) => onOnlyShowUpdatesChange(e.target.checked)}
        />
        <span className="udt-checkbox__box">
          <Checkmark24Regular />
        </span>
        <span>{t('optimization.driver.onlyShowUpdates')}</span>
      </label>
      <Select<DriverSortMode>
        className="udt-driver-select"
        value={sortMode}
        onChange={onSortModeChange}
        options={SORT_OPTIONS.map((option) => ({
          value: option.value,
          label: t(option.i18nKey)
        }))}
      />
    </div>
  )
}

export default function DriverDownloadPanel(): React.JSX.Element {
  const { t } = useTranslation()
  const settings = useDriverStore((s) => s.settings)
  const packages = useDriverStore((s) => s.packages)
  const selectedIds = useDriverStore((s) => s.selectedIds)
  const loadingSettings = useDriverStore((s) => s.loadingSettings)
  const scanning = useDriverStore((s) => s.scanning)
  const error = useDriverStore((s) => s.error)
  const isAnyRunning = useDriverStore((s) => s.isAnyRunning)

  const [machineType, setMachineType] = useState('')
  const [os, setOs] = useState('')
  const [source, setSource] = useState<DriverSourceType>('Vantage')
  const [downloadPath, setDownloadPath] = useState('')
  const [filterText, setFilterText] = useState('')
  const [onlyShowUpdates, setOnlyShowUpdates] = useState(false)
  const [sortMode, setSortMode] = useState<DriverSortMode>('date')
  const [confirmScanOpen, setConfirmScanOpen] = useState(false)

  const pathDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => {
    return () => {
      if (pathDebounceRef.current) clearTimeout(pathDebounceRef.current)
    }
  }, [])

  useEffect(() => {
    if (!loadingSettings && !settings) void useDriverStore.getState().loadSettings()
  }, [loadingSettings, settings])

  useEffect(() => {
    if (!settings) return
    setMachineType(settings.machineType)
    setOs(settings.os)
    setDownloadPath(settings.downloadPath)
    setOnlyShowUpdates(settings.onlyShowUpdates)
  }, [settings])

  useEffect(() => {
    const interval = setInterval(() => {
      if (isAnyRunning) void useDriverStore.getState().pollStatuses()
    }, 2000)
    return () => clearInterval(interval)
  }, [isAnyRunning])

  const hiddenIds = settings?.hiddenPackageIds ?? []

  const visiblePackages = useMemo(() => {
    const query = filterText.trim().toLowerCase()
    const sorted = [...packages].sort((a, b) => {
      if (sortMode === 'name') return a.title.localeCompare(b.title)
      if (sortMode === 'category') return a.category.localeCompare(b.category)
      const da = a.releaseDate ? new Date(a.releaseDate).getTime() : 0
      const db = b.releaseDate ? new Date(b.releaseDate).getTime() : 0
      return db - da
    })
    return sorted.filter((p) => {
      if (hiddenIds.includes(p.id)) return false
      if (onlyShowUpdates && !p.isUpdate) return false
      if (query && !(p.index ?? '').toLowerCase().includes(query) && !p.title.toLowerCase().includes(query)) return false
      return true
    })
  }, [packages, filterText, onlyShowUpdates, sortMode, hiddenIds])

  const hasHidden = hiddenIds.length > 0
  const hasScanned = packages.length > 0 || scanning

  const handleScan = async (): Promise<void> => {
    const trimmed = machineType.trim()
    if (trimmed.length !== 4 || !os) {
      message.warning(t('optimization.driver.scanValidation'))
      return
    }
    const ok = await useDriverStore.getState().scan({ machineType: trimmed, os, source })
    if (ok) {
      const count = useDriverStore.getState().packages.length
      message.success(
        count === 1
          ? t('optimization.driver.packagesFoundOne')
          : t('optimization.driver.packagesFound', { count })
      )
    }
  }

  const handleScanClicked = (): void => {
    if (isAnyRunning) setConfirmScanOpen(true)
    else void handleScan()
  }

  const handleBrowsePath = async (): Promise<void> => {
    const path = await optimizationApi.selectFolder()
    if (!path) return
    setDownloadPath(path)
    void useDriverStore.getState().setDownloadPath(path)
  }

  const handlePathChange = (value: string): void => {
    setDownloadPath(value)
    if (pathDebounceRef.current) clearTimeout(pathDebounceRef.current)
    pathDebounceRef.current = setTimeout(() => {
      void useDriverStore.getState().setDownloadPath(value)
    }, 400)
  }

  const handleOpenPath = async (): Promise<void> => {
    if (downloadPath) await optimizationApi.openPath(downloadPath)
  }

  const handleStartAll = async (): Promise<void> => {
    if (isAnyRunning) await useDriverStore.getState().pauseSelected()
    else await useDriverStore.getState().startSelected()
  }

  const handleHideAll = useCallback((): void => {
    void useDriverStore.getState().hidePackages(visiblePackages.map((p) => p.id))
  }, [visiblePackages])

  const handleShowHidden = (): void => {
    void useDriverStore.getState().showHiddenPackages()
  }

  const emptyState = (() => {
    if (loadingSettings || scanning) return null
    if (packages.length === 0) {
      return {
        title: t('optimization.driver.empty.notScanned.title'),
        message: t('optimization.driver.empty.notScanned.message')
      }
    }
    if (visiblePackages.length === 0) {
      return {
        title: t('optimization.driver.empty.noFilterResults.title'),
        message: t('optimization.driver.empty.noFilterResults.message')
      }
    }
    return null
  })()

  return (
    <div className="udt-driver-panel">
      <div className="udt-driver-filter">
        <div className="udt-driver-filter__fields">
          <div className="udt-driver-field">
            <label>{t('optimization.driver.machineType')}</label>
            <input
              type="text"
              value={machineType}
              maxLength={16}
              placeholder={t('optimization.driver.machineTypePlaceholder')}
              onChange={(e) => setMachineType(e.target.value)}
            />
          </div>
          <div className="udt-driver-field">
            <label>{t('optimization.driver.os')}</label>
            <Select
              aria-label={t('optimization.driver.os')}
              className="udt-driver-select"
              value={os || undefined}
              placeholder="—"
              onChange={(value) => setOs(value ?? '')}
              options={(settings?.osOptions ?? []).map((option) => ({
                value: option,
                label: OS_DISPLAY_KEYS[option] ? t(OS_DISPLAY_KEYS[option]) : option
              }))}
            />
          </div>
          <div className="udt-driver-field">
            <label>{t('optimization.driver.downloadTo')}</label>
            <div className="udt-driver-field__path">
              <input
                type="text"
                value={downloadPath}
                placeholder={t('optimization.driver.downloadToPlaceholder')}
                onChange={(e) => handlePathChange(e.target.value)}
              />
              <button
                type="button"
                className="udt-action-btn"
                title={t('optimization.driver.browse')}
                onClick={() => void handleBrowsePath()}
              >
                <FolderOpen24Regular />
              </button>
              <button
                type="button"
                className="udt-action-btn"
                title={t('optimization.driver.openDownloadTo')}
                onClick={() => void handleOpenPath()}
              >
                <Eye24Regular />
              </button>
            </div>
          </div>
        </div>

        <div className="udt-driver-source">
          <div className="udt-driver-source__title">{t('optimization.driver.source')}</div>
          <label className="udt-driver-source__option">
            <input
              type="radio"
              name="driverSource"
              checked={source === 'Vantage'}
              onChange={() => setSource('Vantage')}
            />
            <span>
              <span className="udt-driver-source__name">{t('optimization.driver.primarySource')}</span>
              <span className="udt-driver-source__message">{t('optimization.driver.primarySourceMessage')}</span>
            </span>
          </label>
          <label className="udt-driver-source__option">
            <input
              type="radio"
              name="driverSource"
              checked={source === 'PCSupport'}
              onChange={() => setSource('PCSupport')}
            />
            <span>
              <span className="udt-driver-source__name">{t('optimization.driver.secondarySource')}</span>
              <span className="udt-driver-source__message">{t('optimization.driver.secondarySourceMessage')}</span>
            </span>
          </label>
          <button type="button" className="udt-btn udt-btn--primary" disabled={scanning} onClick={handleScanClicked}>
            <Search24Regular /> {scanning ? t('optimization.driver.scanning') : t('optimization.driver.scan')}
          </button>
        </div>
      </div>

      {hasScanned && (
        <div className="udt-driver-info">
          {t('optimization.driver.disclaimer')}
        </div>
      )}

      {scanning && (
        <div className="udt-driver-skeleton" role="status" aria-label={t('common.loading', { defaultValue: 'Loading…' })}>
          {Array.from({ length: 3 }, (_, index) => (
            <div key={index} className="udt-driver-skeleton__card">
              <div
                className="udt-skeleton udt-driver-skeleton__check"
                style={{ ['--udt-shimmer-delay' as string]: `${-Math.min(index * 0.055, 0.32)}s` }}
              />
              <div className="udt-driver-skeleton__main">
                <div className="udt-driver-skeleton__row">
                  <div
                    className="udt-skeleton"
                    style={{
                      width: '45%',
                      height: 15,
                      borderRadius: 'var(--udt-radius-small)',
                      ['--udt-shimmer-delay' as string]: `${-Math.min(index * 0.055, 0.32)}s`
                    }}
                  />
                  <div
                    className="udt-skeleton"
                    style={{
                      width: 64,
                      height: 18,
                      borderRadius: 999,
                      ['--udt-shimmer-delay' as string]: `${-Math.min(index * 0.055 + 0.055, 0.32)}s`
                    }}
                  />
                  <div
                    className="udt-skeleton"
                    style={{
                      width: 88,
                      height: 13,
                      borderRadius: 'var(--udt-radius-small)',
                      ['--udt-shimmer-delay' as string]: `${-Math.min(index * 0.055 + 0.11, 0.32)}s`
                    }}
                  />
                </div>
                <div
                  className="udt-skeleton"
                  style={{
                    width: '32%',
                    height: 12,
                    marginTop: 8,
                    borderRadius: 'var(--udt-radius-small)',
                    ['--udt-shimmer-delay' as string]: `${-Math.min(index * 0.055 + 0.165, 0.32)}s`
                  }}
                />
                <div
                  className="udt-skeleton"
                  style={{
                    width: '68%',
                    height: 12,
                    marginTop: 8,
                    borderRadius: 'var(--udt-radius-small)',
                    ['--udt-shimmer-delay' as string]: `${-Math.min(index * 0.055 + 0.22, 0.32)}s`
                  }}
                />
              </div>
              <div
                className="udt-skeleton udt-driver-skeleton__action"
                style={{ ['--udt-shimmer-delay' as string]: `${-Math.min(index * 0.055 + 0.11, 0.32)}s` }}
              />
            </div>
          ))}
        </div>
      )}

      {!scanning && hasScanned && packages.length > 0 && (
        <>
          <DriverFilterBar
            filterText={filterText}
            onFilterTextChange={setFilterText}
            onlyShowUpdates={onlyShowUpdates}
            onOnlyShowUpdatesChange={(value) => {
              setOnlyShowUpdates(value)
              void useDriverStore.getState().setOnlyShowUpdates(value)
            }}
            sortMode={sortMode}
            onSortModeChange={setSortMode}
          />
          <div className="udt-driver-toolbar__actions">
            <button
              type="button"
              className="udt-btn udt-btn--secondary"
              onClick={() => void useDriverStore.getState().selectRecommended()}
            >
              <Star24Filled /> {t('optimization.driver.selectRecommended')}
            </button>
            <button
              type="button"
              className="udt-btn udt-btn--secondary"
              disabled={selectedIds.length === 0}
              onClick={() => void handleStartAll()}
            >
              {isAnyRunning ? <PauseCircle24Regular /> : <PlayCircle24Regular />}
              {isAnyRunning ? t('optimization.driver.pauseAll') : t('optimization.driver.startAll')}
            </button>
            <button
              type="button"
              className="udt-btn udt-btn--secondary"
              disabled={selectedIds.length === 0}
              onClick={() => void useDriverStore.getState().clearSelection()}
            >
              {t('optimization.driver.clearSelection')}
            </button>
          </div>
        </>
      )}

      {!scanning && emptyState && (
        <div className="udt-empty udt-driver-empty">
          <Search24Regular className="udt-empty__icon" />
          <div className="udt-empty__title">{emptyState.title}</div>
          <div className="udt-empty__description">{emptyState.message}</div>
        </div>
      )}

      {!scanning && hasHidden && (
        <button type="button" className="udt-link-button udt-driver-show-hidden" onClick={handleShowHidden}>
          <Eye24Regular /> {t('optimization.driver.showHiddenDownloads')}
        </button>
      )}

      {!scanning && visiblePackages.length > 0 && (
        <div className="udt-driver-list">
          {visiblePackages.map((packageItem) => (
            <PackageCard
              key={packageItem.id}
              packageItem={packageItem}
              selected={selectedIds.includes(packageItem.id)}
              onToggle={toggleSelectedPackage}
              onDownload={downloadPackage}
              onInstall={installPackage}
              onPause={pausePackage}
              onHide={hidePackage}
              onHideAll={handleHideAll}
              onOpenReadme={openPackageReadme}
            />
          ))}
        </div>
      )}

      {error && <div className="udt-page-error">{error}</div>}

      <Modal
        centered
        open={confirmScanOpen}
        title={t('optimization.driver.downloadInProgress.title')}
        okText={t('optimization.driver.downloadInProgress.confirm')}
        cancelText={t('common.cancel')}
        onOk={() => {
          setConfirmScanOpen(false)
          void handleScan()
        }}
        onCancel={() => setConfirmScanOpen(false)}
      >
        <div className="udt-driver-modal-body">
          <ChevronDown24Regular className="udt-driver-modal-body__icon" />
          <span>{t('optimization.driver.downloadInProgress.message')}</span>
        </div>
      </Modal>
    </div>
  )
}
