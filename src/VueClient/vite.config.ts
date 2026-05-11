import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { env } from 'node:process'

const target = env.ASPNETCORE_HTTPS_PORT
  ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
  : env.ASPNETCORE_URLS
    ? env.ASPNETCORE_URLS.split(';')[0]
    : 'http://localhost:5268'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: Number(env.DEV_SERVER_PORT ?? 5173),
    strictPort: true,
    proxy: {
      '/api': {
        target,
        secure: false,
        changeOrigin: true,
      },
      '/health': {
        target,
        secure: false,
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
})
