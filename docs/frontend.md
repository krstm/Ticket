# Frontend & UI Companion Guide

This file is the zero-context briefing for the Razor + Vite experience. Hand it to another engineer (or LLM) and they will be able to build/rerender every view without browsing the repo.

## 1. Stack Overview
- **Layout**: `Views/Shared/_Layout.cshtml` renders the responsive shell, self-hosts the Inter font family from `/fonts/inter`, and loads `wwwroot/dist/main.iife.js + main.css`. No CDN links remain, which lets the new CSP lock scripts/styles to `'self'`.
- **Tooling**: Vite 7 + Tailwind 4 + Alpine 3 live under `Ticket/Frontend/`. ESLint 10 keeps Alpine helpers honest, DOMPurify provides last-mile sanitization, and Chart.js is bundled locally for the insights dashboard.
- **Design system**: Tailwind 4 uses the new CSS-first engine. Variables and theme tokens are defined directly in `Frontend/src/css/index.css` via the `@theme` block alongside the `@font-face` declarations for Inter.
- **JavaScript entry**: `Frontend/src/main.js` registers Alpine, exports helper factories (e.g., `ticketDetailsPage`, `initReportsDashboard`), wires DOMPurify’s `sanitizeInput`, and exposes `ticketActorContext` globally so Razor/Alpine snippets can reuse the actor snapshot.

## 2. Build & Verification Workflow
1. `npm install` (first run) or `npm ci` in CI.
2. `npm run dev` for live reloads inside MVC.
3. `npm run lint` (ESLint strict mode, zero warnings) and `npm run check:audit` (prod-only audit) run locally **and** in CI before every merge.
4. `npm run build` to refresh `wwwroot/dist/` before committing or publishing (Vite outputs `main.iife.js` + `main.css`).
5. `npm run test:e2e` executes the Playwright security smoke tests (no third-party requests, sanitized rendering, actor guard).
6. `dotnet test` remains the backend source of truth; integration/security suites render the Razor views and hit the JSON APIs, so run it after every bundle refresh.

## 3. Actor Identity & Collaboration UI
- `ticketActorContext` wraps a shared Alpine store. `ticketDetailsPage` references the store so every widget (status modal, comments, quick actions) enforces the same identity gating.
- `sanitizeInput` (DOMPurify with zero allowed tags/attrs) runs on every comment/status payload, ensuring the backend receives already-sanitized data **before** HtmlContentSanitizer double-checks it.
- `updateStatus()` and `submitComment()` serialize the actor snapshot into the `TicketActorContextDto` expected by the backend (with `credentials: 'same-origin'`, CSRF-friendly headers, If-Match row versions, and JSON Accept headers).
- The “Your Identity” sidebar still binds inputs to the Alpine state—no local storage, cookies, or tokens. Authentication will eventually set `ticketActorContext.state` centrally.
- Comments render safely because the backend stores encoded bodies; the security + Playwright suites assert `<script>` payloads never survive round trips.

## 4. Navigation & Pages
- **Timeline (Views/Home/Index)**: chronological feed with department badges.
- **Ticket grid (Views/TicketUi/Index)**: filter sidebar includes Keyword, Category, Status, Priority, and Department selectors; adjusting filters resets pagination tokens via Alpine.
- **Ticket create (Views/TicketUi/Create)**: requires Title, Category, Department, Requester name/email, priority, and description.
- **Ticket detail (Views/TicketUi/Details)**: shows department roster, requester/contact metadata, history, status shortcuts, identity form, and threaded comments. Alpine actions now call the shared `ticketActorContext` helpers so they fail fast when actor info is missing.
- **Department admin (Views/DepartmentUi/*)**: read-only list/detail for departments + members; actual CRUD flows through the /departments JSON API.
- **Reports dashboard (Views/ReportsUi/Index)**: outputs Base64-encoded JSON datasets. `initReportsDashboard` in `main.js` decodes them, instantiates Chart.js (line + doughnut), and never touches inline `<script>` tags.

## 5. Vite 7 / Tailwind 4 Migration Checklist
1. **Node 22** on local + CI.
2. **Upgrade deps**: `vite@^7`, `tailwindcss@^4`, matching `@tailwindcss/forms` / `@tailwindcss/typography`, switch PostCSS plugin to `@tailwindcss/postcss`.
3. **Config rewrite**: move to `vite.config.ts`, migrate Tailwind to the new default export, collapse content globs per Tailwind 4 guidance.
4. **Bundle verification**: `npm run build` (confirm new hashed files), then `dotnet test` to ensure MVC still serves the bundle.
5. **Security**: upgrading Vite pulls the patched esbuild resolving the current audit warning; document any interim suppressions if rollout is delayed.
- **Status**: Completed. Stack is now Vite 7 + Tailwind 4, with DOMPurify/Chart.js/Playwright integrated and lint/audit/test scripts wired into CI.
- **Verification**: `npm run lint`, `npm run check:audit`, `npm run build`, `npm run test:e2e`, and `dotnet test` all pass together before a PR merges.

Follow this guide to work on the UI without spelunking through the repo while staying within the CSP + no-CDN guardrails.
