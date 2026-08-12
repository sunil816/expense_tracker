# React + TypeScript Frontend for Expense Tracker

## Purpose

Build the web UI for uploading statement PDFs and viewing spending weekly/monthly by category. React 19 + TypeScript + Vite, Recharts, plain CSS. Single user, no auth.

**Status: all backend prerequisites are implemented and verified** (see `frontend-backend-low-level-design.md` for the backend design). This plan covers only the frontend, its build integration, and documentation. It supersedes earlier drafts that still contained backend work.

## Backend contracts the UI consumes (implemented, verified on disk)

All enums serialize as strings (global `JsonStringEnumConverter`). All errors are RFC 7807 `application/problem+json` with `status` + `title`. Base URL in dev: `https://localhost:7085` (the HTTP port fails the HTTPS gate on mutating endpoints).

| Endpoint | Notes |
| --- | --- |
| `POST /api/document-extractions` | multipart `file` + optional `password`; minutes-long; returns `{ importId, isDuplicate, importedAt, transactions: SavedTransaction[] }`; errors 400/422/429/499/502/503/504 |
| `GET /api/transactions` | filters `from`, `to`, `direction`, `categoryId`, `uncategorized`, `origin`, `sourceFormat`, `page`, `pageSize` (≤200) → `{ items, page, pageSize, totalCount }`; 400 when `from > to` or `categoryId` combined with `uncategorized=true` |
| `GET /api/transactions/{id}` | `TransactionDetailResponse` incl. `sourceFormat`, `balanceAfter`, lines, and tag chips (`state`, `source`, `decidedAt`) |
| `POST /api/transactions` | manual transaction (`ManualTransactionRequest`) → 201; 400 `"The category does not exist."` |
| `PUT /api/transactions/{id}` | full update of editable fields incl. `categoryId` → 200 detail; 400 unknown category; 404 |
| `GET /api/categories` | flat array `{ id, slug, name, kind, parentCategoryId }`; ordered Income before Expense, parents before children, then name |
| `GET /api/reports/spending` | `granularity=week|month` (required), optional `from`/`to` (defaults: `to` = host-local today via `TimeProvider`, `from` = first of month 5 months earlier) → `{ granularity, from, to, rows }` |

Report rows: `{ periodStart, categoryId, categoryName, categoryKind, direction, total, count }`, grouped by `(periodStart, categoryId, direction)`, ordered by period ASC, kind (Income, Expense, null last), name, direction. `categoryId: null` = uncategorized. Weeks are ISO-8601 Monday-start; edge buckets may be partial.

Notes that shape the UI:

- **`TransactionSummaryResponse` does NOT carry `sourceFormat`** (deliberate — it exists as a filter only). The table's provenance column shows `origin` (`Imported`/`Manual`); `sourceFormat` appears in the detail view. Do not add a source-format column to the list.
- **Classification policy (per the backend LLD):** an assigned category's `kind` is authoritative; uncategorized rows derive their class from direction. The frontend aggregation must implement this — see `lib/aggregate.ts` below.
- Static files + SPA fallback are live in `Program.cs`: unknown `/api/*` → ProblemDetails 404; anything else → `index.html` (deep links work once `wwwroot` exists).
- SPA hosting: prod is same-origin from `wwwroot` (no CORS anywhere); dev uses the Vite proxy. Vite's proxy makes its own TLS connection to Kestrel, so `Request.IsHttps` passes; `secure: false` only accepts the self-signed dev cert.

## Frontend

### Location and stack

`frontend/` at repo root (keeps the Node toolchain out of the VS solution folder; `.gitignore` entries already committed).

Runtime deps: `react`, `react-dom`, `react-router-dom`, `recharts`. Dev deps: `vite`, `@vitejs/plugin-react`, `typescript`, `@types/react`, `@types/react-dom`, `vitest`.

- **react-router-dom over tabs:** uploads take minutes and users refresh; URL-addressable routes + filters in search params survive refresh; the SPA fallback already supports deep links.
- **No date library** — API dates are `yyyy-MM-dd`; `Intl.DateTimeFormat('en-IN')` for display; ~15 lines of date math in `lib/dates.ts`.
- **Currency:** `Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' })` → lakh/crore grouping. Wrapped once in `lib/format.ts`.
- **TS unions mirroring the string enums:** `'Debit'|'Credit'`, `'Imported'|'Manual'`, `'PaymentExport'|'BankStatement'|'OrderHistory'`, `'Income'|'Expense'`, `'Week'|'Month'`, tag `'Suggested'|'Confirmed'|'Rejected'` / `'Rule'|'Manual'`.

### File tree

```
frontend/
  package.json  vite.config.ts  tsconfig.json  index.html
  src/
    main.tsx  App.tsx                    # shell: nav (Dashboard | Transactions | Upload) + <Outlet/>
    api/client.ts                        # apiFetch<T>: parses application/problem+json → ApiError{status,title,detail}
    api/types.ts                         # mirrors the response records listed above
    api/{categories,transactions,reports,imports}.ts
                                         # transactions.ts: list(params), get(id), createManual(req), update(id, req)
                                         # imports.ts: FormData POST + AbortController
                                         # NO tags.ts — tag chips come from the transaction detail response
    pages/DashboardPage.tsx              # "/"
    pages/TransactionsPage.tsx           # "/transactions", filters in useSearchParams
    pages/UploadPage.tsx                 # "/upload"
    components/CategorySelect.tsx        # <optgroup> per kind, indented children, "— Uncategorized —"
    components/TransactionsTable.tsx     # inline CategorySelect per row + pagination
    components/ManualTransactionForm.tsx # add-transaction form → POST /api/transactions
    components/SpendingBarChart.tsx      # stacked bars per period: net expense by category
    components/IncomeExpenseChart.tsx    # grouped bars per period: income class vs expense class
    components/CategoryPieChart.tsx      # donut: net expense share by category over the range
    components/RangePicker.tsx           # week/month toggle + presets (This month / Last 3 / Last 6 / custom)
    components/StatusBanner.tsx  components/Spinner.tsx   # spinner has elapsed-time counter
    lib/format.ts  lib/dates.ts
    lib/aggregate.ts                     # classification + pivot (pure functions = the Vitest surface)
    lib/errors.ts                        # ApiError → friendly copy
    styles/global.css                    # CSS custom properties, single file
```

### vite.config.ts

```ts
server: { port: 5173, proxy: { '/api': { target: 'https://localhost:7085', changeOrigin: true, secure: false } } },
build: { outDir: '../ExpenseTracker/ExpenseTracker/wwwroot', emptyOutDir: true }
```

No proxy timeout override — http-proxy has no default timeout; multi-minute extractions flow through.

### Aggregation rules (`lib/aggregate.ts`)

Implements the backend's classification policy. Client sorts rows before pivoting (defensive; server order is already deterministic).

1. **Class assignment per row:** Income class = `categoryKind === 'Income'`, or uncategorized with `direction === 'Credit'`. Expense class = `categoryKind === 'Expense'`, or uncategorized with `direction === 'Debit'`.
2. **Netting within a class:** signed amount = `+total` when the direction matches the class's natural direction (Credit for income, Debit for expense), `−total` otherwise. A Credit row in an Expense category (refund) therefore *reduces* that category's spend instead of counting as income; a Debit in an Income category (salary reversal) reduces income.
3. **Income vs Expense chart:** per period, income = Σ signed Income-class rows; expense = Σ signed Expense-class rows.
4. **Spending charts (stacked bars, pie):** Expense-class rows only, net per category per period. Categories whose net over the range is ≤ 0 are excluded from the pie; a negative per-period net renders as 0 in the stacked bar (edge case, tooltip still shows the true net). Top N categories + "Other"; Uncategorized always its own series.

### Chart acceptance criteria

Invoke the `dataviz` skill before writing chart components. Regardless: one consistent categorical palette across all three charts (same category = same color everywhere); Uncategorized always neutral gray; INR-formatted axis ticks and tooltips (category, period, amount); legends readable at 6+ series; legible in light and dark themes.

### Page behavior

**Upload**
- File input (`accept="application/pdf"`) + optional password; disable form on submit; Spinner with elapsed timer and "extraction can take several minutes" copy; Cancel via `AbortController`.
- **Cancel semantics:** a user cancel surfaces locally as `AbortError` — clear busy state, show a quiet "Upload cancelled" status (not an error banner), re-enable the form. Never wait for the server's 499.
- **Error mapping** in `lib/errors.ts`, keyed on ProblemDetails `status` + exact `title`: 400 `"The PDF could not be opened."` → wrong password or corrupt PDF; 400 `"The uploaded file is invalid."` / `"A PDF file is required."` → not a supported PDF; 422 `"No supported transaction data was found."` → no transaction table found; 429 → busy, retry in a few minutes; 503 → decryption tool unavailable; 504 → timed out; 502 → extraction service problem. Unknown status/title or non-ProblemDetails body → generic fallback.
- **Success:** duplicate banner when `isDuplicate` ("already imported on {importedAt}"); render returned transactions (rows carry persisted IDs — category editing enabled immediately); link to Transactions page.

**Transactions**
- Filters (date range, category incl. Uncategorized, direction, origin, source format) in `useSearchParams`; changing a filter resets `page` to 1. Never send `categoryId` together with `uncategorized=true` (backend 400s).
- Table columns: Date, Description, Reference, Origin, Amount (INR, direction-colored), Category (`CategorySelect` inline). Row click → detail (modal or expandable row): source format, balance, lines, read-only tag chips with State/Source badges.
- **Category change = PUT full-update:** build `TransactionUpdateRequest` from the row's current values plus the new `categoryId`; update the row from the returned detail on 200; revert the select + show banner on error.
- "Add transaction" opens `ManualTransactionForm` → `POST /api/transactions` (201 → prepend row).

**Dashboard**
- `RangePicker` (week/month + presets) drives one `getSpendingReport` call; all three charts pivot the same rows via `lib/aggregate.ts`.
- Child categories (MF, PPF, …) are their own series; parent roll-up toggle = future work.

## Build integration and docs

- **Manual `npm run build`, no MSBuild target** — Vite writes into `wwwroot`; the Web SDK auto-includes it in `dotnet publish`. Node stays off `dotnet build`/test.
- First `npm install` generates `package-lock.json` — **commit the lockfile**; use `npm ci` thereafter.
- **README.md:** "Web UI" section — dev = two terminals (`dotnet run … --launch-profile https` + `cd frontend && npm run dev`, browse `http://localhost:5173`); prod build steps; UI endpoint summary; ISO Monday-week note; classification/netting policy note (category kind authoritative; refunds net against category spend).
- **DEPLOYMENT.md:** Node LTS prerequisite; build-then-publish order; UI served same-origin from `wwwroot` with SPA fallback.

## Testing

Backend behavior is already covered (`CategoryListTests`, `SpendingReportTests`, `ControllerValidationTests`, extended `TransactionQueryTests`, with `FixedTimeProvider`).

**Frontend:** Vitest for `src/lib/` only (`aggregate`, `dates`, `format`, `errors`), `environment: 'node'` — the only real client logic is pure. Component/E2E harnesses are not worth it for a single-user tool; the browser checklist below covers the rest. `aggregate` tests must cover: refund netting (Credit + Expense-kind reduces the category and never appears as income), uncategorized fallback by direction, pie exclusion of non-positive nets, top-N + Other collapsing, week/month period labeling.

## Step order

1. Frontend scaffold: `npm create vite@latest frontend -- --template react-ts`; add proxy/outDir config; install deps; commit lockfile.
2. `api/` layer (client.ts ProblemDetails parsing first, then endpoint modules).
3. `lib/` utils + Vitest tests (pure; no pages needed).
4. TransactionsPage + CategorySelect + PUT flow + ManualTransactionForm — first end-to-end proof through the proxy.
5. UploadPage — verifies long-request behavior and cancel semantics.
6. DashboardPage + charts (invoke `dataviz` skill first).
7. `global.css` polish + App shell/nav.
8. Prod build check + README/DEPLOYMENT updates.

## Verification

```powershell
dotnet test ExpenseTracker/ExpenseTracker.slnx                    # stays green
dotnet run --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj --launch-profile https
cd frontend; npm install; npx vitest run; npm run dev             # second terminal (npm ci once lockfile committed)
# prod: cd frontend; npm run build → dotnet run → browse https://localhost:7085
```

API smoke (already passing on the implemented backend): `curl.exe -k https://localhost:7085/api/categories`, `…/api/transactions?page=1&pageSize=10&uncategorized=true`, `…/api/reports/spending?granularity=week`, and `-i …/api/nonexistent` → 404 problem+json, not index.html.

Browser checklist:
- Dev at `localhost:5173`: three pages load; `/api` calls 200 through the proxy.
- Upload a real PDF: spinner/timer, disabled submit; success shows transactions; re-upload → duplicate banner; wrong-password message distinct from invalid-file; saturated slots → 429 message; Cancel mid-upload → quiet cancelled status, form re-enabled, resubmit works.
- Transactions: filters in URL survive refresh; inline category set/clear via PUT persists across refresh; manual add appears with `origin: Manual`; detail shows source format, lines, tag chips.
- Dashboard: week/month toggle re-buckets; a Sunday transaction lands in its Monday-start bucket; Uncategorized = gray slice; a Credit tagged with an Expense category reduces that category's spend and does not appear as income.
- Prod at `https://localhost:7085`: `/` serves the SPA; deep link `/transactions?direction=Debit` refreshes; `/api/nope` → ProblemDetails 404.

## Out of scope (later)

- Tag assignment/confirmation UI (no backend endpoint to assign or confirm/reject tags on a transaction yet — only tag CRUD/rename/merge).
- Parent-category roll-up toggle on charts.
- Cross-import transaction dedup (documented gap from the persistence task).
