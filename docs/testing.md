# Ticket Platform - Testing Charter & Playbook

This document acts as the "test constitution" for the repository. A future engineer (or LLM with zero context) should be able to understand the full philosophy, tooling, and coverage strategy by reading this file alone.

---

## 1. Test Constitution (Non-Negotiable Rules)

1. **Full capability coverage:** Every behaviour must be covered end-to-end, including happy paths, sad paths, concurrency, abuse, and security scenarios. A feature is *never* "done" until its edge cases exist in tests.
2. **Real database isolation:** Automated tests may not touch a developer's SQL Server. CustomWebApplicationFactory swaps ApplicationDbContext to EF InMemory with a unique database name per test run (TicketTests-{Guid}). ResetStateAsync calls EnsureDeleted/EnsureCreated before each test and also resets the notification spy. If a future test needs SQL Server (e.g., provider-specific behaviour), it must spin up an isolated container.
3. **Adversarial mindset:** Tests are written to break the code. Payloads deliberately omit headers, reuse stale row versions, flood rate limiters, exercise injection-like search terms, and supply invalid PageTokens. A suite that only asserts happy paths is considered incomplete.
4. **Deterministic & fast:** Suites must run via dotnet test in a few seconds. Factories tighten rate limits and use the in-memory notifier spy to avoid network I/O.
5. **Executable documentation:** Each test names the scenario plainly (DomainEvents_ShouldPersistHistory_And_FireNotifications, InvalidPageToken_ShouldReturnBadRequest, etc.), doubling as requirements documentation.
6. **Notification seams stay mocked:** Every automated suite must override `INotificationService` with the spy/fake from `TestUtilities`. Never let a real notifier (SMTP/webhook, even a logging stub) run inside tests; doing so hides regressions and slows the pipeline. New notification handlers must add corresponding spy assertions before implementation starts.

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
- **Builder + clock helpers**
  - `TicketBuilder`, `CategoryBuilder`, `DepartmentBuilder`, and `TicketActorBuilder` keep arrange blocks terse while still producing real DTOs/entities.
  - `SeedCategoryAsync`, `SeedDepartmentAsync`, and `SeedTicketsAsync` hide the boilerplate HTTP calls so scenarios focus on assertions rather than setup.
  - `FakeClock` is registered as the shared `IClock` implementation; tests advance it explicitly to verify ordering (history, comments, reports) without relying on wall-clock timing.
- **Serialization**
  - Uses System.Text.Json with camelCase conventions, matching the API defaults.

Result: tests never hit real infrastructure, yet they exercise the full ASP.NET Core pipeline, EF Core, the manual mapping extensions, FluentValidation, MediatR, and logging scopes.

---

## 4. Suite Breakdown & Scenarios

### 4.1 Unit Tests (Ticket.Tests/Unit)
- **Validators:** Dedicated suites exist for ticket create/update flows, query parameters, notification options, and rate limiting options. Each rule now has explicit sad-path coverage (over 40 parameterized cases) enforced via FluentAssertions.
- **Domain + query helpers:** `TicketStatusTransitionRulesTests` enumerates the full status matrix (36 combinations) and `TicketSearchScopeTests` runs EF-backed filters to prove TitleOnly vs FullContent behaviour along with department/recipient filters.
- **Service helpers & mapping:** `TicketMutationHelperTests`, `TicketDomainEventFactoryTests`, and `MappingExtensionsTests` guard sanitization, normalization, domain-event payloads, and DTO ordering logic so regressions are caught before integration runs.

### 4.2 Integration Tests (Ticket.Tests/Integration)
- **TicketApiTests:** End-to-end lifecycle (create → update → status transitions), concurrency (If-Match 409), and actor enforcement continue to live here.
- **TicketKeysetPaginationTests:** Exercises opaque page tokens, duplicate prevention, and server-side status filters while driving the HTTP pipeline.
- **TicketCommentsIntegrationTests:** Verifies sanitized comment posting, ordering guarantees (using FakeClock), and notification spy hooks.
- **Departments/Category suites:** CRUD coverage for `/departments` and `/categories`, including inactive filters, membership toggles, and category deactivation guards when tickets exist.
- **Reports/ReportingApiTests:** Summary plus trend endpoints now run against seeded data with deterministic From/To windows so grouping/interval logic is kept honest.

### 4.3 Security/Hardening Tests (Ticket.Tests/Security)
- XSS sanitization (persisted body differs from `<script>` payloads).
- Rate limiting deterministic 429s.
- SQL-injection-like search term returns 200 instead of crashing.
- Invalid PageToken yields HTTP 400 (ensures gatekeeping of keyset API).
- Unauthorized actors attempting updates/comments receive 403 while participants succeed.
- Mutation rate-limiter suite (`Security/RateLimiting/MutationRateLimitingTests`) hammers POST/GET endpoints with low permit windows to guarantee deterministic 429s across verbs.

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

CI mirrors the same command and now runs `dotnet test Ticket.sln --collect:"XPlat Code Coverage"` so coverage artifacts are uploaded on every push/PR (target ≥80% for backend assemblies).

### 5.1 Latest Execution Log
- 2026-03-02 19:32 UTC+3 — `dotnet test Ticket.sln` ✅ 136 tests (FluentAssertions-powered suites, coverage collector enabled).
- 2026-03-02 04:36 UTC+3 — `cmd /c "cd Ticket && npm run test:e2e"` ✅ Playwright security suite (3 tests) all green against the Playwright profile (`ASPNETCORE_ENVIRONMENT=Playwright`).
- 2026-03-02 04:34 UTC+3 — `cmd /c "cd Ticket && npm run build"` ✅ Vite emitted `wwwroot/dist/main.iife.js` + `main.css` (fonts reported as runtime-resolved because they stay under `/fonts/inter`).
- 2026-03-02 04:33 UTC+3 — `cmd /c "cd Ticket && npm run lint"` ✅ ESLint (JS recommended rules) with zero warnings.

---

## 6. Isolation Guarantees & Test Doubles

- **Database safety:** CustomWebApplicationFactory removes the production DbContextOptions<ApplicationDbContext> registration and replaces it with InMemory options. It uses unique names so parallel test classes never collide. ResetStateAsync truncates data between tests and also resets the notification spy.
- **Notifications:** TestNotificationService records CreatedEvents and ResolvedEvents, letting tests assert domain events fired without making network calls. Production code wires `NullNotificationService`, so only tests exercise real dispatch logic.
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
- `npm audit` currently reports zero issues (Mar 2, 2026). We still plan the Vite 7/Tailwind 4/Node 22 migration to stay ahead of advisories even when the report is clean.

### 8.1 Frontend Security Baseline — 2026-03-02 04:40 +03
- `cmd /c "cd Ticket && npm audit --production"` → **0 vulnerabilities** (prod + dev). `npm run check:audit` wraps the same command with `--audit-level=high` and already runs on every PR/push.
- `cmd /c "cd Ticket && npm outdated"` → **no output**, confirming all pinned versions are current after the dependency cleanup (lucide removed, Chart.js/DOMPurify/Playwright/ESLint pinned).
- `cmd /c "cd Ticket && npm ls --depth=0"` → dependency snapshot:

```text
ticket-frontend@1.0.0
├── @eslint/js@10.0.1
├── @playwright/test@1.58.2
├── @tailwindcss/cli@4.2.1
├── @tailwindcss/forms@0.5.11
├── @tailwindcss/postcss@4.2.1
├── @tailwindcss/typography@0.5.19
├── @tailwindcss/vite@4.2.1
├── alpinejs@3.15.8
├── chart.js@4.5.1
├── dompurify@3.3.1
├── eslint@10.0.2
├── tailwindcss@4.2.1
└── vite@7.3.1
```

- `cmd /c "cd Ticket && rg -n "http" Views Frontend/src wwwroot"` → now only documentation strings remain (`wwwroot/js/site.js` + `_Layout.cshtml.css` scaffolding comments, Tailwind metadata inside `wwwroot/dist/main.css`). Razor views no longer reference third-party hosts; fonts + Chart.js are self-hosted, which keeps the CSP strict (`default-src 'self'`).
- Cadence: before any release run `npm run lint`, `npm run check:audit`, `npm run build`, `npm run test:e2e`, and `dotnet test`. Monthly, review `npm outdated` even if it stays empty so we can log drift.
- CI gate: `.github/workflows/dotnet.yml` + `security.yml` execute the same commands plus CodeQL + OWASP ZAP Baseline, so regressions fail fast.


### 4.4 Department & Comment Flows
- Integration tests now cover department filtering (DepartmentFilters_ShouldLimitSearchResults), permissions (DepartmentMember_EditPermissions_ShouldBeEnforced), and the public /tickets/{id}/comments APIs.
- Security suite includes adversarial assertions: outsiders attempting updates/comments receive 403s, scripted comments are sanitized, and rate limiting still works after the department bootstrap stage.
- Whenever a new capability is added (mail hooks, future auth), start by cloning one of these tests so we keep the “test first, implement after” habit.

---

## 9. Security Scans & Manual Review Log

| Timestamp (UTC+3) | Command | Result | Notes |
| --- | --- | --- | --- |
| 2026-03-02 02:32 | `dotnet list Ticket/Ticket.csproj package --vulnerable` | **No vulnerable packages found.** | Required gate before merging to main. |
| 2026-03-02 02:33 | `dotnet list Ticket/Ticket.csproj package --outdated` | EF Core 8.0.6 → 10.0.3, Serilog 8.x → 10.x updates available. | Logged for later; upgrade only when time is allocated for full regression. |
| 2026-03-02 02:34 | `cmd /c "cd Ticket && npm audit --json"` | 0 vulnerabilities (prod 5, dev 129 deps). | Attach the JSON blob to CI artifacts for traceability. |
| 2026-03-02 02:35 | `rg -n -i "eval\(|CodeDom|ProcessStart"` | Matches limited to vendor JS (jquery*, validation bundles). | Reviewed and accepted; no project code uses `eval`/CodeDom/Process spawning. |
| 2026-03-02 02:35 | `rg -n "TODO|HACK|FIXME"` | Hits only inside vendor bundles. | No latent TODOs inside our code path. |
| 2026-03-02 04:32 | `cmd /c "cd Ticket && npm audit --production"` | **0 vulnerabilities** | Mirrors `npm run check:audit`; attach JSON if it ever reports anything. |
| 2026-03-02 04:33 | `cmd /c "cd Ticket && npm outdated"` | *(no output)* | Confirms all pinned frontend dependencies are current. |
| 2026-03-02 04:33 | `cmd /c "cd Ticket && npm ls --depth=0"` | Snapshot recorded above. | Use as reference when auditing diffing lockfiles. |
| 2026-03-02 04:33 | `cmd /c "cd Ticket && npm run lint"` | ESLint clean (0 warnings). | Blocks CI if any JS rule regresses. |
| 2026-03-02 04:34 | `cmd /c "cd Ticket && npm run build"` | Vite build succeeded. | Emits `wwwroot/dist/main.*`; rerun whenever CSS/JS changes. |
| 2026-03-02 04:36 | `cmd /c "cd Ticket && npm run test:e2e"` | Playwright suite passed (3 tests). | Verifies no third-party requests + sanitized rendering + actor guard. |
| 2026-03-02 04:39 | `dotnet test Ticket.sln` | 25 tests passed. | Includes new SecurityHardening tests for sanitization + actor enforcement. |
| 2026-03-02 04:40 | `cmd /c "cd Ticket && rg -n \"http\" Views Frontend/src wwwroot"` | Only documentation references remain. | Confirms CDN removal before CSP activation. |

**Manual spot checks (2026-03-02 02:36 UTC+3):**
- `TicketService`, `TicketHistoryHandler`, `NotificationDispatcherHandler`, and all controllers were reviewed for unchecked string concatenation or unsanitized user input. All paths continue to flow through FluentValidation, HtmlContentSanitizer, normalization helpers, and EF parameterization (no raw SQL/string concatenation).
- Domain-event handlers (history + audit + notification dispatch) only operate after SaveChanges succeeds; they reuse the existing DbContext scope so no nested transactions or partial writes surfaced.

**Required before release:**
1. Run `dotnet list Ticket/Ticket.csproj package --vulnerable` and `npm audit --json`. Fail the pipeline if either reports a CVE.
2. Run `dotnet list ... --outdated` to capture pending dependency bumps (attach the output to the release notes even if we defer upgrades).
3. Optional but recommended: `dotnet format analyzers --verify-no-changes` and `trufflehog filesystem --since HEAD~50` if those tools are available. Document results in this table.
4. Run the `rg` sweeps above. Any hits outside `wwwroot/lib` require remediation or an explicit risk acceptance in this document.

---

## 10. Frontend Security Checklist
1. **Dependency + build gate:** Run `npm run lint`, `npm run check:audit`, `npm run build`, `npm run test:e2e`, and `dotnet test` locally before opening a PR; CI enforces the same set plus CodeQL + OWASP ZAP Baseline.
2. **No third-party hosts:** `cmd /c "cd Ticket && rg -n \"http\" Views Frontend/src wwwroot"` must only hit documentation comments. If a new vendor is needed, document the risk, self-host it, and adjust the CSP before merging.
3. **Fonts & charts stay local:** Inter variants live under `Ticket/wwwroot/fonts/inter/` and Chart.js ships via npm. Never reintroduce Google Fonts/CDNs; doing so would break the CSP.
4. **CSP verification:** After publishing, `curl -I https://{host}/` should include `Content-Security-Policy: default-src 'self'; ...`. If you need to relax a directive, update `ContentSecurityPolicyMiddleware`, justify it in `docs/architecture.md`, and add regression tests.
5. **Sanitization + actor context:** Frontend mutations must run `sanitizeInput` and `ticketActorContext.ensure()` before issuing `fetch`. The Playwright tests (`tests/e2e/security.spec.js`) plus `SecurityHardeningTests` in xUnit will fail if this contract regresses.
6. **Monitoring:** Review ZAP Baseline artifacts produced by `.github/workflows/security.yml` and log any Medium/High findings here. Treat missed reports as a release blocker.
