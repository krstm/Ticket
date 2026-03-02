import { defineConfig, devices } from '@playwright/test';

const port = process.env.PLAYWRIGHT_PORT ?? '5178';
const defaultHost = process.env.PLAYWRIGHT_HOST ?? '127.0.0.1';
const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? `http://${defaultHost}:${port}`;

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
      'Database:Provider': 'InMemory',
    },
    timeout: 300000,
    reuseExistingServer: !process.env.CI,
    url: baseURL,
  },
});
