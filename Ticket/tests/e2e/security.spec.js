import { test, expect } from '@playwright/test';

const BASE_URL = process.env.PLAYWRIGHT_BASE_URL ?? 'http://127.0.0.1:5178';

const createCategory = async (api, name) => {
  const response = await api.post('/categories', { data: { name } });
  expect(response.ok()).toBeTruthy();
  return response.json();
};

const createDepartment = async (api, name, email) => {
  const response = await api.post('/departments', {
    data: {
      name,
      members: [
        {
          fullName: `${name} Member`,
          email,
          notifyOnTicketEmail: true,
        },
      ],
    },
  });
  expect(response.ok()).toBeTruthy();
  return response.json();
};

const createTicket = async (api, payload) => {
  const response = await api.post('/tickets', { data: payload });
  expect(response.ok()).toBeTruthy();
  return response.json();
};

const addComment = async (api, ticketId, actor, body) => {
  const response = await api.post(`/tickets/${ticketId}/comments`, {
    data: {
      body,
      actor,
    },
  });
  expect(response.ok()).toBeTruthy();
  return response.json();
};

const bootstrapTicket = async (api, overrides = {}) => {
  const unique = Date.now();
  const category = await createCategory(api, `E2E Category ${unique}`);
  const departmentEmail = `dept-${unique}@example.com`;
  const department = await createDepartment(api, `E2E Department ${unique}`, departmentEmail);
  const ticket = await createTicket(api, {
    title: overrides.title ?? `E2E Ticket ${unique}`,
    description: overrides.description ?? 'secure body',
    priority: 1,
    categoryId: category.id,
    departmentId: department.id,
    requester: {
      name: 'Requester',
      email: `requester-${unique}@example.com`,
    },
  });

  return { ticket, departmentEmail };
};

const departmentActor = (email) => ({
  name: 'Dept Member',
  email,
  actorType: 1,
});

test('reports dashboard should avoid third-party requests', async ({ page }) => {
  const disallowed = [];
  await page.route('**/*', (route) => {
    const url = route.request().url();
    const sameOrigin = url.startsWith(BASE_URL) || url.startsWith('data:') || url.startsWith('about:');
    if (!sameOrigin) {
      disallowed.push(url);
    }
    route.continue();
  });

  await page.goto('/ui/reports');
  await expect(page.getByRole('heading', { name: 'System Insights' })).toBeVisible();
  expect(disallowed).toEqual([]);
});

test('ticket details sanitize descriptions and comments', async ({ page, request }) => {
  const { ticket, departmentEmail } = await bootstrapTicket(request, {
    description: "<script>alert('ticket')</script><b>bold</b>",
  });

  await addComment(request, ticket.id, departmentActor(departmentEmail), "<script>alert('c')</script>comment <b>strong</b>");

  await page.goto(`/ui/tickets/${ticket.id}`);
  const description = page.locator('[data-testid="ticket-description"]');
  await expect(description).toContainText('<b>bold</b>');
  await expect(description.locator('script')).toHaveCount(0);

  const commentBody = page.locator('[data-testid="ticket-comment-body"]').first();
  await expect(commentBody).toContainText('comment');
  await expect(commentBody.locator('script')).toHaveCount(0);
});

test('status updates require actor context', async ({ page, request }) => {
  const { ticket } = await bootstrapTicket(request);

  let statusRequests = 0;
  await page.route(`**/tickets/${ticket.id}/status`, async (route) => {
    statusRequests += 1;
    await route.fulfill({ status: 400, body: '{}' });
  });

  await page.goto(`/ui/tickets/${ticket.id}`);
  await page.evaluate(() => {
    const component = document.querySelector('div[x-data^="ticketDetailsPage"]');
    const state = component?.__x?.$data;
    if (state) {
      state.actor.name = '';
      state.actor.email = '';
      state.actor.actorType = 'Requester';
      state.newStatus = 'InProgress';
      state.statusNote = '';
      return state.updateStatus();
    }
    return null;
  });
  await page.waitForTimeout(250);
  expect(statusRequests).toBe(0);
});
