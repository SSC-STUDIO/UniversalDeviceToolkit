import { useCallback, useEffect, useState } from 'react'
import { FolderOpen24Regular, Link24Regular } from '../components/icons/fluent'
import { useTranslation } from 'react-i18next'
import { invoke } from '../api/bridge'
import { openExternalUrl } from '../utils/url'
import appMarkUrl from '../assets/app-mark.png'
import './pages.css'

interface AppStatus {
  pid?: number
  version?: string
  build?: string
  logPath?: string
}

interface ThirdPartyLibrary {
  name: string
  url: string
}

const PROJECT_URL = 'https://github.com/SSC-STUDIO/UniversalDeviceToolkit'
const LATEST_RELEASE_URL = `${PROJECT_URL}/releases/latest`

const THIRD_PARTY_LIBRARIES: ThirdPartyLibrary[] = [
  { name: 'Electron', url: 'https://github.com/electron/electron' },
  { name: 'React', url: 'https://react.dev' },
  { name: 'Ant Design', url: 'https://github.com/ant-design/ant-design' },
  { name: 'ECharts', url: 'https://github.com/apache/echarts' },
  { name: 'Zustand', url: 'https://github.com/pmndrs/zustand' },
  { name: 'i18next', url: 'https://github.com/i18next/i18next' },
  { name: 'React Router', url: 'https://github.com/remix-run/react-router' },
  { name: 'Fluent UI System Icons', url: 'https://github.com/microsoft/fluentui-system-icons' },
  { name: 'TypeScript', url: 'https://github.com/microsoft/TypeScript' },
  { name: 'Vite', url: 'https://vite.dev' },
  { name: 'electron-vite', url: 'https://electron-vite.org' },
  { name: 'electron-builder', url: 'https://github.com/electron-userland/electron-builder' },
  { name: 'react-i18next', url: 'https://github.com/i18next/react-i18next' },
  { name: 'echarts-for-react', url: 'https://github.com/hustcc/echarts-for-react' },
  { name: 'esbuild', url: 'https://github.com/evanw/esbuild' },
  { name: 'ESLint', url: 'https://github.com/eslint/eslint' }
]

function AboutHeroPattern(): React.JSX.Element {
  return (
    <svg
      className="udt-about-page__pattern"
      viewBox="0 0 720 280"
      preserveAspectRatio="xMaxYMid slice"
      aria-hidden="true"
    >
      <g fill="none" stroke="currentColor" strokeLinecap="round">
        <path d="M 430 36 A 122 122 0 0 1 430 248" strokeWidth="22" opacity="0.34" />
        <path d="M 412 58 A 96 96 0 0 1 412 226" strokeWidth="10" opacity="0.22" />
        <path d="M 398 78 A 74 74 0 0 1 398 206" strokeWidth="4" opacity="0.16" />
      </g>
      <g fill="currentColor">
        <rect x="548" y="28" width="108" height="20" rx="4" transform="rotate(38 548 28)" opacity="0.28" />
        <rect x="586" y="86" width="78" height="14" rx="3" transform="rotate(38 586 86)" opacity="0.18" />
        <rect x="402" y="42" width="26" height="9" rx="1" opacity="0.2" />
        <circle cx="508" cy="58" r="3.5" opacity="0.28" />
        <circle cx="534" cy="96" r="2.5" opacity="0.2" />
        <circle cx="560" cy="138" r="2.5" opacity="0.16" />
        <circle cx="586" cy="178" r="2" opacity="0.14" />
        <circle cx="612" cy="216" r="2" opacity="0.12" />
      </g>
    </svg>
  )
}

export default function AboutPage(): React.JSX.Element {
  const { t, i18n } = useTranslation()
  const [appStatus, setAppStatus] = useState<AppStatus | null>(null)
  const [statusError, setStatusError] = useState(false)

  const loadAppStatus = useCallback((): void => {
    void invoke<AppStatus>('app.getStatus')
      .then((status) => {
        setStatusError(false)
        setAppStatus(status)
      })
      .catch(() => {
        setStatusError(true)
      })
  }, [])

  useEffect(() => {
    loadAppStatus()
  }, [loadAppStatus])

  const version = statusError
    ? t('common.error')
    : (appStatus?.version ?? t('common.loading'))
  const build = appStatus?.build?.trim() ?? ''
  const isEnglish = i18n.language.toLowerCase().startsWith('en')

  const openApplicationDataFolder = (): void => {
    void window.bridge?.openAppFolder?.('data')
  }

  const openApplicationTempFolder = (): void => {
    void window.bridge?.openLogFolder?.()
  }

  const handleExternalLink = (url: string) => (event: React.MouseEvent<HTMLAnchorElement>): void => {
    event.preventDefault()
    void openExternalUrl(url)
  }

  return (
    <div className="udt-about-page udt-content-column udt-content-fill">
      <section className="udt-about-page__hero">
        <AboutHeroPattern />
        <div className="udt-about-page__brand">
          <img className="udt-about-page__mark" src={appMarkUrl} alt="" width={96} height={96} />
          <div className="udt-about-page__identity">
            <h1 className="udt-page-title">{t('about.title')}</h1>
            <p className="udt-about-page__app-name">{t('app.name')}</p>
            <div className="udt-about-page__badges">
              <span className="udt-about-page__badge" aria-live="polite">
                {t('about.version')} {version}
              </span>
              {build !== '' && (
                <span className="udt-about-page__badge">
                  {t('about.build')} {build}
                </span>
              )}
              {statusError && (
                <button type="button" className="udt-link-button" onClick={loadAppStatus}>
                  {t('common.retry')}
                </button>
              )}
            </div>
            <p className="udt-about-page__copyright">
              {t('about.copyright')} © SSC-STUDIO
            </p>
            {!isEnglish && (
              <p className="udt-about-page__credit">
                {t('about.translationCredit', 'Translated by Karl Lee. 由凌卡Karl汉化，开源社区校正。')}
              </p>
            )}
          </div>
        </div>
      </section>

      <div className="udt-about-page__grid">
        <section className="udt-about-page__card udt-about-page__card--links">
          <h2 className="udt-subsection-title">{t('about.links')}</h2>
          <a
            className="udt-about-page__tile"
            href={PROJECT_URL}
            target="_blank"
            rel="noreferrer"
            onClick={handleExternalLink(PROJECT_URL)}
          >
            <span className="udt-about-page__tile-icon">
              <Link24Regular />
            </span>
            {t('about.projectWebsite')}
          </a>
          <a
            className="udt-about-page__tile"
            href={LATEST_RELEASE_URL}
            target="_blank"
            rel="noreferrer"
            onClick={handleExternalLink(LATEST_RELEASE_URL)}
          >
            <span className="udt-about-page__tile-icon">
              <Link24Regular />
            </span>
            {t('about.latestRelease')}
          </a>
        </section>

        <section className="udt-about-page__card udt-about-page__card--libs">
          <h2 className="udt-subsection-title">{t('about.thirdParty')}</h2>
          <div className="udt-about-page__chips">
            {THIRD_PARTY_LIBRARIES.map((lib) => (
              <a
                key={lib.name}
                className="udt-about-page__chip"
                href={lib.url}
                target="_blank"
                rel="noreferrer"
                onClick={handleExternalLink(lib.url)}
              >
                {lib.name}
              </a>
            ))}
          </div>
        </section>

        <section className="udt-about-page__card udt-about-page__card--folders">
          <h2 className="udt-subsection-title">{t('about.applicationFolders')}</h2>
          <button type="button" className="udt-about-page__tile" onClick={openApplicationDataFolder}>
            <span className="udt-about-page__tile-icon">
              <FolderOpen24Regular />
            </span>
            {t('about.data')}
          </button>
          <button type="button" className="udt-about-page__tile" onClick={openApplicationTempFolder}>
            <span className="udt-about-page__tile-icon">
              <FolderOpen24Regular />
            </span>
            {t('about.temp')}
          </button>
        </section>
      </div>
    </div>
  )
}
