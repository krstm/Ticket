# Ticket Platform – Testing Charter & Playbook

This document is a “test constitution” for the repository. It is written so that any engineer (or any LLM without repo context) can fully understand how, why, and where tests live, and the hard rules the team follows.

---

## 1. Core Principles (“Test Constitution”)

1. **Full Capability Coverage:** Every feature must be exercised end-to-end, across all edge cases, failure paths, and security scenarios. A feature is considered “complete” only when its happy-path, sad-path, concurrency, and abuse cases are under automated test.
2. **Real Database Isolation:** No automated test is allowed to talk to a shared/production database. All tests MUST run against disposable providers (InMemory/SQLite) or isolated containers. A failing test must never corrupt persistent data.
3. **Repeatable & Deterministic:** Tests should produce identical results regardless of run order or machine. Use `EnsureDeleted/EnsureCreated`, in-memory clocks, and factory resets to keep state separated.
4. **Fast Feedback:** Unit and integration suites are expected to run via `dotnet test` in under a few seconds. Heavy, long-running scenarios should be flagged and potentially moved to a separate pipeline.
5. **Executable Documentation:** Every high-level scenario (ticket lifecycle, rate limiting, XSS protection) should be represented by a readable test that doubles as documentation.

---

## 2. Project Layout

`Ticket.Tests/`

| Folder | Role |
| --- | --- |
| `TestUtilities/` | Infrastructure shared by all tests: `CustomWebApplicationFactory` spins up the real ASP.NET Core host using the production `Program`. `IntegrationTestBase` provides helper methods (`AsJson`, `EnsureSuccessAsync`, fixture reset). |
| `Integration/` | High-level API tests that exercise controllers + middleware + EF Core (`TicketApiTests`). |
| `Security/` | Hardening scenarios (rate limiting, API key, XSS, SQL-injection resilience). |
| `Unit/` | Pure logic tests (validators, rules) – no database or HTTP overhead. |

Dependencies added to the test project:
- `Microsoft.AspNetCore.Mvc.Testing` for `WebApplicationFactory`.
- `Microsoft.EntityFrameworkCore.InMemory` for disposable DBs (replaces SQLite from early drafts).
- `Microsoft.EntityFrameworkCore.Sqlite` still referenced if future scenarios need it.
- `Bogus` (if data builders are needed later; currently optional).

---

## 3. Test Environment Mechanics

- **Factory Configuration:** `CustomWebApplicationFactory` overrides DI to use the InMemory EF Core provider with a unique database name per factory instance. Rate limiting options are tightened (limit=2, queue=0) so tests can deterministically hit throttling.
- **Per-Test Isolation:** `IntegrationTestBase.InitializeAsync` calls `EnsureDeleted` + `EnsureCreated`, guaranteeing a clean slate for each test run. No test shares data with another.
- **Helpers:**
  - `AsJson(object)` serializes payloads with camelCase (System.Text.Json defaults).
  - `EnsureSuccessAsync(HttpResponseMessage)` throws an informative exception (status + body) if a call fails, making diagnosis easy.
  - Common assertions (e.g., Base64 RowVersion handling) live inside the tests themselves so they remain self-explanatory.

---

## 4. Suite Breakdown

### 4.1 Unit Tests (`Ticket.Tests/Unit`)
- **Validators:** `TicketCreateRequestValidatorTests`, `TicketQueryParametersValidatorTests` verify all boundary conditions (title length, pagination bounds, date-range assertions).
- **Domain Rules:** `TicketStatusTransitionRulesTests` enforces the status transition matrix.
- **Additional room:** Add more classes as business logic grows (e.g., service-level pure functions).

### 4.2 Integration Tests (`Ticket.Tests/Integration/TicketApiTests.cs`)
Scenarios covered:
1. **Ticket lifecycle:** Create → update → status progress (New → InProgress → Resolved) → search filtering. Ensures history is appended, RowVersion concurrency works, and filtering by status returns the ticket.
2. **Concurrency:** Simulates a stale `If-Match` header to assert 409 Conflict responses.
3. **Reporting:** Creates tickets across categories and checks the `/reports/summary` output.
The entire ASP.NET Core pipeline is used: Middleware, filters, services, repositories, FluentValidation, AutoMapper, and logging.

### 4.3 Security Tests (`Ticket.Tests/Security/SecurityHardeningTests.cs`)
- **API Key Enforcement:** POST `/categories` without `X-API-Key` returns 401.
- **XSS Sanitization:** Creating a ticket with `<script>` in the description ensures the persisted string is not equal to raw HTML and does not contain `<script>`.
- **Rate Limiting:** Burst of POST `/tickets` requests triggers at least one 429 response when limit is exceeded.
- **SQL Injection Guards:** Running `/tickets?SearchTerm=' OR 1=1;--` returns 200, proving query builder is safe.

> Feel free to add more security probes (CSRF, oversized payloads, log redaction) as requirements evolve.

---

## 5. Running the Suites

```bash
dotnet test Ticket.Tests/Ticket.Tests.csproj
```

Selective runs:
```bash
dotnet test Ticket.Tests/Ticket.Tests.csproj --filter Ticket.Tests.Integration.TicketApiTests.TicketLifecycle_Should_Create_Update_Status_And_Filter
dotnet test Ticket.Tests/Ticket.Tests.csproj --filter Ticket.Tests.Security.SecurityHardeningTests.RateLimiter_ShouldThrottleMutations
```

All commands stay inside the repo root. No additional services need to be launched because the factory uses an in-memory provider.

---

## 6. Test Data Guarantees & Isolation Strategy

- **Database Safety:** Tests never hit LocalDB/SQL Server. The factory removes existing `DbContextOptions<ApplicationDbContext>` and replaces it with `UseInMemoryDatabase`. The in-memory store name is randomized per factory so parallel test classes won’t clash.
- **RowVersion Emulation:** Because InMemory DB does not generate rowversion columns, `ApplicationDbContext` stamps `RowVersion` with GUID bytes on every save. This keeps optimistic concurrency tests realistic even without SQL Server.
- **Reset Strategy:** `EnsureDeleted` + `EnsureCreated` inside `ResetStateAsync` ensures global test state does not leak, satisfying the “no real data touched” rule (Test Constitution clause #2).

---

## 7. How To Extend

1. **New Feature?** Start by writing at least one integration test describing the intended behavior; let it fail, then implement the feature. Follow up with unit tests for edge-case logic.
2. **New Security Requirement?** Draft a new method inside `SecurityHardeningTests` (or create a dedicated class) and configure the factory if special knobs are needed (e.g., custom headers).
3. **Complex Workflow?** Break down into smaller integration tests, each covering a specific branch. Consider using Bogus builders for large payloads while keeping deterministic seeds.
4. **Performance/Regression Tests?** Create a separate xUnit collection or move into a distinct project/pipeline so the fast feedback property is preserved.

---

Armed with this playbook, a zero-context reader can reconstruct the entire testing philosophy of the project and add new tests without guessing the conventions.
