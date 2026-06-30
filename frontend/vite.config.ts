import { defineConfig, loadEnv } from 'vite'
import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // Backend ASP.NET Core (profile "http" trong launchSettings.json). Đổi qua .env nếu cần.
  const apiTarget = env.VITE_API_PROXY_TARGET || 'http://localhost:5024'

  return {
    plugins: [
      react(),
      babel({ presets: [reactCompilerPreset()] })
    ],
    server: {
      port: 5173,
      // Proxy mọi request /api -> backend, tránh lỗi CORS khi dev.
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
  }
})
