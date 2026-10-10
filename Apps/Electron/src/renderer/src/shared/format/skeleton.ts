/**
 * Skeleton animation tokens — mirrors Electron SkeletonAnimationTokens +
 * SkeletonShimmer brush math. CSS in styles/skeleton.css consumes the same
 * durations/stagger; this module is the TS source of truth for components.
 */

import type { CSSProperties } from 'react'

export const skeletonAnimationTokens = {
  durationSeconds: 1.7,
  sweepFrom: -1.25,
  sweepTo: 1.25,
  staggerStepSeconds: 0.055,
  staggerMaxSeconds: 0.32,
  breathingFloorOpacity: 0.85
} as const

export type SkeletonBoneVariant = 'default' | 'muted' | 'on-card' | 'chart' | 'static'

export type ShimmerDelayStyle = CSSProperties & { '--udt-shimmer-delay'?: string }

/** Mirrors SkeletonShimmer.RestartSubtreeCore automatic delay. */
export function resolveStaggerDelay(index: number): number {
  return Math.min(
    index * skeletonAnimationTokens.staggerStepSeconds,
    skeletonAnimationTokens.staggerMaxSeconds
  )
}

/** Negative delay keeps each bone permanently de-phased (WPF DelaySeconds = -1 pattern). */
export function shimmerDelayStyle(step = 0): ShimmerDelayStyle {
  return { '--udt-shimmer-delay': `${-resolveStaggerDelay(step)}s` }
}

export function skeletonBoneClass(
  variant: SkeletonBoneVariant = 'default',
  extra?: string
): string {
  const parts = ['udt-skeleton']
  if (variant !== 'default') {
    parts.push(`udt-skeleton--${variant}`)
  }
  if (extra) parts.push(extra)
  return parts.join(' ')
}
