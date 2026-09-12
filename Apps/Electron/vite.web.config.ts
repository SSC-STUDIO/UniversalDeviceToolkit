import { resolve } from 'path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const bridgePort = process.env.UDT_DEV_BRIDGE_PORT ?? '17831'
const bridgeUrl = process.env.VITE_DEV_BRIDGE_URL ?? `http://127.0.0.1:${bridgePort}`

export default defineConfig({
  root: resolve(__dirname, 'src/renderer'),
  envDir: resolve(__dirname),
  plugins: [
    react(),
    {
      name: 'relax-csp-for-web-dev',
      transformIndexHtml(html) {
        return html.replace(
          /content="default-src[^"]*"/,
          "content=\"default-src 'self'; script-src 'self' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' http://127.0.0.1:* ws://127.0.0.1:*\""
        )
      }
    }
  ],
  resolve: {
    alias: {
      '@renderer': resolve(__dirname, 'src/renderer/src')
    }
  },
  define: {
    'import.meta.env.VITE_DEV_BRIDGE_URL': JSON.stringify(bridgeUrl)
  },
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true
  }
})
