import { Suspense, lazy, type ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { ArrowSync24Regular, FluentIcon } from './components/icons/fluent'
import { isInstallerOptionalFeatureEnabled, type InstallerOptionalFeature } from '../../shared/installer-selection'
import { CapabilityGate } from './layout/CapabilityRedirect'
import AppLayout from './layout/AppLayout'

const DashboardPage = lazy(() => import('./pages/DashboardParityPage'))
const SettingsPage = lazy(() => import('./pages/SettingsPage'))
const ActionsPage = lazy(() => import('./pages/ActionsPage'))
const KeyboardBacklightPage = lazy(() => import('./pages/KeyboardBacklightPage'))
const WindowsOptimizationPage = lazy(() => import('./pages/WindowsOptimizationPage'))
const AboutPage = lazy(() => import('./pages/AboutPage'))

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
        <Routes>
          <Route element={<AppLayout />}>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/actions" element={<ActionsPage />} />
            <Route path="/automation" element={<Navigate to="/actions?view=automation" replace />} />
            <Route path="/keyboard" element={<InstalledFeatureRoute feature="keyboard"><KeyboardBacklightPage /></InstalledFeatureRoute>} />
            <Route path="/macro" element={<Navigate to="/actions?view=macro" replace />} />
            <Route path="/tools" element={<InstalledFeatureRoute feature="windowsOptimization"><WindowsOptimizationPage /></InstalledFeatureRoute>} />
            <Route path="/optimization" element={<Navigate to="/tools" replace />} />
            <Route path="/about" element={<AboutPage />} />
          </Route>
        </Routes>
      </CapabilityGate>
    </Suspense>
  )
}
