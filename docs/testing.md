# Ticket Platform - Testing Charter & Playbook

This document acts as the "test constitution" for the repository. A future engineer (or LLM with zero context) should be able to understand the full philosophy, tooling, and coverage strategy by reading this file alone.

---

## 1. Test Constitution (Non-Negotiable Rules)

1. **Full capability coverage:** Every behaviour must be covered end-to-end, including happy paths, sad paths, concurrency, abuse, and security scenarios. A feature is *never* "done" until its edge cases exist in tests.
2. **Real database isolation:** Automated tests may not touch a developer's SQL Server. CustomWebApplicationFactory swaps ApplicationDbContext to EF InMemory with a unique database name per test run (TicketTests-{Guid}). ResetStateAsync calls EnsureDeleted/EnsureCreated before each test and also resets the notification spy. If a future test needs SQL Server (e.g., provider-specific behaviour), it must spin up an isolated container.
3. **Adversarial mindset:** Tests are written to break the code. Payloads deliberately omit headers, reuse stale row versions, flood rate limiters, exercise injection-like search terms, and supply invalid PageTokens. A suite that only asserts happy paths is considered incomplete.
4. **Deterministic & fast:** Suites must run via dotnet test in a few seconds. Factories tighten rate limits and use the in-memory notifier spy to avoid network I/O.
5. **Executable documentation:** Each test names the scenario plainly (DomainEvents_ShouldPersistHistory_And_FireNotifications, InvalidPageToken_ShouldReturnBadRequest, etc.), doubling as requirements documentation.

---

## 2. Test Project Layout

Ticket.Tests/

| Folder | Purpose |
| --- | --- |
| Unit/ | Pure logic tests (TicketQueryParametersValidatorTests, TicketStatusTransitionRulesTests, etc.). No HTTP/EF dependencies. |
| Integration/ | Full-stack API tests using CustomWebApplicationFactory (real middleware, EF context, MediatR handlers). Exercises lifecycle, pagination, domain events, reporting, and auditing. |
| Security/ | Abuse/regression scenarios: sanitization, rate limiting, SQL-like searches, invalid page tokens, and unauthorized actor attempts. |
| TestUtilities/ | Shared infrastructure: CustomWebApplicationFactory, IntegrationTestBase, TestNotificationService, JSON helpers. |

Dependencies include Microsoft.AspNetCore.Mvc.Testing, Microsoft.EntityFrameworkCore.InMemory, Microsoft.EntityFrameworkCore.Sqlite (reserved), and xUnit. No mocking framework is needed because most tests run the real pipeline.

---

## 3. Test Environment Mechanics

- **CustomWebApplicationFactory**
  - Overrides the connection string with an EF InMemory provider per test run.
  - Adjusts rate limiting via RateLimitingOptions overrides so the suite can deterministically trigger 429s.
  - Swaps the production INotificationService with TestNotificationService, which records create/resolve events and exposes Reset() so each test starts with a clean slate.
  - Ensures the database is re-created before each test via ResetStateAsync (called by IntegrationTestBase.InitializeAsync).
- **IntegrationTestBase**
  - Creates a typed HttpClient with pplication/json accept headers.
  - Provides AsJson, EnsureSuccessAsync, and NotificationSpy helpers.
- **Serialization**
  - Uses System.Text.Json with camelCase conventions, matching the API defaults.

Result: tests never hit real infrastructure, yet they exercise the full ASP.NET Core pipeline, EF Core, AutoMapper, FluentValidation, MediatR, and logging scopes.

---

## 4. Suite Breakdown & Scenarios

### 4.1 Unit Tests (Ticket.Tests/Unit)
- **Validators:** TicketQueryParametersValidatorTests now cover page-size bounds plus mutual exclusivity of PageToken and Page. Any future DTO (e.g., notification options) should receive similar coverage.
- **Domain rules:** TicketStatusTransitionRulesTests verifies the allowed transition matrix.
- **Future slots:** When pure logic helpers (e.g., normalization utilities) expand, add unit tests here to keep integration suites lean.

### 4.2 Integration Tests (Ticket.Tests/Integration/TicketApiTests)
Key scenarios:
1. **Ticket lifecycle:** Create â†’ update â†’ multi-step status transitions â†’ search filtering. Asserts sanitized descriptions, row-version enforcement, and final filtered result.
2. **Concurrency:** PUT with stale If-Match returns 409.
3. **Reporting:** /reports/summary grouping by category.
4. **Keyset pagination:** Requests with PageToken return stable slices, no duplicates, and opaque tokens.
5. **Search scope:** SearchScope=TitleOnly ignores description matches; FullContent finds them.
6. **Domain events:** After creation/resolution, history rows exist and notification spies capture eventsâ€”proving MediatR handlers ran instead of services mutating history directly.

### 4.3 Security/Hardening Tests (Ticket.Tests/Security)
- XSS sanitization (persisted body differs from `<script>` payloads).
- Rate limiting deterministic 429s.
- SQL-injection-like search term returns 200 instead of crashing.
- Invalid PageToken yields HTTP 400 (ensures gatekeeping of keyset API).
- Unauthorized actors attempting updates/comments receive 403 while participants succeed.

---

## 5. Running the Suites

`ash
# From repo root
dotnet test Ticket.sln
`

Selective runs:

`ash
# Run only integration tests
dotnet test Ticket.Tests/Ticket.Tests.csproj --filter Ticket.Tests.Integration

# Run a specific scenario
dotnet test Ticket.Tests/Ticket.Tests.csproj --filter Ticket.Tests.Security.SecurityHardeningTests.InvalidPageToken_ShouldReturnBadRequest
`

No external services (SMTP, queues, SQL Server) are required; everything spins up inside the xUnit host.

---

## 6. Isolation Guarantees & Test Doubles

- **Database safety:** CustomWebApplicationFactory removes the production DbContextOptions<ApplicationDbContext> registration and replaces it with InMemory options. It uses unique names so parallel test classes never collide. ResetStateAsync truncates data between tests and also resets the notification spy.
- **Notifications:** TestNotificationService records CreatedEvents and ResolvedEvents, letting tests assert domain events fired without making network calls. Production code uses NotificationPlaceholderService, but tests prove the plumbing works.
- **RowVersion emulation:** ApplicationDbContext stamps RowVersion on InMemory providers, enabling realistic optimistic concurrency tests.
- **Rate limiting:** Factory overrides RateLimitingOptions to PermitLimit=rateLimit, WindowSeconds=1, QueueLimit=0, ensuring deterministic throttling scenarios.

---

## 7. Extending the Suites

1. **New feature?** Start with an integration test describing the behaviour (even if it fails). Add unit tests for pure logic. Only then implement the feature.
2. **New security requirement?** Create/extend a class under Security/. Use factory overrides (headers, options) to simulate attack patterns (oversized payloads, tampered tokens, etc.).
3. **Services requiring external integrations?** Wrap dependencies in interfaces (INotificationService, future IEmailSender) and inject in tests. Provide spies/fakes under TestUtilities/ to avoid real network calls.
4. **Performance/soak tests?** Keep them out of this test project; create a separate pipeline so the core suite stays fast.

By following this playbook, every capability (especially edge cases) remains testable, isolated from real infrastructure, and adversarial enough to detect regressions early.

---

## 8. Frontend Build Spot-Checks
- Run `npm install` once per clone; afterwards `npm run dev` (or `npm run build`) ensures Vite/Tailwind regenerate `wwwroot/dist`.
- Razor UI smoke-tests piggyback on the integration suite because the controllers/views render real HTML. Before shipping major UI tweaks, run `npm run build` and `dotnet test` to guarantee the bundle + backend both compile.
- `npm audit` currently reports the esbuild advisory (moderate). Remediating it requires upgrading to Vite 7 (and Tailwind 4), which is tracked separately because it forces new config conventions.
### 4.4 Department & Comment Flows
- Integration tests now cover department filtering (DepartmentFilters_ShouldLimitSearchResults), permissions (DepartmentMember_EditPermissions_ShouldBeEnforced), and the public /tickets/{id}/comments APIs.
- Security suite includes adversarial assertions: outsiders attempting updates/comments receive 403s, scripted comments are sanitized, and rate limiting still works after the department bootstrap stage.
- Whenever a new capability is added (mail hooks, future auth), start by cloning one of these tests so we keep the “test first, implement after” habit.
