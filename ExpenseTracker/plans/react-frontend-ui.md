# React + TypeScript Frontend for Expense Tracker

## Purpose

Build the web UI for uploading statement PDFs and viewing spending weekly/monthly by category. React 19 + TypeScript + Vite, Recharts, plain CSS. Single user, no auth — see Deployment boundary below for what that requires operationally.

**Status: all backend prerequisites are implemented and verified** (see `frontend-backend-low-level-design.md`). This plan covers the frontend, its build integration, one small backend hardening change, and documentation. Revised 2026-08-12 to incorporate the findings in `DESIGN_REVIEW.md`; module-level contracts live in `frontend-low-level-design.md`.

## Backend contracts the UI consumes (implemented, verified on disk)

All enums serialize as strings (global `JsonStringEnumConverter`). All errors are RFC 7807 `application/problem+json` with `status` + `title`; model-binding failures return `ValidationProblemDetails` with a field-keyed `errors` map. Base URL in dev: `https://localhost:7085`.

| Endpoint | Notes |
| --- | --- |
| `POST /api/document-extractions` | multipart `file` + optional `password`; minutes-long; returns `{ importId, isDuplicate, importedAt, transactions: SavedTransaction[] }`; errors 400/413/422/429/499/502/503/504 |
| `GET /api/transactions` | filters `from`, `to`, `direction`, `categoryId`, `uncategorized`, `origin`, `sourceFormat`, `page`, `pageSize` (≤200) → `{ items, page, pageSize, totalCount }`; 400 when `from > to` or `categoryId` combined with `uncategorized=true` |
| `GET /api/transactions/{id}` | `TransactionDetailResponse` incl. `sourceFormat`, `balanceAfter`, lines, and tag chips |
| `POST /api/transactions` | manual transaction → 201; 400 `"The category does not exist."`; 400 `ValidationProblemDetails` for field errors |
| `PUT /api/transactions/{id}` | full update of editable fields incl. `categoryId` → 200 detail; 400 unknown category; 404 |
| `GET /api/categories` | flat array `{ id, slug, name, kind, parentCategoryId }`; ordered Income before Expense, parents before children, then name |
| `GET /api/reports/spending` | `granularity=week|month` (required), optional `from`/`to` (defaults: `to` = host-local today, `from` = first of month 5 months earlier) → `{ granularity, from, to, rows }` |

Report rows: `{ periodStart, categoryId, categoryName, categoryKind, direction, total, count }`, grouped by `(periodStart, categoryId, direction)`. `categoryId: null` = uncategorized. Weeks are ISO-8601 Monday-start; edge buckets may be partial.

Notes that shape the UI:

- **The four transaction response shapes are NOT interchangeable** (list summary, detail, create response, extraction row differ in `origin`/`hasLines`/`lineExtractionStatus`). Policy: **after any mutation (create, category update) the Transactions page refetches its active list query** — this also respects current filters, sort, and pagination. The Upload page renders returned `SavedTransaction` rows with its own dedicated table shape (no adapter into the list DTO). The list DTO is never faked from another shape.
- **`TransactionSummaryResponse` does NOT carry `sourceFormat`** (filter-only by design). The list shows `origin`; `sourceFormat` appears in the detail view.
- **Classification policy (per the backend LLD):** an assigned category's `kind` is authoritative; uncategorized rows derive their class from direction.
- Static files + SPA fallback are live: unknown `/api/*` → ProblemDetails 404; anything else → `index.html`.
- Dev uses the Vite proxy (its own TLS connection satisfies `Request.IsHttps`; `secure: false` accepts the dev cert); prod is same-origin from `wwwroot` — no CORS anywhere.
- Kestrel caps the whole multipart body at the configured `MaximumFileSizeBytes` (25 MB default) — an oversized upload yields **413** before the controller runs.

## Deployment boundary (backend hardening + docs)

The app stores financial data with no authentication. That is acceptable **only** when the listener is unreachable by anyone but the owner:

- **Document in DEPLOYMENT.md:** bind to loopback (`localhost`) or a private/VPN interface only; never expose the port to a LAN/internet without adding authentication first. Database access stays restricted to the app identity (already documented).
- **Small backend change (this task):** in `Program.cs`, when not Development, add `app.UseHttpsRedirection()` and `app.UseHsts()` so the whole app — not just the mutating endpoints' `Request.IsHttps` checks — refuses plaintext in production. If TLS is ever terminated by a reverse proxy, forwarded-headers configuration is required first; note this in DEPLOYMENT.md rather than pre-configuring it.

## Frontend

### Location and stack

`frontend/` at repo root. Runtime deps: `react`, `react-dom`, `react-router-dom`, `recharts`. Dev deps: `vite`, `@vitejs/plugin-react`, `typescript`, `@types/react`, `@types/react-dom`, `vitest`. Node: pin the current LTS major (22) in `package.json` `engines` and README.

- **react-router-dom over tabs:** URL-addressable routes + filters in search params survive refresh; the SPA fallback already supports deep links.
- **No date library.** API dates (`yyyy-MM-dd`) are **calendar components, not UTC timestamps** — they are never passed through `new Date(iso)` directly (which would parse as UTC midnight and shift across timezones). `lib/dates.ts` owns all conversion; range presets are computed from the **browser's local date** (the report's server-side default range uses host-local time; both are "the user's machine" in this single-user deployment — documented, not reconciled).
- **Currency:** `Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' })` → lakh/crore grouping.
- **Money precision:** chart aggregation accumulates in **integer paise** (`Math.round(x * 100)`), converting back to rupees only for display — float summation drift can't reach rendered totals. Amounts are assumed within JavaScript's safe-integer range in paise (≤ ~₹90 trillion); the backend's theoretical `decimal` maximum exceeds this and is documented as out of scope.

### Routes

| Path | Purpose |
| --- | --- |
| `/` | Dashboard |
| `/transactions` | List + filters (in search params) |
| `/transactions/new` | Manual transaction form |
| `/transactions/:id` | Transaction detail (source format, balance, lines, tag chips) |
| `/upload` | Statement upload |

Detail and create are **routes, not modals** — explicit buttons/links navigate to them (no click-only rows); closing/back returns to `/transactions` with the previous search params intact.

### Responsive matrix

No page-level horizontal scroll at any breakpoint; interactive targets ≥ 44×44 px on touch.

| Route | 375 px (mobile) | 768 px (tablet) | 1280 px (desktop) |
| --- | --- | --- | --- |
| Shell | top bar + three icon/text nav links | same, more padding | horizontal nav |
| Dashboard | RangePicker stacks; charts full-width, stacked vertically, fixed heights (bar 280px, pie 260px); legend below chart | 2-column: bar full row, income/expense + pie side by side | 3-zone grid; legend right of pie |
| Transactions | filters collapse into a disclosure ("Filters (2)"); table becomes stacked cards (date + description headline; amount right-aligned; category select full-width) | 6-column table, horizontal scroll inside the table container if needed | full table |
| Detail / New | single column form/stack | single column, max-width 560px centered | same |
| Upload | single column; dropzone full-width | centered, max-width 560px | same |

Charts render inside fixed-height containers with `ResponsiveContainer` width so layout never jumps on data change.

### Accessibility contract (right-sized, verified in the browser checklist)

- Semantic landmarks (`header`/`nav`/`main`), one `h1` per route, heading order.
- Every input labeled (`<label for>`); form errors associated via `aria-describedby`; on failed submit, focus moves to the first invalid field and an error summary appears.
- Direction is **never color-only**: amounts render with explicit sign/prefix (`− ₹1,250.00` debit, `+ ₹15,000.00` credit) in addition to color.
- Visible focus indicator on every interactive element (`:focus-visible` token); the whole app is operable keyboard-only.
- Each chart has a text alternative: a visually-hidden (expandable) data table with the same numbers.
- Async status changes (upload progress, saved, errors) announced via a polite `aria-live` region (`StatusBanner` doubles as it).
- `prefers-reduced-motion` disables chart animations and transitions.
- Full WCAG 2.2 AA audit is explicitly out of scope for this single-user tool; the above are the enforced subset.

### Aggregation rules (`lib/aggregate.ts`)

1. **Class per row:** Income = `categoryKind === 'Income'`, or uncategorized + Credit. Expense = `categoryKind === 'Expense'`, or uncategorized + Debit.
2. **Netting:** signed amount = `+total` when direction matches the class's natural direction (Credit↔income, Debit↔expense), `−total` otherwise. A refund (Credit in an Expense category) reduces that category's spend; it is never income.
3. **Income vs Expense chart:** per period, income = Σ signed income-class rows; expense = Σ signed expense-class rows.
4. **Spending charts:** expense-class rows only, net per category per period. **Negative nets are rendered truthfully below a visible zero baseline** (Recharts `stackOffset="sign"`), not clamped — a refund-dominant period shows a negative bar segment. The chart's text-alternative table carries the same signed values.
5. **Top N = 6** categories by absolute net over the range; the rest collapse into "Other"; Uncategorized never collapses. Pie shows net-positive slices only, with a footnote line listing any net-negative categories ("Refunds exceeded spending: X").
6. All accumulation in integer paise (see Money precision).

### Chart acceptance criteria

Invoke the `dataviz` skill before writing chart components. Regardless: one consistent categorical palette (same category = same color in every chart, assignment stable within a render); Uncategorized/Other always neutral grays; INR-formatted ticks and tooltips; legend below charts on mobile, beside on desktop; long category labels ellipsized with full text in tooltip/table; all-zero range renders the empty state, not an empty axis.

### Page states (every route defines all of these)

| State | Transactions | Dashboard | Upload |
| --- | --- | --- | --- |
| Loading | skeleton rows | skeleton chart blocks | — |
| Empty (no data at all) | "No transactions yet — upload a statement or add one" + both CTAs | same message + upload CTA | — |
| Empty (filters exclude all) | "No matches — clear filters" + clear button | "No spending in this range" | — |
| Error | inline retry panel with `ApiError.title` | same | mapped message (below) |
| Mutating | per-row select disabled + inline spinner; form submit disabled | — | form disabled + elapsed timer |
| Success | refetch of active query; polite announcement | — | result table + duplicate banner |

Stale responses are ignored: each fetch is keyed to the current params; out-of-date resolutions are discarded (`AbortController` per query, aborted on param change/unmount).

### Page behavior

**Upload**
- Native file input wrapped in a **drag-and-drop zone**; shows selected filename + size + Replace; helper text states "PDF, up to 25 MB". Optional password field with accessible show/hide toggle.
- **Client-side pre-check:** reject non-PDF or > 25 MB files locally before any request.
- Submit → disable form, Spinner with elapsed timer and "extraction can take several minutes — leave this page open"; Cancel via `AbortController`.
- **Navigation guard while uploading:** `beforeunload` prompt + router blocker with a confirm dialog ("Extraction in progress — leaving cancels it; processing cannot be resumed"). Confirmed departure aborts the request.
- **Cancel semantics:** user cancel surfaces as `AbortError` → quiet "Upload cancelled" info status, form re-enabled. Never wait for the server's 499.
- **Error mapping** (`lib/errors.ts`, exact backend titles): 400 `"The PDF could not be opened."` → wrong password or corrupt PDF; 400 `"The uploaded file is invalid."` / `"A PDF file is required."` → not a supported PDF; **413 → file too large for the server (25 MB limit)**; 422 `"No supported transaction data was found."` → no transaction table found; 429 → busy, retry in a few minutes; 503 → decryption tool unavailable; 504 → timed out; 502 → extraction service problem; unknown → generic fallback.
- **Success:** duplicate banner when `isDuplicate`; results shown in a dedicated import-result table (its own shape — date, description, direction-signed amount, source format); link to Transactions.

**Transactions**
- Filters (date range, category incl. Uncategorized, direction, origin, source format) in `useSearchParams`; changing one resets `page`. Never send `categoryId` with `uncategorized=true`.
- Columns: Date, Description, Reference, Origin, signed Amount, Category (`CategorySelect` inline), Details link. **Category change = PUT full-update** built from the row's current values; on 200 → refetch the active list query; on 400 `ValidationProblemDetails` or unknown-category → revert select, show field/summary errors.
- `/transactions/new`: form with field-level validation display (client mirrors of the DataAnnotations + server `errors` map); on 201 → navigate back to the list (which refetches).
- `/transactions/:id`: read view; lines table; tag chips with state/source badges.

**Dashboard**
- `RangePicker` (week/month toggle + presets This month / Last 3 / Last 6 / custom from–to) drives one `getSpendingReport` call; all charts pivot the same rows. **Custom range is capped at 24 months** (the backend materializes all matching rows before grouping; the cap keeps payloads sane).
- Copy map for display labels: `PaymentExport` → "Payment export", `BankStatement` → "Bank statement", `OrderHistory` → "Order history", `Debit` → "Money out", `Credit` → "Money in" (table headers keep Debit/Credit), `Imported`/`Manual` as-is, `Week`/`Month` → "Weekly"/"Monthly".

### Theming

Design tokens defined **first** (step 2, not last): semantic custom properties for surface, text, borders, accent, banner semantics, focus ring, and `--chart-1..8` + `--chart-neutral`. Light and dark palettes switch on `prefers-color-scheme`; `color-scheme: light dark` set on `:root` so native controls follow. Contrast-checked in both modes (text ≥ 4.5:1, chart series ≥ 3:1 against surface). A persisted manual theme toggle is future work — system preference only in v1.

## Build integration and docs

- **Manual `npm run build`, no MSBuild target** — Vite writes into `wwwroot`; the Web SDK auto-includes it in `dotnet publish`. Node stays off `dotnet build`/test.
- First `npm install` generates `package-lock.json` — **commit the lockfile**; use `npm ci` thereafter.
- **Release procedure must fail without the UI** (a clean `dotnet publish` succeeds with no `wwwroot`): DEPLOYMENT.md gains an ordered release script — `npm ci` → `npx vitest run` → `npm run build` → `dotnet publish` → **assert `publish/wwwroot/index.html` exists** → smoke `/`, one deep link, and an unknown `/api/*` route (expect 404 problem+json).
- **README.md:** Web UI section (two-terminal dev workflow, prod build, endpoint summary, ISO Monday-week note, classification/netting policy, Node 22 requirement).
- **DEPLOYMENT.md:** Node prerequisite; release script above; deployment boundary (loopback/VPN-only, HTTPS redirect + HSTS in production, forwarded-headers note if a proxy ever terminates TLS).

## Testing

Backend behavior already covered (44 tests green, incl. report/category/validation suites).

**Frontend (Vitest, `environment: 'node'`):**
- `lib/aggregate` — classify (kind wins; direction fallback); refund netting (negative expense, never income); income/expense per period; per-category nets incl. **negative net preserved (not clamped)**; top-6 + Other collapsing; Uncategorized never collapses; paise accumulation (e.g. 0.1-style drift case); zero-net and cancellation cases.
- `lib/dates` — month arithmetic across year boundary; presets against an injected fixed today; `yyyy-MM-dd` treated as calendar components (no UTC shift).
- `lib/format` — INR lakh grouping; signed amount rendering; period labels.
- `lib/errors` — every mapping row incl. 413; unknown ApiError; non-ApiError fallback.
- **`api/client`** — with a stubbed global `fetch`: ok → typed JSON; problem+json → `ApiError` with title/detail; `ValidationProblemDetails` → `ApiError.errors` field map; non-JSON failure → generic `ApiError`; `AbortError` passes through untouched.

Component/E2E harnesses remain out of scope (single-user tool); the browser checklist and release smoke test cover integration. This is a documented tradeoff, not an omission.

## Step order

1. Scaffold (`npm create vite@latest frontend -- --template react-ts`); proxy/outDir config; prune template; commit lockfile; pin Node in `engines`.
2. `styles/global.css` design tokens (light + dark, `color-scheme`, focus ring, chart palette) + App shell/nav + router with empty routed pages.
3. `api/types.ts`, `api/client.ts` (incl. ValidationProblemDetails parsing), endpoint modules + client tests.
4. `lib/` modules + Vitest (green before any page logic).
5. TransactionsPage + CategorySelect + table/cards responsive layout + refetch-on-mutation; `/transactions/new`; `/transactions/:id`.
6. UploadPage (dropzone, pre-checks, navigation guard, cancel, error map, result table).
7. DashboardPage (RangePicker with 24-month cap, three charts with sign-aware stacking, text-alternative tables, copy map).
8. Backend hardening: `UseHttpsRedirection` + `UseHsts` outside Development.
9. Prod build; README/DEPLOYMENT updates incl. release script; run the release procedure once end-to-end.

## Verification

```powershell
dotnet test ExpenseTracker/ExpenseTracker.slnx                    # stays green
dotnet run --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj --launch-profile https
cd frontend; npm install; npx vitest run; npm run dev             # second terminal (npm ci once lockfile committed)
# release: npm ci; npx vitest run; npm run build; dotnet publish; assert wwwroot/index.html; smoke /, deep link, /api/nope
```

Browser checklist (desktop 1280×800, tablet 768×1024, mobile 375×812; light and dark):
- Three routes load; no page-level horizontal scroll at 375px; table collapses to cards; filters collapse to disclosure.
- Keyboard-only pass: navigate all routes, change a filter, edit a category, submit the manual form (focus lands on first invalid field), upload + cancel; focus visible throughout.
- Upload: drag-drop and picker both work; oversized/non-PDF rejected locally; spinner/timer; cancel → quiet status; navigation guard prompts mid-upload; duplicate banner on re-upload; wrong-password vs invalid-file messages distinct; 429 path.
- Transactions: filters in URL survive refresh; category set/clear persists (list refetches); manual add navigates back and appears with `origin: Manual`; detail route shows source format, lines, tag chips; deep links refresh correctly in prod.
- Dashboard: week/month re-buckets; Sunday lands in Monday-start bucket; refund shows below the zero baseline and reduces its category; Uncategorized gray; chart data tables match rendered values; empty and all-zero states render.
- `prefers-reduced-motion` disables animations; 200% zoom stays usable.
- Prod at `https://localhost:7085`: SPA serves; `/api/nope` → problem+json 404; plain-HTTP request redirects (non-dev).

## Out of scope (later)

- Tag assignment/confirmation UI (no backend endpoint yet).
- Parent-category roll-up toggle; persisted manual theme toggle.
- Authentication (deployment boundary documented instead); forwarded-headers/reverse-proxy TLS termination.
- Cross-import transaction dedup (documented backend gap).
- Full WCAG 2.2 AA audit; automated component/E2E tests.
