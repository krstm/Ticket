# Ticket Platform - Architecture & Operations Guide

This guide is intentionally exhaustive so a future engineer (or another LLM with zero repo context) can reconstruct the entire backend by reading it alone. It documents intent, folder responsibilities, data modelling, MediatR domain events, paging/search behaviours, security posture, and operational knobs.

---

## 1. Product Intent & Scope

1. Public-facing ticket intake for requesters that do **not** authenticate yet (requester/recipient metadata is supplied manually for now).
2. Workspace for counterpart teams to triage, filter, and report on tickets, including category management and reporting dashboards.
3. Enterprise guardrails from day one: centralized logging, FluentValidation on every DTO, HTML sanitization, rate limiting, and RFC-7807 error envelopes (even though no temporary API-key/“shared secret” hacks exist).
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
| Domain/EventHandlers | MediatR handlers (TicketHistoryHandler, AuditLogHandler, NotificationDispatcherHandler). |
| Domain/Events | Domain event records (TicketCreatedEvent, TicketStatusChangedEvent, TicketResolvedEvent). |
| DTOs/Requests|Responses|ViewModels | Wire formats for controllers, API responses, Razor view models, and pagination wrappers (PagedResult<T> with NextPageToken). |
| Interfaces/Services | Service contracts (ITicketService, ICategoryService, IReportingService, INotificationService). |
| Interfaces/Infrastructure | Cross-cutting abstractions (IClock, IContentSanitizer). |
| Services/ | Business services (TicketService, CategoryService, ReportingService, NullNotificationService, SystemClock, HtmlContentSanitizer). |
| Data/ApplicationDbContext | EF Core context with row-version emulation, soft-delete filters, normalized column configuration, and domain-event dispatch in SaveChangesAsync. |
| Data/Querying | TicketQueryExtensions + TicketPageToken helpers for filtering/sorting/paging/projections. |
| Configuration/ | Options (RateLimitingOptions, NotificationOptions). |
| Extensions/ | DI + pipeline wiring (ServiceCollectionExtensions, ApplicationBuilderExtensions). |
| Middleware/, Filters/, ModelBinding/ | Correlation IDs, RFC-7807 exception handling, model trimming, validation filter. |
| Services/Mapping/ | Explicit mapper extensions that convert entities/value objects to DTOs without AutoMapper. |
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
   - NotificationDispatcherHandler simply forwards events to `INotificationService`. Production wiring uses the null implementation; tests override it with `TestNotificationService` to assert emails/webhooks would occur without hitting external systems.

---

## 6. Service Responsibilities & Helpers

### TicketService
| Method | Responsibilities |
| --- | --- |
| SearchAsync | Runs `TicketSearchPipeline`, which applies filters/sorts, projects summaries server-side, and executes either offset paging (with TotalCount) or keyset paging (PageToken + TicketPageToken). Invalid tokens or unsupported sort orders return 400. |
| GetAsync | No-tracking load of a ticket, category, history, comments, and department roster; mapping extensions ensure history/comments are sorted descending before returning. |
| CreateAsync | Sanitizes description, normalizes fields, ensures category is active, sets timestamps from IClock, raises TicketCreatedEvent, and lets event handlers write history/log/notifications. |
| UpdateAsync | Row-version guard, sanitization, normalization, timestamp updates. Does **not** touch historiesâ€”only domain events do. |
| UpdateStatusAsync | Checks transition matrix, emits TicketStatusChangedEvent and (if resolving) TicketResolvedEvent, letting handlers append history and send notifications. |
| Helpers | `TicketMutationHelper` (sanitization + normalization), `TicketCommentHelper` (participant-only comment creation), `TicketSearchPipeline` (paging/filter orchestration), `TicketDomainEventFactory` (centralized creation of domain events). |

### CategoryService
- Query _context.Categories directly and uses _clock for timestamps.
- Rejects deactivation when tickets still reference the category via _context.Tickets.IgnoreQueryFilters().

### ReportingService
- Aggregates tickets with AsNoTracking() queries, taking date ranges from ReportQuery. Day/week alignment handled via helper method.

### TicketQueryExtensions
- ApplyFilters â€” centralizes search terms, category/status/priority filters, requester/recipient matches, created/due date ranges, and TicketSearchScope semantics (Title-only vs full content).
- ApplySorting â€” whitelisted sort expressions (CreatedAt, Priority, Status, CategoryName, DueAt) with deterministic tie-breakers (Id).
- ProjectToSummary â€” projection expression so EF performs server-side DTO shaping; detail views rely on the manual mapping extensions in `Services/Mapping`.

### TicketPageToken
- Encodes {timestamp}|{servedWithinTimestamp}|{ticketId} to Base64. servedWithinTimestamp counts how many rows with that timestamp already appeared so the next page can skip them even when multiple tickets share the same CreatedAtUtc value.

### NullNotificationService
- Provides a no-op implementation today so the rest of the pipeline stays side-effect free. Production deployments will swap in real SMTP/webhook transports, while tests override it with TestNotificationService.

---

## 7. Security & Hardening

1. **Input validation:** FluentValidation enforces lengths, enums, pagination limits, and date ordering. Validators explicitly forbid using PageToken with page > 1.
2. **Sanitization:** HtmlContentSanitizer strips scripts/event attributes. Tests assert persisted data differs from raw XSS input.
3. **Rate limiting:** Global fixed window (default 100 req/min) plus a "mutations" policy applied to POST/PUT/PATCH/DELETE. Integration tests tighten the window to force deterministic 429s. This relies on the built-in ASP.NET Core rate-limiting middleware so no extra NuGet package is required.
4. **Centralized errors:** RFC-7807 responses include correlation IDs, hide stack traces outside Development, and map known exception types (BadRequestException, ConflictException, NotFoundException).
5. **Identity readiness:** No temporary API keys or client-side hacks are in use. Instead, controllers accept unauthenticated traffic today while services, validators, and domain events already expose the seams (`TicketActorContext`, notification hooks) where a proper identity provider will plug in later.
6. **Transport + headers:** HTTPS redirection, HSTS (non-development), and space for future CSP/XFO headers.
7. **Guardrails proven by tests:** Security suite covers XSS sanitization, rate limiting, SQL-injection-style filters, invalid page tokens, and department-only mutations via actor contexts.
8. **Release checklist:** Before every deployment run `dotnet list Ticket/Ticket.csproj package --vulnerable`, `npm audit --json`, and the `rg` sweeps listed in docs/testing.md §9. Capture the outputs in release notes; deployments block on any non-empty vulnerability report.

---

## 8. Configuration & Operations

`json
{
  "Serilog": { "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"], ... },
  "ConnectionStrings": {
    "TicketDb": "Server=(localdb)\\MSSQLLocalDB;Database=TicketDb;..."
  },
  "RateLimiting": { "PermitLimit": 100, "WindowSeconds": 60, "QueueLimit": 0 },
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
- TailwindCSS 4 + Vite 7 are now active. The build uses the `@tailwindcss/vite` plugin and the new CSS-first `@theme` configuration. Node 22 is required for this toolchain.
- The esbuild advisory is resolved by the Vite 7 upgrade.

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
| POST /categories | Creates new category (currently open until Identity policies arrive; every mutation is logged). |
| PUT /categories/{id} | Updates name/description/isActive (identity enforcement arrives with the auth work). |
| DELETE /categories/{id} | Soft-deactivate (blocked if tickets exist). |
| POST /categories/{id}/reactivate | Reactivate a category. |
| GET /reports/summary | Groups tickets by category/status/priority within a date range. |
| GET /reports/trend | Status trends grouped by day or ISO week. |
| GET / | Timeline view; fetches the latest 20 tickets using ITicketService.SearchAsync. |

---

## 10. Future Enhancements & Guardrails

- **Identity & authorization:** No temporary API-key or shared-secret filter exists. Domain events already carry ChangedBy strings so plugging Identity later only changes the caller, not the workflow.
- **Mail/Webhooks:** Hook new MediatR handlers onto TicketCreatedEvent and TicketResolvedEvent. INotificationService already abstracts notification delivery.
- **Full-text search / analytics:** Swap the normalized-column strategy for SQL Server Full-Text Search or an external engine when volume justifies it. Documented token format ensures keyset paging survives the migration.
- **UI polish:** Razor view intentionally plain. When SPA requirements emerge, controllers already emit DTOs ready for a separate front-end.

With this architecture, every behaviour (validation, normalization, paging, domain events, logging, security, testing) is documented and traceable, enabling future contributorsâ€”or LLM copilotsâ€”to extend the platform safely.
---

## 11. Department Collaboration Model

**Data additions**
- Departments (Departments + DepartmentMembers tables) capture the target audience for every ticket. Members enforce uniqueness per department and store notification preferences. Ticket.DepartmentId is mandatory and normalized (DepartmentNameNormalized) to accelerate filters.
- Ticket comments (TicketComments table) are append-only, sanitized, and tied to TicketCommentSource (Requester vs DepartmentMember). The ticket row keeps a LastCommentAtUtc pointer so UI timelines can prioritize threads without extra joins.

**Actor enforcement**
- Every update/status/comment request carries a TicketActorContext (name, email, actor type). The TicketAccessEvaluator verifies that:
  - Description changes can only be made by the original requester.
  - Status updates and comment posts are restricted to the requester or active department members.
  - Unauthorized attempts throw ForbiddenException, exercised in both integration and security suites.
- TicketActorContext instances are trimmed/sanitized by model binders; validators ensure non-empty names, RFC-compliant emails, and enum bounds.

**Domain events + notifications**
- TicketCreatedEvent, TicketStatusChangedEvent, TicketResolvedEvent, and the new TicketCommentAddedEvent now carry department metadata plus the active recipient snapshot. Serilog-based handlers log structured audits and stub notifications that include concrete recipient lists (proving future SMTP/webhook handlers have all context they need).
- History entries are materialized from events (creation, status changes, comments) so the immutable audit trail is still accurate even after the repository/UoW removal.

**Services & controllers**
- DepartmentService owns CRUD/member synchronization logic, exposed via /departments JSON APIs and /ui/departments Razor views. These remain open for now; once Identity lands we will bolt policies onto the existing controllers.
- TicketService depends on TicketAccessEvaluator to keep business logic readable; new methods (AddCommentAsync, GetCommentsAsync) back /tickets/{id}/comments endpoints and UI forms.
- Reporting gained department filters and grouping: /reports/summary?groupBy=department and /reports/summary?DepartmentIds=... reuse the same normalized columns that power ticket search.

**UI faceting**
- The navigation now surfaces Departments. Ticket list filters gained a Department dropdown and result cards show the owning department badge.
- Ticket details render department rosters, the comment feed, and a "Your Identity" card that binds directly to Alpine state (no client-side persistence). Users declare whether they are the requester or a department member per action until authentication lands.

---

## 12. Frontend Stack & Migration Plan

**Current stack snapshot**
- Razor views orchestrate layout, while a lightweight Vite 7 + Tailwind 4 + Alpine 3 bundle (Frontend/) provides shared CSS tokens and Alpine helpers for actor capture, filtering, and comment posting.
- The build emits `wwwroot/dist/main.iife.js` + `main.css`, referenced from `_Layout.cshtml` with cache busting. `npm run dev` enables HMR inside the MVC project; `npm run build` runs in CI before publishing.

**Actor-aware UX**
- `ticketDetailsPage` centralizes the actor identity form, status updates, and comment submission so each request includes the correct `TicketActorContext` payload without client-side storage.
- Comment panels and filters are intentionally minimal (plain forms/tables) because the backend is the priority; Alpine helpers explain how to evolve the UI later.

**Vite 7 / Tailwind 4 migration strategy**
1. **Prerequisites:** upgraded to Node v22.13.0.
2. **Config rewrite:** Migrated to Vite 7 + Tailwind 4. Replaced `tailwind.config.js` and `postcss.config.js` with CSS-native `@theme` variables in `index.css`.
3. **esbuild advisory mitigation:** Resolved by upgrading to Vite 7.
4. **Validation plan:** Build verified; integration tests pass.

With the documentation + helpers above, a future engineer (or LLM) can recreate the entire collaboration model end-to-end without additional tribal knowledge.
## 13. Lean Core Refactor Notes

- **AutoMapper removal:** DTO shaping now lives in the explicit helpers under `Services/Mapping`. They keep projections obvious, eliminate reflection, and make DTO/test alignment transparent.
- **Helper-focused services:** `TicketSearchPipeline`, `TicketMutationHelper`, and `TicketCommentHelper` encapsulate pagination math, sanitization/normalization, and comment construction so `TicketService` reads top-to-bottom like a workflow while still sharing the hard bits across methods.
- **Domain events as the seam:** `TicketDomainEventFactory` is the sole place tickets raise events. MediatR handlers (history, audit, notification dispatch) remain the only components that touch secondary tables or integrations, so future SMTP/webhook work plugs in without touching services.
- **Auth placeholders purged:** API-key filters and client-side identity shims were intentionally deleted. Controllers stay open until real Identity policies arrive, but `TicketActorContext` + `TicketAccessEvaluator` already enforce participant-only changes even without authentication.
- **Null notification default:** Production registers `NullNotificationService`, a true no-op. Tests swap in `TestNotificationService` to assert recipients. When the real notifier lands, replace the DI binding without modifying business logic.


