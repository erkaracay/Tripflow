import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  test: {
    globals: true,
    environment: 'happy-dom',
    include: ['tests/**/*.{test,spec}.{ts,tsx,vue}'],
    setupFiles: ['tests/setup.ts'],
  },
} as import('vite').UserConfigExport & { test?: import('vitest').UserWorkspaceConfig })
