import { Suspense, lazy, type ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { ArrowSync24Regular, FluentIcon } from '../shared/ui/icons/fluent'
import { isInstallerOptionalFeatureEnabled, type InstallerOptionalFeature } from '../../../shared/installer-selection'
import { CapabilityGate } from './layout/CapabilityRedirect'
import AppLayout from './layout/AppLayout'
import CachedRoute from './CachedRoute'

const DashboardPage = lazy(() => import('../features/dashboard/DashboardPage'))
const SettingsPage = lazy(() => import('../features/settings/SettingsPage'))
const ActionsPage = lazy(() => import('../features/actions/ActionsPage'))
const KeyboardBacklightPage = lazy(() => import('../features/keyboard/KeyboardBacklightPage'))
const ToolsPage = lazy(() => import('../features/tools/ToolsPage'))
const AboutPage = lazy(() => import('../features/about/AboutPage'))

// Fluent spinner instead of antd Spin: the route fallback is on the eager
// startup path, and this keeps antd component code out of the entry chunk.
function PageFallback(): React.JSX.Element {
  return (
    <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
      <FluentIcon size={32} spin color="var(--udt-accent-secondary)">
        <ArrowSync24Regular />
      </FluentIcon>
    </div>
  )
}

function InstalledFeatureRoute({
  feature,
  children
}: {
  feature: InstallerOptionalFeature
  children: ReactNode
}): React.JSX.Element {
  if (!isInstallerOptionalFeatureEnabled(window.bridge?.installerSelection?.features, feature)) {
    return <Navigate to="/dashboard" replace />
  }
  return <>{children}</>
}

export default function App(): React.JSX.Element {
  return (
    <Suspense fallback={<PageFallback />}>
      <CapabilityGate>
        <AppLayout>
          <Routes>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/automation" element={<Navigate to="/actions?view=automation" replace />} />
            <Route path="/macro" element={<Navigate to="/actions?view=macro" replace />} />
            <Route path="/optimization" element={<Navigate to="/tools" replace />} />
            <Route path="*" element={null} />
          </Routes>
          <CachedRoute path="/dashboard" fallback={<PageFallback />}><DashboardPage /></CachedRoute>
          <CachedRoute path="/settings" fallback={<PageFallback />}><SettingsPage /></CachedRoute>
          <CachedRoute path="/actions" fallback={<PageFallback />}><ActionsPage /></CachedRoute>
          <CachedRoute path="/keyboard" fallback={<PageFallback />}>
            <InstalledFeatureRoute feature="keyboard"><KeyboardBacklightPage /></InstalledFeatureRoute>
          </CachedRoute>
          <CachedRoute path="/tools" fallback={<PageFallback />}>
            <InstalledFeatureRoute feature="windowsOptimization"><ToolsPage /></InstalledFeatureRoute>
          </CachedRoute>
          <CachedRoute path="/about" fallback={<PageFallback />}><AboutPage /></CachedRoute>
        </AppLayout>
      </CapabilityGate>
    </Suspense>
  )
}
