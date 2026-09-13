import { Suspense, useState, type ReactNode } from 'react'
import { matchPath, Route, Routes, useLocation } from 'react-router-dom'
import CachedView from '../shared/ui/CachedView'

/** Keeps visited page state and DOM, suspending subscriptions while away. */
export default function CachedRoute({ path, children, fallback }: {
  path: string
  children: ReactNode
  fallback: ReactNode
}): React.JSX.Element {
  const location = useLocation()
  const active = matchPath({ path, end: true }, location.pathname) !== null
  const [lastLocation, setLastLocation] = useState(location)
  if (active && lastLocation !== location) setLastLocation(location)

  return (
    <CachedView active={active}>
      <div className="udt-page-enter" data-udt-page={path}>
        <Suspense fallback={fallback}>
          {/* Hidden pages retain their own query parameters and route context. */}
          <Routes location={active ? location : lastLocation}>
            <Route path={path} element={children} />
          </Routes>
        </Suspense>
      </div>
    </CachedView>
  )
}
