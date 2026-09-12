import { useEffect, type ReactNode } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useHostCapabilitiesStore } from '../stores/hostCapabilitiesStore'

const CAPABILITY_ROUTES: ReadonlyMap<string, string> = new Map([
  ['/keyboard', 'keyboard'],
  ['/automation', 'automation'],
  ['/macro', 'macro'],
  ['/optimization', 'optimization']
])

/**
 * Route-level guard for direct launches and tray navigation. Navigation hides
 * unavailable entries, but a stale deep link should not expose an unsupported
 * page before the user can act.
 */
export function CapabilityGate({ children }: { children: ReactNode }): React.JSX.Element {
  const capabilities = useHostCapabilitiesStore((state) => state.capabilities)
  const location = useLocation()
  const navigate = useNavigate()

  useEffect(() => {
    if (capabilities == null) return
    const route = CAPABILITY_ROUTES.get(location.pathname)
    if (route != null && capabilities.capabilities[route as keyof typeof capabilities.capabilities] === false) {
      navigate('/dashboard', { replace: true })
    }
  }, [capabilities, location.pathname, navigate])

  return <>{children}</>
}
