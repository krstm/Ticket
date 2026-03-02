import { defineConfig, devices } from '@playwright/test';

const port = process.env.PLAYWRIGHT_PORT ?? '5178';
const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: './tests/e2e',
  timeout: 60000,
  retries: process.env.CI ? 1 : 0,
  expect: {
    timeout: 10000,
  },
  use: {
    baseURL,
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
      },
    },
  ],
  webServer: {
    command: `dotnet run --configuration Release --urls ${baseURL}`,
    cwd: '.',
    env: {
      ASPNETCORE_ENVIRONMENT: 'Playwright',
    },
    timeout: 120000,
    reuseExistingServer: false,
  },
});
