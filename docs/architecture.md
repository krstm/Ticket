# Ticket Platform - Architecture & Operations Guide

This guide is intentionally exhaustive so a future engineer (or another LLM with zero repo context) can reconstruct the entire backend by reading it alone. It documents intent, folder responsibilities, data modelling, MediatR domain events, paging/search behaviours, security posture, and operational knobs.

---

## 1. Product Intent & Scope

1. Public-facing ticket intake for requesters that do **not** authenticate yet (requester/recipient metadata is supplied manually for now).
2. Workspace for counterpart teams to triage, filter, and report on tickets, including category management and reporting dashboards.
3. Enterprise guardrails from day one: centralized logging, FluentValidation on every DTO, HTML sanitization, rate limiting, API-key protection for admin routes, and RFC-7807 error envelopes.
4. A modern Razor shell (Tailwind + Alpine) now ships alongside the JSON APIs: timeline, ticket grid/detail, category admin, and insights dashboards all live inside the same MVC project. Future mail/identity features must still be pluggable without rewriting core services.

---

## 2. Directory Layout Cheat-Sheet

| Path | Purpose / Notes |
| --- | --- |
| Controllers/ | Request boundaries (TicketsController, CategoriesController, ReportsController, HomeController, TicketUiController, CategoryUiController, ReportsUiController). JSON APIs coexist with the Razor experiences. |
| Domain/Entities | EF Core entities (Ticket, Category, TicketHistory, BaseEntity). BaseEntity now raises DomainEvents so MediatR handlers react after SaveChanges. |
| Domain/ValueObjects | Owned types (TicketContactInfo, TicketMetadata) stored inline with the Ticket row. |
| Domain/Enums & Rules | Enum definitions plus TicketStatusTransitionRules and TicketSearchScope. |
| Domain/Support | Helpers such as SearchNormalizer. |
| Domain/EventHandlers | MediatR handlers (TicketHistoryHandler, AuditLogHandler, NotificationPlaceholderHandler). |
| Domain/Events | Domain event records (TicketCreatedEvent, TicketStatusChangedEvent, TicketResolvedEvent). |
| DTOs/Requests|Responses|ViewModels | Wire formats for controllers, API responses, Razor view models, and pagination wrappers (PagedResult<T> with NextPageToken). |
| Interfaces/Services | Service contracts (ITicketService, ICategoryService, IReportingService, INotificationService). |
| Interfaces/Infrastructure | Cross-cutting abstractions (IClock, IContentSanitizer, IApiKeyValidator). |
| Services/ | Business services (TicketService, CategoryService, ReportingService, NotificationPlaceholderService, SystemClock, HtmlContentSanitizer). |
| Data/ApplicationDbContext | EF Core context with row-version emulation, soft-delete filters, normalized column configuration, and domain-event dispatch in SaveChangesAsync. |
| Data/Querying | TicketQueryExtensions + TicketPageToken helpers for filtering/sorting/paging/projections. |
| Configuration/ | Options (ApiKeyOptions, RateLimitingOptions, NotificationOptions). |
| Extensions/ | DI + pipeline wiring (ServiceCollectionExtensions, ApplicationBuilderExtensions). |
| Middleware/, Filters/, ModelBinding/ | Correlation IDs, RFC-7807 exception handling, API-key filter, model trimming, validation filter. |
| Mapping/ | AutoMapper profiles that project entities to DTOs/view models. |
| Views/ | Razor pages powering the timeline, modern ticket list/detail forms, categories UI, and the analytics dashboard. |
| Frontend/ | Tailwind/Alpine/Vite source (JS entry under `Frontend/src/main.js`, shared styles, and component helpers). Treat this as the design-system workspace. |
| package.json / vite.config.js / tailwind.config.js | NPM manifest + build tooling. `npm run build` emits `wwwroot/dist/main.iife.js` + `main.css`, which `_Layout.cshtml` references. |
| wwwroot/ | Static output (Vite bundle in `dist/` plus legacy assets). |
| docs/ | This architecture guide plus testing.md (single source of truth for new contributors/LLMs). |
| Ticket.Tests/ | Unit/integration/security suites (see docs/testing.md). Includes CustomWebApplicationFactory + TestNotificationService. |


---

## 3. Scale, Data-Access & Query Strategy

- **Workload assumptions:** Approximately 5k tickets/year on a single SQL Server. No replicas or sharding, but all timestamps are stored in UTC for future geo scenarios.
- **Repository/UoW removal:** Prior repositories leaked EF-specific flags (sTracking, includeHistory) and added boilerplate. Services now accept ApplicationDbContext directly, keeping EF features explicit while the shared logic lives inside Data/Querying/TicketQueryExtensions.
- **Normalized search columns:** At write time, TicketService populates TitleNormalized, DescriptionNormalized, contact/email normalized fields, and ReferenceCodeNormalized using SearchNormalizer. EF indexes those columns so case-insensitive LIKE filters reuse B-Tree indexes without calling .ToLower().
- **Filtering helpers:** TicketQueryExtensions.ApplyFilters/ApplySorting/ProjectToSummary encapsulate LINQ expressions so every controller/service uses exactly the same predicate definitions. This avoids duplicating Contains logic and makes search hardening testable.
- **Keyset + offset pagination:** Offset paging (page/pageSize) still exists for backwards compatibility and returns TotalCount. When clients pass a PageToken, the service enforces SortBy=CreatedAt DESC, uses the encoded token (Base64("{timestamp}|{servedWithinTimestamp}|{ticketId}")) to skip already-served rows, and returns a new NextPageToken. Tokens never expose raw IDsâ€”they are opaque strings safe to log and expire naturally when data changes.
- **Why no CQRS/MediatR pipelines for queries?** For this scale, bespoke query handlers would be ceremony. Instead, domain events are reserved for side effects (history/audit/notifications) while read paths stay synchronous and easy to debug.
- **Staying keyset-ready:** Even though keyset paging still hits the same table, the TicketPageToken format records how many rows with the final timestamp were already served. That prevents duplicates when many tickets share identical CreatedAtUtc values without needing GUID comparisons (which SQL Server cannot order efficiently in EF LINQ).
- **Future search/messaging headroom:** The domain-event pipeline already emits TicketCreatedEvent, TicketStatusChangedEvent, and TicketResolvedEvent. Adding mail/webhook handlers later will be a matter of registering new INotificationHandler<T> implementationsâ€”no changes to TicketService needed.

---

## 4. Data Model & Persistence Highlights

### 4.1 Entities & Columns
- **BaseEntity**: Id, CreatedAtUtc, UpdatedAtUtc, IsDeleted, RowVersion, plus a List<IDomainEvent> used by the MediatR dispatcher.
- **Ticket**: Core fields plus normalized columns and owned value objects. Relationships: Ticket -> Category (many-to-one), Ticket -> TicketHistory (one-to-many). Soft-deleted tickets are filtered automatically.
- **Category**: Case-insensitive unique Name, optional Description, IsActive. Used by reporting/grouping.
- **TicketHistory**: Immutable audit rows written by the MediatR TicketHistoryHandler. It references the parent ticket but does not raise events (prevents recursion).

### 4.2 ApplicationDbContext
- Configures normalized columns, indexes (CreatedAtUtc, Status, Priority, CategoryId, normalized fields), and row-version behaviour. SQLite/InMemory providers emulate rowversion with GUID bytes.
- Overrides SaveChangesAsync to:
  1. Emulate rowversion if the provider lacks it.
  2. Collect pending domain events from tracked BaseEntity instances.
  3. Clear the entity event lists.
  4. Publish events through MediatR **after** the commit so handlers only run on successful saves.
- TicketHistories has a query filter so fetching history for soft-deleted tickets automatically hides them.

---

## 5. Request Lifecycle & Domain Events

1. **Pipeline:** UseSerilogRequestLogging â€º CorrelationIdMiddleware â€º ExceptionHandlingMiddleware â€º HTTPS/static files â€º routing â€º fixed-window rate limiting (global + "mutations") â€º authorization.
2. **Validation:** [ApiController] + FluentValidation means every DTO gets automatic validation responses. TrimModelBinder removes whitespace-only payloads.
3. **Controllers:** Slimâ€”each controller calls the relevant service and returns DTOs. Status-specific behaviour (e.g., If-Match header parsing) lives inside controllers so services remain HTTP-agnostic.
4. **Services:**
   - TicketService performs sanitization, validation, normalization, and row-version checks before issuing commands. It raises domain events instead of mutating history/log tables directly.
   - CategoryService enforces uniqueness and active-state rules directly via _context queries.
   - ReportingService runs LINQ to group by status/category/priority and supports day/week intervals.
5. **Domain Event Handlers:**
   - TicketHistoryHandler inserts TicketHistory rows (creation + status changes) using the same ApplicationDbContext scope.
   - AuditLogHandler logs structured audit entries (action, ticket id, statuses, actors).
   - NotificationPlaceholderHandler forwards events to INotificationService (currently a logging stub configured via NotificationOptions). Tests override this with TestNotificationService to assert events fired without hitting external systems.

---

## 6. Service Responsibilities & Helpers

### TicketService
| Method | Responsibilities |
| --- | --- |
| SearchAsync | Builds a composable query via TicketQueryExtensions, projects directly to TicketSummaryDto, and either runs offset paging (with TotalCount) or keyset paging (using PageToken + TicketPageToken). Invalid tokens or unsupported sort orders return 400. |
| GetAsync | No-tracking load of a ticket, category, and history. History ordering happens inside AutoMapper (descending by OccurredAtUtc). |
| CreateAsync | Sanitizes description, normalizes fields, ensures category is active, sets timestamps from IClock, raises TicketCreatedEvent, and lets event handlers write history/log/notifications. |
| UpdateAsync | Row-version guard, sanitization, normalization, timestamp updates. Does **not** touch historiesâ€”only domain events do. |
| UpdateStatusAsync | Checks transition matrix, emits TicketStatusChangedEvent and (if resolving) TicketResolvedEvent, letting handlers append history and send notifications. |
| Helpers | ApplyNormalization, MapContact, SanitizeDescription, SanitizeOptional, EnsureCategoryExists, ParsePageToken, SkipPreviouslyServed. |

### CategoryService
- Query _context.Categories directly and uses _clock for timestamps.
- Rejects deactivation when tickets still reference the category via _context.Tickets.IgnoreQueryFilters().

### ReportingService
- Aggregates tickets with AsNoTracking() queries, taking date ranges from ReportQuery. Day/week alignment handled via helper method.

### TicketQueryExtensions
- ApplyFilters â€” centralizes search terms, category/status/priority filters, requester/recipient matches, created/due date ranges, and TicketSearchScope semantics (Title-only vs full content).
- ApplySorting â€” whitelisted sort expressions (CreatedAt, Priority, Status, CategoryName, DueAt) with deterministic tie-breakers (Id).
- ProjectToSummary â€” projection expression so EF performs server-side DTO shaping (no extra AutoMapper call on search).

### TicketPageToken
- Encodes {timestamp}|{servedWithinTimestamp}|{ticketId} to Base64. servedWithinTimestamp counts how many rows with that timestamp already appeared so the next page can skip them even when multiple tickets share the same CreatedAtUtc value.

### NotificationPlaceholderService
- Reads NotificationOptions to decide whether to log stub notifications for create/resolve events. Production deployments will replace it with real mail/webhook services, while tests override it with TestNotificationService.

---

## 7. Security & Hardening

1. **Input validation:** FluentValidation enforces lengths, enums, pagination limits, and date ordering. Validators explicitly forbid using PageToken with page > 1.
2. **Sanitization:** HtmlContentSanitizer strips scripts/event attributes. Tests assert persisted data differs from raw XSS input.
3. **Rate limiting:** Global fixed window (default 100 req/min) plus a "mutations" policy applied to POST/PUT/PATCH/DELETE. Integration tests tighten the window to force deterministic 429s. This relies on the built-in ASP.NET Core rate-limiting middleware so no extra NuGet package is required.
4. **API keys:** CategoriesController uses ApiKeyAuthorizeAttribute to guard admin endpoints until real identity is added.
5. **Centralized errors:** RFC-7807 responses include correlation IDs, hide stack traces outside Development, and map known exception types (BadRequestException, ConflictException, NotFoundException).
6. **Transport + headers:** HTTPS redirection, HSTS (non-development), and space for future CSP/XFO headers.
7. **Guardrails proven by tests:** Security suite covers API keys, XSS sanitization, rate limiting, SQL-injection-style filters, and invalid page tokens.

---

## 8. Configuration & Operations

`json
{
  "Serilog": { "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"], ... },
  "ConnectionStrings": {
    "TicketDb": "Server=(localdb)\\MSSQLLocalDB;Database=TicketDb;..."
  },
  "RateLimiting": { "PermitLimit": 100, "WindowSeconds": 60, "QueueLimit": 0 },
  "ApiKeys": { "CategoryManagement": "SET-THIS-IN-SECRETS" },
  "Notifications": {
    "NotifyOnTicketCreated": false,
    "NotifyOnTicketResolved": false,
    "PreferredChannel": "log"
  }
}
`

- Local development uses LocalDB; integration tests swap to EF InMemory via CustomWebApplicationFactory.
- NotificationOptions can be toggled to dry-run notification pipelines without touching code.
- Logs land in Logs/log-*.txt; mount that folder when containerizing.

**Build & Run:**
1. dotnet restore
2. dotnet ef database update (if SQL Server is enabled)
3. dotnet run --project Ticket
4. Browse https://localhost:{port}/tickets or / for the timeline.

### 8.4 Frontend build workflow
- Run `npm install` (once) followed by `npm run dev` for live Tailwind/Alpine changes inside the Frontend/ workspace.
- `npm run build` compiles the Vite library entry (`Frontend/src/main.js`) into `wwwroot/dist/main.iife.js` + `main.css`, which `_Layout.cshtml` references via `asp-append-version`.
- TailwindCSS 3.4 + Vite 5 remain in place because the Tailwind 4/Vite 7 stacks would force a config rewrite plus Node 22. The only outstanding npm audit alert is the esbuild vulnerability fixed by Vite 7, so the upgrade is tracked but deferred until we are ready for that breaking change.

---

## 9. Endpoint Summary

| Method | Route | Notes |
| --- | --- | --- |
| GET /tickets | Accepts TicketQueryParameters (SearchTerm, SearchScope, filters, sort, Page/PageSize, or PageToken). Returns PagedResult<TicketSummaryDto> with optional NextPageToken. |
| GET /tickets/{id} | Returns TicketDetailsDto with contact info, metadata, and ordered history. |
| POST /tickets | Creates a ticket; body TicketCreateRequest. Emits TicketCreatedEvent. |
| PUT /tickets/{id} | Updates mutable fields; requires If-Match header (base64 rowversion). |
| PATCH /tickets/{id}/status | Status-only update. Validates transition matrix, requires If-Match. Emits TicketStatusChangedEvent (+TicketResolvedEvent when applicable). |
| GET /categories?includeInactive= | Lists categories, optionally including inactive ones. |
| POST /categories | API-key protected; creates new category. |
| PUT /categories/{id} | API-key protected; updates name/description/isActive. |
| DELETE /categories/{id} | API-key protected soft-deactivate (blocked if tickets exist). |
| POST /categories/{id}/reactivate | Reactivate a category. |
| GET /reports/summary | Groups tickets by category/status/priority within a date range. |
| GET /reports/trend | Status trends grouped by day or ISO week. |
| GET / | Timeline view; fetches the latest 20 tickets using ITicketService.SearchAsync. |

---

## 10. Future Enhancements & Guardrails

- **Identity & authorization:** API-key filter is a placeholder. Domain events already carry ChangedBy strings so plugging Identity later only changes the caller, not the workflow.
- **Mail/Webhooks:** Hook new MediatR handlers onto TicketCreatedEvent and TicketResolvedEvent. INotificationService already abstracts notification delivery.
- **Full-text search / analytics:** Swap the normalized-column strategy for SQL Server Full-Text Search or an external engine when volume justifies it. Documented token format ensures keyset paging survives the migration.
- **UI polish:** Razor view intentionally plain. When SPA requirements emerge, controllers already emit DTOs ready for a separate front-end.

With this architecture, every behaviour (validation, normalization, paging, domain events, logging, security, testing) is documented and traceable, enabling future contributorsâ€”or LLM copilotsâ€”to extend the platform safely.
