/// <reference types="vite/client" />
/// <reference path="../preload/index.d.ts" />

interface ImportMetaEnv {
  /** Set by vite.web.config.ts / dev:web for browser debugging against the Host. */
  readonly VITE_DEV_BRIDGE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
