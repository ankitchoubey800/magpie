import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Where the gateway listens when you run it with `dotnet run` on your laptop.
// Override with:  GATEWAY_URL=http://localhost:8080 npm run dev
const gatewayUrl = process.env.GATEWAY_URL ?? 'http://localhost:5100'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // In development the browser loads the page from Vite (port 5173).
    // Anything starting with /api is forwarded to the gateway, so the React
    // code can always use relative addresses like "/api/accounts/ping".
    proxy: {
      // xfwd adds the X-Forwarded-... headers, so the backend still learns the
      // address you typed (localhost:5173) even though Vite passes the request on.
      '/api': { target: gatewayUrl, changeOrigin: true, xfwd: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
