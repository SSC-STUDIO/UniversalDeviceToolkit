import { Select } from 'antd'
import { useTranslation } from 'react-i18next'
import {
  applyWindowBackdrop,
  normalizeWindowBackdropStyle,
  type WindowBackdropStyle
} from '../../theme/windowBackdrop'
import { SettingsCard } from './SettingsCard'

interface WindowBackdropSettingProps {
  application: Record<string, unknown>
  persist: (patch: Record<string, unknown>) => void
  disabled?: boolean
}

const PLATFORM: string = window.bridge?.platform ?? 'win32'

/** Native mica is Windows-only; Linux still offers Windows/macOS as opaque approximations. */
const OPTIONS: WindowBackdropStyle[] =
  PLATFORM === 'darwin' ? ['macOS', 'Off'] : ['Windows', 'macOS', 'Off']

export default function WindowBackdropSetting({
  application,
  persist,
  disabled = false
}: WindowBackdropSettingProps): React.JSX.Element {
  const { t } = useTranslation()
  const labels: Record<WindowBackdropStyle, string> = {
    Windows: t('wpf.settingsPagewindowBackdropmica'),
    macOS: t('wpf.settingsPagewindowBackdropacrylic'),
    Off: t('wpf.settingsPagewindowBackdropoff')
  }
  const rawStyle = normalizeWindowBackdropStyle(application['WindowBackdropStyle'])
  const style: WindowBackdropStyle = OPTIONS.includes(rawStyle) ? rawStyle : OPTIONS[0]

  return (
    <SettingsCard
      title={t('wpf.settingsPagewindowBackdroptitle')}
      description={t('wpf.settingsPagewindowBackdropmessage')}
      action={
        <Select<WindowBackdropStyle>
          className="udt-settings-select"
          value={style}
          disabled={disabled}
          onChange={(value) => {
            applyWindowBackdrop(value)
            persist({ WindowBackdropStyle: value })
          }}
          options={OPTIONS.map((value) => ({ value, label: labels[value] }))}
        />
      }
    />
  )
}
