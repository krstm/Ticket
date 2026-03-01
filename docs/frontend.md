# Frontend & UI Companion Guide

This file is the zero-context briefing for the Razor + Vite experience. Hand it to another engineer (or LLM) and they will be able to build/rerender every view without browsing the repo.

## 1. Stack Overview
- **Razor layout (Views/Shared/_Layout.cshtml)** renders the responsive shell, pulls fonts, and loads the Vite bundle from wwwroot/dist/main.iife.js + main.css.
- **Vite 5 + Tailwind 3 + Alpine 3** live in Ticket/Frontend/. 
pm run dev boots Vite's dev server for rapid CSS/JS edits, while 
pm run build emits the production bundle.
- **Design tokens** are defined in 	ailwind.config.js (brand palette + Inter font stack). Frontend/src/css/index.css pulls Tailwind layers and includes component utilities (buttons, cards, nav links, badges).
- **JavaScript entry (Frontend/src/main.js)** registers Alpine globally and exposes helper factories. Notably 	icketDetailsPage drives the actor identity drawer, status modal, and comment form in the ticket details screen.

## 2. Build & Verification Workflow
1. 
pm install (first run).
2. 
pm run dev for live reloads. Razor views automatically reference /dist assets in Development thanks to _Layout.cshtml.
3. 
pm run build for production artifacts. The command writes to wwwroot/dist/ which is git-tracked so backend-only deployments still receive updated CSS/JS.
4. dotnet test remains the regression source of truth; UI assertions piggyback on integration/security tests which exercise the rendered HTML and APIs.

## 3. Actor Identity & Collaboration UI
- The ticket details page uses x-data="ticketDetailsPage(...)". The component keeps a ctor object in localStorage (name, email, actorType: Requester or DepartmentMember).
- updateStatus() and submitComment() read that actor, send the payload expected by the backend (TicketActorContextDto), and refresh the page when the server replies.
- The "Your Identity" sidebar card edits the same data. Clicking “Remember details” invokes persistActor() so subsequent requests stay authorized even without real authentication.
- Comments are rendered via Model.Comments and appended by the AJAX form. They are sanitized on the server; the UI simply reflects the safe body.

## 4. Navigation & Pages
- **Timeline (Views/Home/Index.cshtml)**: chronological feed, now labels each card with its department.
- **Ticket list (Views/TicketUi/Index.cshtml)**: filter sidebar gained Category + Department selectors. X-data ensures new filters reset pagination tokens.
- **Ticket create (Views/TicketUi/Create.cshtml)**: the form requires Category + Department + Requester email before sending the POST body consumed by /tickets.
- **Ticket detail (Views/TicketUi/Details.cshtml)**: shows department roster, activity history, collaborative comment thread, quick actions (status modal), and identity card.
- **Department admin (Views/DepartmentUi/*)**: read-only list/detail screens for the new department entities. Mutations still flow through the JSON API for now.

## 5. Vite 7 / Tailwind 4 Migration Checklist
1. **Node 22 toolchain** – update local dev images/CI runners.
2. **Update dependencies** – raise ite to ^7.x, 	ailwindcss to ^4.x, @tailwindcss/forms/typography to their Tailwind 4-compatible versions, and switch PostCSS plugins to @tailwindcss/postcss.
3. **Config rewrite** – rename ite.config.js to .ts, migrate the Tailwind config to the new default export style, and drop legacy content globs in favor of Tailwind 4's content: ['./**/*.{cshtml,js}'] syntax.
4. **Bundle verification** – run 
pm run build, confirm wwwroot/dist only contains the new hashed files, and run the full dotnet test suite (integration tests exercise the Razor views to ensure the bundle loads).
5. **Security note** – the current Vite 5 lockfile inherits the esbuild advisory; upgrading to Vite 7 resolves it. Document any temporary audit suppressions inside this guide if the migration is delayed.

Armed with this guide, you can operate the UI without spelunking through the repo or relying on application context.
