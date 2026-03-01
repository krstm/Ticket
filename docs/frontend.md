# Frontend & UI Companion Guide

This file is the zero-context briefing for the Razor + Vite experience. Hand it to another engineer (or LLM) and they will be able to build/rerender every view without browsing the repo.

## 1. Stack Overview
- **Layout**: Views/Shared/_Layout.cshtml renders the responsive shell, pulls Inter from Google Fonts, and loads wwwroot/dist/main.iife.js + main.css.
- **Tooling**: Vite 7 + Tailwind 4 + Alpine 3 live under Ticket/Frontend/. 
- **Design system**: Tailwind 4 uses the new CSS-first engine. Variables and theme tokens are defined directly in `Frontend/src/css/index.css` via the `@theme` block.
- **JavaScript entry**: Frontend/src/main.js registers Alpine and exports helper factories—most notably 	icketDetailsPage, which powers the ticket detail interactions.

## 2. Build & Verification Workflow
1. 
pm install (first run).
2. 
pm run dev for live reloads inside MVC.
3. 
pm run build to refresh wwwroot/dist/ before committing or publishing.
4. dotnet test stays the single source of truth; integration/security suites render the Razor views and hit the JSON APIs.

## 3. Actor Identity & Collaboration UI
- 	icketDetailsPage exposes ctor fields (name, email, actorType) but **never persists them client-side**; users provide the info per action until real authentication ships.
- updateStatus() and submitComment() serialize that actor into the TicketActorContextDto expected by the backend so domain rules ("only requester/department members") stay enforceable.
- The "Your Identity" sidebar simply binds text inputs to the Alpine state—no LocalStorage, cookies, or temporary tokens.
- Comments render from Model.Comments (already sanitized server-side) and the form posts directly to /tickets/{id}/comments.

## 4. Navigation & Pages
- **Timeline (Views/Home/Index)**: chronological feed with department badges.
- **Ticket grid (Views/TicketUi/Index)**: filter sidebar includes Keyword, Category, Status, Priority, and Department selectors; adjusting filters resets pagination tokens via Alpine.
- **Ticket create (Views/TicketUi/Create)**: requires Title, Category, Department, Requester name/email, priority, and description.
- **Ticket detail (Views/TicketUi/Details)**: shows department roster, requester/contact metadata, history, status shortcuts, identity form, and threaded comments.
- **Department admin (Views/DepartmentUi/*)**: read-only list/detail for departments + members; actual CRUD flows through the /departments JSON API.

## 5. Vite 7 / Tailwind 4 Migration Checklist
1. **Node 22** on local + CI.
2. **Upgrade deps**: ite@^7, 	ailwindcss@^4, matching @tailwindcss/forms/	ypography, switch PostCSS plugin to @tailwindcss/postcss.
3. **Config rewrite**: move to ite.config.ts, migrate Tailwind to the new default export, collapse content globs per Tailwind 4 guidance.
4. **Bundle verification**: 
pm run build (confirm new hashed files), then dotnet test to ensure MVC still serves the bundle.
5. **Security**: upgrading Vite pulls the patched esbuild resolving the current audit warning; document any interim suppressions if rollout is delayed.
- **Status**: Completed. Stack is now Vite 7 + Tailwind 4.
- **Verification**: `npm run build` and `dotnet test` verify the integration.

Follow this guide to work on the UI without spelunking through the repo.
