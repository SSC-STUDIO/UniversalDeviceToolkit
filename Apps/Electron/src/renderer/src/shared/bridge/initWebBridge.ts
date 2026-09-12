import { createWebBridge } from './webBridgeClient'

/**
 * Must be imported before any module that reads window.bridge at load time.
 * Wired as the first import in main.tsx for browser dev (npm run dev:web).
 */
if (import.meta.env.DEV && typeof window !== 'undefined' && !window.bridge) {
  const bridgeUrl = import.meta.env.VITE_DEV_BRIDGE_URL
  if (bridgeUrl) {
    window.bridge = createWebBridge(bridgeUrl)
  }
}
