import { Hourglass24Regular } from '../icons/fluent'
import { useTranslation } from 'react-i18next'

/**
 * Macro recording status window — port of the Electron MacroRecordingWindow
 * (FluentWindow: hourglass "Recording will start in 3 seconds..." while
 * preparing, record dot + "Press ESC to stop." while recording). Like the
 * Electron window it is a small floating card that does not block the page.
 */
export interface MacroRecordingModalProps {
  preparing: boolean
}

export default function MacroRecordingModal({
  preparing
}: MacroRecordingModalProps): React.JSX.Element {
  const { t } = useTranslation()
  return (
    <div className="udt-macro-recording">
      <div className="udt-macro-recording__window">
        <span className="udt-macro-recording__icon">
          {preparing ? (
            <Hourglass24Regular />
          ) : (
            <span className="udt-macro-recording__dot" aria-hidden="true" />
          )}
        </span>
        <div className="udt-macro-recording__copy">
          <div className="udt-macro-recording__title">
            {preparing ? t('macro.recording.preparing') : t('macro.recording.title')}
          </div>
          {!preparing && (
            <div className="udt-macro-recording__subtitle">
              {t('macro.recording.pressEscToStop')}
            </div>
          )}
          {!preparing && (
            <div className="udt-macro-recording__hint">{t('macro.recording.focusHint')}</div>
          )}
        </div>
      </div>
    </div>
  )
}
