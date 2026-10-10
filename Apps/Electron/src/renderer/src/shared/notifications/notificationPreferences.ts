import { useSettingsStore } from '../settings/settingsStore'

/** Electron SystemSounds.Asterisk equivalent — short two-tone beep. */
export function playNotificationSound(): void {
  try {
    const AudioContextClass = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (AudioContextClass === undefined) return
    const context = new AudioContextClass()
    const now = context.currentTime
    const playTone = (frequency: number, start: number, duration: number): void => {
      const oscillator = context.createOscillator()
      const gain = context.createGain()
      oscillator.type = 'sine'
      oscillator.frequency.value = frequency
      gain.gain.setValueAtTime(0.08, start)
      gain.gain.exponentialRampToValueAtTime(0.001, start + duration)
      oscillator.connect(gain)
      gain.connect(context.destination)
      oscillator.start(start)
      oscillator.stop(start + duration)
    }
    playTone(880, now, 0.12)
    playTone(1320, now + 0.1, 0.16)
    window.setTimeout(() => {
      void context.close()
    }, 400)
  } catch {
    // Best-effort sound playback.
  }
}

export function readApplicationScope(): Record<string, unknown> {
  const scopes = useSettingsStore.getState().scopes
  return typeof scopes.application === 'object' && scopes.application !== null
    ? (scopes.application as Record<string, unknown>)
    : {}
}

/** Suppression + sound settings (Electron AppNotificationHost.ShouldSuppress/TryPlaySound). */
export function readNotificationPreferences(): {
  suppressed: boolean
  suppressSuccess: boolean
  playSound: boolean
  duration: 'Short' | 'Normal' | 'Long'
  position: string
} {
  const app = readApplicationScope()
  const notifications =
    typeof app['Notifications'] === 'object' && app['Notifications'] !== null
      ? (app['Notifications'] as Record<string, unknown>)
      : {}
  const duration = app['NotificationDuration']
  const position = app['NotificationPosition']
  return {
    suppressed: app['DontShowNotifications'] === true,
    suppressSuccess: notifications['SuccessNotifications'] === false,
    playSound: notifications['NotificationSound'] === true,
    duration:
      duration === 'Short' || duration === 'Long' ? duration : ('Normal' as const),
    position: typeof position === 'string' ? position : 'BottomRight'
  }
}

export function maybePlayNotificationSound(): void {
  if (readNotificationPreferences().playSound) playNotificationSound()
}
