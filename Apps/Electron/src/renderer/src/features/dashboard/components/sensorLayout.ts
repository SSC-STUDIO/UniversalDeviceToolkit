export const SENSOR_COLUMNS = ['CPU', 'Battery', 'GPU'] as const

export type SensorColumnId = (typeof SENSOR_COLUMNS)[number]

export function normalizeSensorColumnId(value: string): SensorColumnId | null {
  const upper = value.toUpperCase()
  if (upper === 'CPU') return 'CPU'
  if (upper === 'BATTERY') return 'Battery'
  if (upper === 'GPU') return 'GPU'
  return null
}

export function readStringList(value: unknown): string[] {
  if (!Array.isArray(value)) return []
  return value.filter((item): item is string => typeof item === 'string')
}

/** Visible dashboard columns from hardwareSensors (VisibleSections + SectionOrder). */
export function readSensorLayout(scopes: Record<string, unknown>): SensorColumnId[] {
  const hardware =
    typeof scopes.hardwareSensors === 'object' && scopes.hardwareSensors !== null
      ? (scopes.hardwareSensors as Record<string, unknown>)
      : {}
  const visible = new Set(
    readStringList(hardware.VisibleSections ?? hardware.visibleSections)
      .map(normalizeSensorColumnId)
      .filter((id): id is SensorColumnId => id != null)
  )
  const ordered: SensorColumnId[] = []
  for (const item of readStringList(hardware.SectionOrder ?? hardware.sectionOrder)) {
    const id = normalizeSensorColumnId(item)
    if (id != null && (visible.size === 0 || visible.has(id)) && !ordered.includes(id)) {
      ordered.push(id)
    }
  }
  for (const id of SENSOR_COLUMNS) {
    if ((visible.size === 0 || visible.has(id)) && !ordered.includes(id)) ordered.push(id)
  }
  return ordered.length > 0 ? ordered : [...SENSOR_COLUMNS]
}
