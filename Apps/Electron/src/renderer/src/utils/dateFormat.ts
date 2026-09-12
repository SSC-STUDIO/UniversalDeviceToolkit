import i18n from '../i18n'

/** Formats a date with the active application language rather than the OS locale. */
export function formatDateForUi(date: Date): string {
  const language = i18n.resolvedLanguage ?? i18n.language ?? 'en'
  return date.toLocaleDateString(language)
}
