import { defineConfig, devices } from '@playwright/test'

// End-to-end tests drive a REAL browser against the REAL system started by
// Docker Compose. Start the system first:
//   docker compose -f infra/docker-compose.yml up --build
export default defineConfig({
  testDir: './tests',
  // One test at a time: the tests share one running system and its rate limits.
  workers: 1,
  fullyParallel: false,
  reporter: 'list',
  use: {
    baseURL: process.env.SITE_URL ?? 'http://localhost:8080',
    // When a test fails, keep a recording you can open with "npx playwright show-trace".
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
