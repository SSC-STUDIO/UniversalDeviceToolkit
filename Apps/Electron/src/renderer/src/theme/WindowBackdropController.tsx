import { useEffect } from 'react'
import { on } from '../api/bridge'
import { applyWindowBackdrop, loadWindowBackdrop } from './windowBackdrop'
import './WindowBackdrop.css'

export default function WindowBackdropController(): null {
  useEffect(() => {
    let disposed = false
    const load = async (): Promise<void> => {
      try {
        await loadWindowBackdrop()
      } catch {
        if (!disposed) applyWindowBackdrop('Windows')
      }
    }

    void load()
    const offHostReady = on('host.ready', () => {
      if (!disposed) void load()
    })

    return () => {
      disposed = true
      offHostReady()
    }
  }, [])

  return null
}
