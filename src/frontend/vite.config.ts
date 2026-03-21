import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import path from 'path'

const isTest = process.env.VITEST === 'true' || process.env.NODE_ENV === 'test'
const worktreeRoot = path.resolve(__dirname, '../..')

export default defineConfig({
  plugins: [
    react(),
    ...(!isTest ? [VitePWA({
      registerType: 'autoUpdate',
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'service-worker.ts',
      manifest: {
        name: 'Medicine Scheduler',
        short_name: 'MedSched',
        theme_color: '#ffffff',
        icons: [
          { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png' }
        ]
      },
      devOptions: { enabled: true, type: 'module' }
    })] : [])
  ],
  server: {
    proxy: {
      '/auth': 'http://localhost:5000',
      '/patients': 'http://localhost:5000',
      '/medications': 'http://localhost:5000',
      '/schedule': 'http://localhost:5000',
      '/push': 'http://localhost:5000',
      '/settings': 'http://localhost:5000',
    },
    fs: { allow: [worktreeRoot] }
  },
  test: {
    root: worktreeRoot,
    environment: 'jsdom',
    setupFiles: [path.resolve(__dirname, 'src/test-setup.ts')],
    globals: true,
    include: ['tests/frontend/**/*.{test,spec}.?(c|m)[jt]s?(x)', 'src/frontend/src/**/*.{test,spec}.?(c|m)[jt]s?(x)']
  }
})
