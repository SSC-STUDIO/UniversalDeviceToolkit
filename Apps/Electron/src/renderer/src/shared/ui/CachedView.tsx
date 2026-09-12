import { Activity, useState, type ReactNode } from 'react'

/** Mount on first visit; retain local state while hidden effects are paused. */
export default function CachedView({ active, children }: { active: boolean; children: ReactNode }): React.JSX.Element | null {
  const [visited, setVisited] = useState(active)
  if (active && !visited) setVisited(true)
  if (!active && !visited) return null
  return <Activity mode={active ? 'visible' : 'hidden'}>{children}</Activity>
}
