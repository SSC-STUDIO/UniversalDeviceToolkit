import type { TFunction } from 'i18next'

/** Standard desktop notification positions (corners and top/bottom edges). */
export const NOTIFICATION_POSITIONS = [
  'BottomRight',
  'TopRight',
  'TopCenter',
  'BottomCenter'
] as const

export const NOTIFICATION_DURATIONS = ['Short', 'Normal', 'Long'] as const

export type NotificationPosition = (typeof NOTIFICATION_POSITIONS)[number]
export type NotificationDuration = (typeof NOTIFICATION_DURATIONS)[number]

export function sanitizeNotificationPosition(raw?: unknown): NotificationPosition {
  if (typeof raw !== 'string') return 'BottomRight'
  if ((NOTIFICATION_POSITIONS as readonly string[]).includes(raw)) {
    return raw as NotificationPosition
  }
  // Remap deprecated positions to standard desktop corner/edge equivalents
  if (raw === 'TopLeft') return 'TopRight'
  if (raw === 'BottomLeft' || raw === 'CenterLeft' || raw === 'CenterRight' || raw === 'Center') {
    return 'BottomRight'
  }
  return 'BottomRight'
}

function positionI18nKey(value: string): string {
  const lower = value.charAt(0).toLowerCase() + value.slice(1)
  return `settings.display.notificationPositions.${lower}`
}

function durationI18nKey(value: string): string {
  const lower = value.charAt(0).toLowerCase() + value.slice(1)
  return `settings.display.notificationDurations.${lower}`
}

export function buildNotificationPositionOptions(t: TFunction): Array<{ value: string; label: string }> {
  return NOTIFICATION_POSITIONS.map((value) => ({
    value,
    label: t(positionI18nKey(value))
  }))
}

export function buildNotificationDurationOptions(t: TFunction): Array<{ value: string; label: string }> {
  return NOTIFICATION_DURATIONS.map((value) => ({
    value,
    label: t(durationI18nKey(value))
  }))
}
