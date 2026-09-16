import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end against the real stack: the built frontend, the real Api, RabbitMQ, and a Judge that
 * actually runs the submitted code in a container. Nothing is stubbed, which is the point -- the
 * component tests already cover the pieces in isolation.
 */
export default defineConfig({
  testDir: './e2e',
  // Judging is a queue round trip plus a container per test case, so give it room.
  timeout: 90_000,
  expect: { timeout: 30_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['list']] : [['list']],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:4173',
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: 'npm run preview -- --port 4173',
        url: 'http://localhost:4173',
        reuseExistingServer: !process.env.CI,
        timeout: 60_000,
      },
});
