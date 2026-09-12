import { useEffect, useRef, useState } from 'react'
import { create } from 'zustand'
import { useTranslation } from 'react-i18next'
import { SYMBOL_CATALOG } from './symbolIcons'
import { useUtilsDialog } from './useUtilsDialog'
import './utils.css'

/**
 * Port of Electron SymbolRegularPicker: an icon picker with a search filter
 * (debounced like the Electron DebounceDispatcher) over a grid of symbol buttons.
 * Returns the selected icon name, or null for "Default".
 */

interface SymbolPickerRequest {
  id: number
}

let requestSeq = 0
let pendingResolve: ((icon: string | null) => void) | null = null

interface SymbolPickerState {
  request: SymbolPickerRequest | null
  show: () => void
  settle: (icon: string | null) => void
}

const useSymbolPickerStore = create<SymbolPickerState>((set) => ({
  request: null,
  show: () => set({ request: { id: ++requestSeq } }),
  settle: (icon) => {
    pendingResolve?.(icon)
    pendingResolve = null
    set({ request: null })
  }
}))

export function openSymbolPicker(): Promise<string | null> {
  return new Promise((resolve) => {
    pendingResolve = resolve
    useSymbolPickerStore.getState().show()
  })
}

const DEBOUNCE_MS = 300

export default function SymbolPickerModalHost(): React.JSX.Element {
  const { t } = useTranslation()
  const request = useSymbolPickerStore((s) => s.request)
  const settle = useSymbolPickerStore((s) => s.settle)
  const [filter, setFilter] = useState('')
  const [filtered, setFiltered] = useState(SYMBOL_CATALOG)
  const inputRef = useRef<HTMLInputElement>(null)
  const debounceRef = useRef<number | undefined>(undefined)
  const { dialogRef, titleId, dialogProps } = useUtilsDialog(request != null, () => settle(null))

  useEffect(() => {
    if (!request) return
    setFilter('')
    setFiltered(SYMBOL_CATALOG)
    const timer = window.setTimeout(() => inputRef.current?.focus(), 0)
    return () => window.clearTimeout(timer)
  }, [request])

  useEffect(() => {
    return () => window.clearTimeout(debounceRef.current)
  }, [])

  const handleFilterChange = (value: string): void => {
    setFilter(value)
    window.clearTimeout(debounceRef.current)
    debounceRef.current = window.setTimeout(() => {
      const needle = value.trim().toLowerCase()
      setFiltered(
        needle.length === 0
          ? SYMBOL_CATALOG
          : SYMBOL_CATALOG.filter((item) => item.name.toLowerCase().includes(needle))
      )
    }, DEBOUNCE_MS)
  }

  if (!request) return <></>

  return (
    <div className="udt-utils-backdrop" onClick={() => settle(null)}>
      <div
        ref={dialogRef}
        className="udt-utils-modal"
        style={{ width: 760, height: 560 }}
        onClick={(event) => event.stopPropagation()}
        {...dialogProps}
      >
        <div className="udt-utils-modal__title" id={titleId} style={{ paddingBottom: 8 }}>
          {t('wpf.symbolRegularPickertitle')}
        </div>
        <div className="udt-utils-modal__body" style={{ paddingTop: 0 }}>
          <input
            ref={inputRef}
            data-utils-initial-focus=""
            className="udt-utils-input"
            value={filter}
            placeholder={t('wpf.filter')}
            onChange={(event) => handleFilterChange(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                event.preventDefault()
                settle(null)
              }
            }}
          />
          <div className="udt-utils-symbol-grid" style={{ marginTop: 10 }}>
            {filtered.map((item) => (
              <button
                key={item.name}
                type="button"
                className="udt-utils-symbol-cell"
                title={item.name}
                onClick={() => settle(item.name)}
              >
                {item.icon}
              </button>
            ))}
          </div>
          {filtered.length === 0 && (
            <p className="udt-utils-text" style={{ textAlign: 'center', padding: 20 }}>
              {t('wpf.symbolRegularPickerempty')}
            </p>
          )}
        </div>
        <div className="udt-utils-modal__actions">
          <button type="button" className="udt-utils-button" onClick={() => settle(null)}>
            {t('wpf.default')}
          </button>
        </div>
      </div>
    </div>
  )
}
