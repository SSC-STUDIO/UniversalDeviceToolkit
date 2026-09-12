/**
 * Power mode metadata — port of PowerModeStateExtensions:
 * per-state accent color used by the Electron client
 * (Quiet #357BF2, Balance white, Performance #D43333, Extreme #FF8C00, GodMode #6334E3).
 */

export const POWER_MODE_COLORS: Record<string, string> = {
  Quiet: '#357bf2',
  Balance: '#ffffff',
  Performance: '#d43333',
  Extreme: '#ff8c00',
  GodMode: '#6334e3'
}

/**
 * Accent color for a power mode state name ("Performance" | "performance").
 * Returns undefined for unknown states (Electron falls back to Transparent).
 * Balance resolves to the theme foreground instead of WPF's white so the
 * icon stays visible on light surfaces too.
 */
export function powerModeColor(state: string | undefined | null): string | undefined {
  if (!state) return undefined
  const normalized = POWER_MODE_COLORS[state] !== undefined
    ? state
    : state.charAt(0).toUpperCase() + state.slice(1)
  if (normalized === 'Balance') return 'var(--udt-text-primary)'
  return POWER_MODE_COLORS[normalized]
}
