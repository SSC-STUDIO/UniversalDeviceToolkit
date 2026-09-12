export type SensorViewPhase = 'idle' | 'loading' | 'error' | 'ready'

/**
 * Snapshot presence always wins (live updates can recover after a failed
 * first fetch). A requested "ready" without a snapshot is treated as error
 * so the board cannot stay on a perpetual loading skeleton.
 */
export function resolveSensorViewPhase(
  snapshot: unknown | null,
  requested: SensorViewPhase
): SensorViewPhase {
  if (snapshot != null) return 'ready'
  if (requested === 'ready') return 'error'
  return requested
}
