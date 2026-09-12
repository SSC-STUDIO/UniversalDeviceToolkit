import { resolve } from 'path'
import { defineConfig } from 'electron-vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  // electron-vite defaults to minify:false for every target; the app ships
  // production bundles inside app.asar, so minified output directly cuts both
  // package size and V8 parse time at startup.
  main: {
    build: { minify: 'esbuild' }
  },
  preload: {
    build: { minify: 'esbuild' }
  },
  renderer: {
    resolve: {
      alias: {
        '@renderer': resolve('src/renderer/src')
      }
    },
    plugins: [react()],
    build: {
      minify: 'esbuild',
      rollupOptions: {
        output: {
          // Split only modules that are already in the module graph. Naming a
          // barrel package (especially @fluentui/react-icons) as a chunk entry
          // can pull the entire export surface into the renderer.
          manualChunks(id) {
            // echarts + its zrender engine load together, and only when a
            // trend chart actually mounts (utils/echarts.ts lazy loader).
            if (id.includes('node_modules/echarts') || id.includes('node_modules/zrender')) {
              return 'charts'
            }
            if (id.includes('node_modules/@fluentui/react-icons')) return 'icons'
            // Keep the truly startup-critical framework in one eager chunk.
            // antd intentionally has no manual assignment: Rollup then splits
            // it by consumer, so components used only by lazy routes (Select,
            // Slider, Tabs, ColorPicker, ...) stay out of the startup graph
            // instead of being hoisted into an eagerly parsed vendor chunk.
            if (
              id.includes('node_modules/react-dom') ||
              id.includes('node_modules/react-router') ||
              id.includes('node_modules/react/') ||
              id.includes('node_modules/scheduler')
            ) {
              return 'vendor'
            }
            return undefined
          }
        }
      }
    }
  }
})
