# Ticket Platform – Architecture & Operations Guide

This document is intentionally verbose so that a developer (or another LLM with zero additional context) can reconstruct the entire project by reading it alone. It covers the application’s intent, every folder and artifact, the request lifecycle, key services/methods, data modeling, security posture, and operational guidance.

---

## 1. Product Intent & Scope

1. Ticket submission portal where unauthenticated users can raise requests (for now there is no identity module, so the caller supplies requester/recipient metadata manually).
2. Shared workspace for “counterpart” teams to triage, filter, and report on incoming tickets.
3. Enforce enterprise-grade logging, validation, sanitization, rate limiting, and structured error handling from day one.
4. Enable category management, timeline display, and reporting capabilities without relying on an external SPA (plain MVC views are enough for now).

> Everything lives inside a single ASP.NET Core MVC project (`Ticket/`), but the internal structure is layered to mimic a DDD/service-oriented design.

---

## 2. Directory Map & Responsibilities

| Path | Purpose / Notes |
| --- | --- |
| `Controllers/` | Request boundary. Includes `TicketsController` (CRUD+query), `CategoriesController` (API-key protected management), `ReportsController` (summary/trend endpoints), and `HomeController` (timeline view). All controllers return JSON except `HomeController`, which renders a Razor table. |
| `Domain/Entities` | EF Core entities: `Ticket` (aggregate root), `Category`, `TicketHistory`, `BaseEntity` (Id, timestamps, soft-delete, RowVersion). |
| `Domain/ValueObjects` | Immutable owned types: `TicketContactInfo` and `TicketMetadata`. Stored inside `Ticket` as owned components. |
| `Domain/Enums` | Priority/status/sort/report enums. `TicketStatus` drives workflow, `TicketPriority` drives urgency, `TicketSortBy` and `SortDirection` drive query ordering, `ReportGroupBy`/`ReportInterval` control reporting. |
| `Domain/Rules` | `TicketStatusTransitionRules` is the single source of truth for allowed status transitions (e.g., `Resolved → Closed` allowed, `Resolved → New` forbidden). |
| `DTOs/Requests` | Shapes the HTTP request payloads: `TicketCreateRequest`, `TicketUpdateRequest`, `TicketStatusUpdateRequest`, `TicketQueryParameters`, `CategoryCreate/UpdateRequest`, `ReportQuery`. |
| `DTOs/Responses` | API responses: `PagedResult<T>`, `TicketSummaryDto`, `TicketDetailsDto` (+ history/contact info), `CategoryDto`, `ReportBucketDto`. |
| `DTOs/ViewModels` | Only `TimelineItemViewModel` for the Home page (chronological list). |
| `Interfaces/` | Abstractions for repositories (`ITicketRepository`, `ICategoryRepository`, `IUnitOfWork`) and services (`ITicketService`, `ICategoryService`, `IReportingService`, `IAuditLogService`) plus infra contracts (`IClock`, `IContentSanitizer`, `IApiKeyValidator`). |
| `Services/` | Implementation of business workflows: `TicketService`, `CategoryService`, `ReportingService`, `AuditLogService`, `SystemClock`, `HtmlContentSanitizer`, `ApiKeyValidator`. |
| `Repositories/` | EF Core data access wrappers. `TicketRepository` owns filtering/sorting projections; `CategoryRepository` handles uniqueness + activity toggles; `UnitOfWork` forward `SaveChangesAsync`. |
| `Validators/` | FluentValidation rules ensuring every inbound DTO is correct (length constraints, enum combos, pagination limits, etc.). |
| `Middleware/` | `CorrelationIdMiddleware` (injects `X-Correlation-Id` header + logging scope) and `ExceptionHandlingMiddleware` (catches all exceptions and returns RFC-7807 `ProblemDetails`). |
| `Filters/` | `ValidateModelFilter` (short-circuits invalid model state) and `ApiKeyAuthorizeAttribute` (simple API-key gate for category management). |
| `ModelBinding/` | `TrimmingModelBinder` & provider – trims all incoming strings and converts empty strings to `null`. |
| `Extensions/` | `ServiceCollectionExtensions` wires DI, AutoMapper, FluentValidation, rate limiting, EF Core; `ApplicationBuilderExtensions` arranges middleware order and the default route. |
| `Configuration/` | Options POCOs used with `IOptions<T>`: `ApiKeyOptions`, `RateLimitingOptions`. |
| `Mapping/` | AutoMapper profile mapping Entities ⇄ DTOs/ViewModels (includes history ordering). |
| `Data/` | `ApplicationDbContext` (with provider-aware row-version emulation for SQLite/InMemory), `DesignTimeDbContextFactory`, and EF migrations in `Data/Migrations/`. |
| `Views/` | Minimal Razor views; currently the Home timeline uses a table to list latest tickets. |
| `wwwroot/` | Placeholder for static content (empty for now). |
| `docs/` | Architecture and testing guides (this file + `testing.md`). Keep these updated as the single source of truth for new contributors or LLM agents. |
| `Ticket.Tests/` | Separate test project (see `docs/testing.md` for detailed breakdown). |

---

## 3. Data Model & Persistence Details

### 3.1 Entities
- **BaseEntity**: `Guid Id`, `DateTimeOffset CreatedAtUtc/UpdatedAtUtc`, `bool IsDeleted`, `byte[] RowVersion`. RowVersion is stored as a SQL Server rowversion when available; on SQLite/InMemory tests it is emulated via GUID bytes (see `ApplicationDbContext.ApplyEmulatedRowVersions`).
- **Ticket**: Title, Description, `TicketPriority`, `TicketStatus`, `int CategoryId`, request/recipient contact info, metadata flags, optional `DueAtUtc`, optional `ReferenceCode`, `ICollection<TicketHistory> History`. Owned value objects ensure EF stores their columns inline.
- **Category**: Id, Name (unique, case-insensitive), Description, `IsActive`, timestamps. No rowversion because SQLite cannot auto-generate one and categories rarely conflict.
- **TicketHistory**: Immutable log per status/action change (Status, Action text, Note, ChangedBy, OccurredAtUtc).

### 3.2 DbContext Highlights
- Query filters exclude soft-deleted tickets.
- Indexes: ticket `CreatedAtUtc`, `CategoryId`, `Status`, `Priority`; category `Name` (unique) and `IsActive`; history `OccurredAtUtc`.
- Owned navigation configuration ensures contact info fields are renamed (e.g., `RequesterEmail` column).
- Provider-aware concurrency: `ConfigureTicket` only calls `.IsRowVersion()` when provider supports it.
- Save pipeline ensures RowVersion is refreshed for tracked entities even under InMemory (important for tests).

---

## 4. Request Lifecycle

1. **Hosting & Logging**: Program wires Serilog (console + rolling file) reading `appsettings.json`. When tests run, `WebApplicationFactory` uses InMemory DB.
2. **Middleware Order**:
   1. `UseSerilogRequestLogging`
   2. `CorrelationIdMiddleware` (adds header + logging scope)
   3. `ExceptionHandlingMiddleware` (wraps everything else)
   4. `UseHttpsRedirection` + static files
   5. `UseRouting`
   6. `UseRateLimiter`
   7. `UseAuthorization`
3. **Controllers**: `[ApiController]` on API controllers automatically produces typed `ProblemDetails`. The Home controller remains an MVC controller but consumes `ITicketService` to populate a timeline view model.
4. **Filters & Validation**:
   - `ValidateModelFilter` ensures modelstate errors are converted to 400 responses (AJAX-friendly).
   - `ApiKeyAuthorizeAttribute` runs before category mutations and compares `X-API-Key` header to config.
   - FluentValidation auto-registration validates every DTO (requests include nested validators for contact info).
5. **Services**:
   - Enforce domain rules (status transitions), sanitize body text, set timestamps (`IClock`), log via `ILogger`.
   - `TicketService` also writes to `TicketHistory` and delegates to `AuditLogService`.
6. **Repositories**:
   - Accept `TicketQueryParameters` and build LINQ expressions. Search functionality is case-insensitive and SQLite-friendly.
   - Sorting uses safe projections (ticks) so SQLite order-by limitations on `DateTimeOffset` do not break tests.
7. **Response**:
   - AutoMapper mapping + `PagedResult<T>`.
   - Errors: `ExceptionHandlingMiddleware` maps known exceptions (NotFound, Conflict, Validation, etc.) to correct status codes. Unknown errors become sanitized 500 responses (message hidden when not in Development).

---

## 5. Services & Method Cheat-Sheet

### `TicketService`
| Method | Core Responsibilities |
| --- | --- |
| `SearchAsync` | Accepts `TicketQueryParameters`, defers to repository, returns paged summaries. Enforces page size clamp (1–100). |
| `GetAsync` | Loads ticket + history; throws `NotFoundException` when missing. |
| `CreateAsync` | Sanitizes description, ensures category exists & active, sets status = `New`, appends history entry “Ticket created”, logs via `AuditLogService`. |
| `UpdateAsync` | Checks RowVersion, re-validates category, re-sanitizes, updates metadata/contact info, logs. |
| `UpdateStatusAsync` | Validates transition using `TicketStatusTransitionRules`, writes history entry with `ChangedBy` + optional note, updates RowVersion. |

### `CategoryService`
- `GetAllAsync(includeInactive)` returns sorted category list.
- `CreateAsync` ensures normalized uniqueness (case-insensitive) and marks category active.
- `UpdateAsync` toggles fields and active flag.
- `DeactivateAsync` refuses if any ticket references the category (`AnyInCategoryAsync`).
- `ReactivateAsync` switches `IsActive` back to true.

### `ReportingService`
- `GetSummaryAsync` groups tickets in date range by Category/Status/Priority and returns `ReportBucketDto`.
- `GetStatusTrendAsync` builds day/week buckets for status counts.

### `AuditLogService`
- Lightweight layer wrapping `ILogger`; called by `TicketService` to ensure every mutation is captured as structured logs.

### `TicketRepository`
- `SearchAsync` handles full-text search across multiple columns, filtering by categories/status/priority/dates/due range, as well as optional requester/recipient filters (case-insensitive).
- Sorting uses `TicketSortBy`. For `DueAt` it orders by `DueAtUtc.HasValue` + ticks to prevent SQLite from throwing when ordering by nullable `DateTimeOffset`.
- `GetByIdAsync` optionally tracks entity and includes history ordering.
- `AnyInCategoryAsync` helps the category service guard deactivation.

### `HtmlContentSanitizer`
- Because `Ganss.XSS` was unavailable in the offline package feeds, a custom sanitizer strips `<script>` blocks and event attributes, then HTML-encodes the remaining string (preserving user text but neutralizing markup). Validators ensure sanitized output isn’t empty.

---

## 6. Security & Compliance

1. **Input Validation**: FluentValidation for all DTOs, trimming binder to eliminate whitespace-only strings, explicit status transition guards.
2. **Sanitization**: `HtmlContentSanitizer` + double-check in validators -> description cannot become empty post-sanitization. Razor automatically HTML-encodes output (so timeline view is safe).
3. **Rate Limiting**:
   - Global limiter: `FixedWindowRateLimiter` keyed by Remote IP (defaults: 100 requests/minute).
   - “Mutations” limiter: used via `[EnableRateLimiting("mutations")]` for POST/PUT/PATCH/DELETE. Tests override settings (limit 2, queue 0) to assert throttling.
4. **API Key**: Category management endpoints require `X-API-Key` header matching `ApiKeys:CategoryManagement`. Replace with proper auth in the future.
5. **Centralized Errors**: Maps exceptions to HTTP codes and scrubs stack traces when not in Development. Logs include correlation ID + request metadata.
6. **Logging**: Serilog with `FromLogContext`, `WithMachineName`, `WithThreadId`, file sink storing 30 days of logs. `AuditLogService` adds semantic logs for domain events.
7. **Transport Security**: `UseHsts`, `UseHttpsRedirection`, and the pipeline is ready for extra headers (CSP, XFO) once requirements are known.

---

## 7. Configuration & Operations

### 7.1 AppSettings
```jsonc
{
  "Serilog": { /* console + file sinks */ },
  "ConnectionStrings": {
    "TicketDb": "Server=(localdb)\\MSSQLLocalDB;Database=TicketDb;..."
  },
  "RateLimiting": { "PermitLimit": 100, "WindowSeconds": 60, "QueueLimit": 0 },
  "ApiKeys": { "CategoryManagement": "SET-THIS-IN-SECRETS" }
}
```
- In tests, the factory overrides RateLimiting settings (queue=0, limit=2) and uses InMemory DB.
- In production, update `TicketDb` connection string and `ApiKeys` via user secrets or environment variables.

### 7.2 Build & Run
1. `dotnet restore`
2. `dotnet ef database update` (optional – only if using SQL Server)
3. `dotnet run --project Ticket`
4. Browse https://localhost:{port}/tickets etc., or GET `/` for timeline view.

### 7.3 Deployment Considerations
- Containerization is straightforward since the project is a single ASP.NET Core app.
- Logging folder `Logs/` is relative to the working directory; mount to persistent storage in production.
- Rate limiting + API key values should be tuned per environment.

---

## 8. Testing & Quality Mandates

- **Adversarial Suites:** Every new feature must ship with tests designed to break it (invalid payloads, stale row versions, malicious inputs, rate-limit abuse, etc.). “Happy path only” tests are forbidden.
- **Database Isolation:** Automated tests never touch a real database. `CustomWebApplicationFactory` replaces the DbContext with `UseInMemoryDatabase` so test runs cannot damage shared resources. If a new test introduces any external dependency, isolation must be preserved (e.g., using containers or mocks).
- **Docs as Contract:** Before onboarding a new subsystem or LLM, ensure `docs/architecture.md` and `docs/testing.md` reflect the latest flows and rules.

---

## 9. Endpoints Overview

| Method | Route | Description |
| --- | --- | --- |
| `GET /tickets` | Query using `TicketQueryParameters`. Supports search term, categories, statuses, priorities, date/due ranges, requester/recipient, sort field/direction, page/pageSize. Returns `PagedResult<TicketSummaryDto>`. |
| `GET /tickets/{id}` | Ticket details + history. |
| `POST /tickets` | Create new ticket. Body `TicketCreateRequest`. Returns `TicketDetailsDto`. |
| `PUT /tickets/{id}` | Update core fields. Requires `If-Match` header containing Base64 RowVersion. |
| `PATCH /tickets/{id}/status` | Update status only. Respects transition matrix. Requires `If-Match`. |
| `GET /categories?includeInactive=false` | List categories. |
| `POST /categories` | Requires `X-API-Key`. Creates new category. |
| `PUT /categories/{id}` | Requires API key. Updates name/description/isActive. |
| `DELETE /categories/{id}` | Soft-deactivates (only if no tickets exist). |
| `POST /categories/{id}/reactivate` | Reactivate. |
| `GET /reports/summary` | Query via `ReportQuery` (`from`, `to`, `groupBy`). |
| `GET /reports/trend` | Query via `ReportQuery` (`interval` controls day/week buckets). |
| `GET /` | Home timeline – shows latest 20 tickets sorted by `CreatedAtUtc DESC`. |

---

## 10. Future Enhancements
- Authentication & Authorization (replace API key guard with proper policies).
- Department dimension + assignment workflows once users exist.
- Rich UI/UX (component library, live updates, filters) once the service layer stabilizes.
- Notification/communication hooks (email/webhook) triggered from `TicketService`.

With this guide, a developer can open any file, understand its place in the system, and know how requests flow from entry point to database and back, including the strong logging/security envelope wrapped around every operation.
