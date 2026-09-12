const BYTES_PER_UNIT = { KB: 1024, MB: 1024 ** 2, GB: 1024 ** 3 } as const

/** Unit conversion only; callers own missing values, thresholds and precision. */
export function bytesInUnit(bytes: number, unit: keyof typeof BYTES_PER_UNIT): number {
  return bytes / BYTES_PER_UNIT[unit]
}
