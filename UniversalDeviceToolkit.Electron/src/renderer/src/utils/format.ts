const MIB_PER_GIB = 1024

function isFiniteNumber(value: number | null | undefined): value is number {
  return value != null && Number.isFinite(value)
}

function formatGigabyteQuantity(mb: number): string {
  const gb = mb / MIB_PER_GIB
  if (gb >= 0.05) return gb.toFixed(1)
  if (mb > 0) return gb.toFixed(2)
  return '0.0'
}

/**
 * Formats Host `*Mb` usage as "x.x / y.y GB (z%)".
 * Percent is always used/total when both are valid; omitted when total is 0.
 */
export function formatUsageInGigabytes(
  usedMb: number | null | undefined,
  totalMb: number | null | undefined,
  percentage: number | null | undefined = -1
): string {
  const total = isFiniteNumber(totalMb) && totalMb > 0 ? totalMb : null
  const percentHint = isFiniteNumber(percentage) && percentage >= 0 ? percentage : null

  let used = isFiniteNumber(usedMb) && usedMb >= 0 ? usedMb : null
  if (used == null && total != null && percentHint != null) {
    used = total * (percentHint / 100)
  }

  if (used == null) {
    return percentHint != null ? `${percentHint.toFixed(0)}%` : '-'
  }

  if (total == null) {
    if (used === 0) return '-'
    return `${formatGigabyteQuantity(used)} GB`
  }

  const percent = (used / total) * 100
  return `${formatGigabyteQuantity(used)} / ${formatGigabyteQuantity(total)} GB (${percent.toFixed(0)}%)`
}
