# Design Plan Review: React Expense Tracker UI

Reviewed against: `ExpenseTracker/plans/react-frontend-ui.md`
Philosophy: Not defined
Date: 2026-08-12

## Review Scope

This is a review of the frontend plan, not a visual review of a built interface. The repository currently has no `frontend/` directory, generated `wwwroot/`, `.design/` brief, or running UI to inspect. Visual hierarchy, rendering, interaction, and responsive findings must be validated with screenshots after implementation.

## Screenshots Captured

No screenshots were captured because no frontend implementation exists. The required desktop (1280x800), tablet (768x1024), and mobile (375x812) captures remain a release prerequisite for the implementation review.

## Summary

The plan is strongest where it defines financial classification, upload behavior, URL-backed filters, and same-origin deployment. It is not yet implementation-ready: transaction endpoints return incompatible response shapes, while responsive behavior, accessibility, non-upload states, and production access controls are insufficiently specified. These gaps should be resolved in the plan before scaffolding the UI.

## Must Fix

1. **Define responsive behavior for every route and breakpoint.** The component inventory and page behavior do not explain how the navigation, filters, six-column transaction table, detail view, forms, charts, or legends reorganize at 375px, 768px, and 1280px ([plan](ExpenseTracker/plans/react-frontend-ui.md#L51-L72), [page behavior](ExpenseTracker/plans/react-frontend-ui.md#L97-L113)). _Fix: add a route-by-route responsive matrix. At 375px use single-column pages, compact navigation, a filter sheet, stacked transaction rows, vertically stacked charts, 44x44px touch targets, and no page-level horizontal scroll; define corresponding tablet and desktop layouts and stable chart dimensions._

2. **Add a measurable accessibility and keyboard contract.** The plan relies on direction-colored amounts and interactive charts but does not require WCAG conformance, non-color cues, chart alternatives, focus behavior, or reduced motion ([chart criteria](ExpenseTracker/plans/react-frontend-ui.md#L93-L95), [transaction behavior](ExpenseTracker/plans/react-frontend-ui.md#L105-L109)). _Fix: require WCAG 2.2 AA, semantic landmarks and headings, associated labels and errors, visible focus indicators, text or signs in addition to color, keyboard-accessible chart summaries/data tables, polite status announcements, and `prefers-reduced-motion` support._

3. **Resolve incompatible transaction response shapes.** The plan says uploaded transactions can be rendered immediately, a PUT response can replace a list row, and a POST response can be prepended ([plan](ExpenseTracker/plans/react-frontend-ui.md#L103-L109)). List rows require `origin`, `lineExtractionStatus`, and `hasLines` ([summary DTO](ExpenseTracker/ExpenseTracker/Models/Transactions/TransactionReadModels.cs#L29-L42)); create responses lack all three ([create DTO](ExpenseTracker/ExpenseTracker/Models/Transactions/ManualTransactionModels.cs#L34-L44)); detail responses lack `hasLines` ([detail DTO](ExpenseTracker/ExpenseTracker/Models/Transactions/TransactionReadModels.cs#L68-L86)); extraction rows lack `origin` and `hasLines` ([extraction DTO](ExpenseTracker/ExpenseTracker/Models/Extraction/ExtractedTransaction.cs#L23-L38)). _Fix: define explicit adapters or, preferably, refetch the active list query after create/update/import. Refetching also respects active filters, sorting, and pagination._

4. **Do not visualize negative expense net as zero.** The planned stacked chart turns a refund-dominant period into a zero-height bar and exposes the real value only in a tooltip ([aggregation rules](ExpenseTracker/plans/react-frontend-ui.md#L84-L91)). That is visually false and inaccessible on touch and to assistive technology. _Fix: render negative values below a visible zero baseline, or show a persistent refund marker and signed value; provide the same value in the textual chart alternative._

5. **Set the production access and transport boundary.** The product handles financial data but is specified as “single user, no auth” ([purpose](ExpenseTracker/plans/react-frontend-ui.md#L3-L7)). The app has no global HTTPS redirect/HSTS and read endpoints are not guarded by `Request.IsHttps` ([pipeline](ExpenseTracker/ExpenseTracker/Program.cs#L75-L86)). _Fix: explicitly limit deployment to loopback or a private VPN and enforce that boundary, or add authentication/authorization. Enforce HTTPS globally in production and document forwarded-header handling for TLS termination._

6. **Make a clean production publish fail when the UI is absent.** Frontend generation is manual, output is ignored, and the runtime always maps `index.html` as the SPA fallback ([build plan](ExpenseTracker/plans/react-frontend-ui.md#L117-L120), [pipeline](ExpenseTracker/ExpenseTracker/Program.cs#L75-L86)). A clean `dotnet publish` can therefore succeed without a UI. _Fix: require the release pipeline to run `npm ci`, frontend tests, `npm run build`, and `dotnet publish`, then assert that published `wwwroot/index.html` exists and smoke-test `/`, a deep link, and an unknown `/api/*` route._

## Should Fix

1. **Specify loading, empty, error, saving, and success states outside Upload.** Upload has a complete lifecycle, while Transactions and Dashboard describe mostly successful data ([page behavior](ExpenseTracker/plans/react-frontend-ui.md#L97-L113)). _Fix: add a state table for each query and mutation, including initial loading, background refetch, empty account, zero filtered results, retryable failure, per-row saving, form submission, stale-request cancellation, and announced success._

2. **Choose the transaction detail and creation interaction model.** “Modal or expandable row” leaves keyboard behavior and mobile navigation unresolved, and the manual form is only described as opening ([transactions plan](ExpenseTracker/plans/react-frontend-ui.md#L105-L109)). _Fix: define `/transactions/:id` and `/transactions/new`; use explicit buttons rather than a click-only row, preserve the list query on close/back, and specify focus entry, Escape/close behavior, focus containment where applicable, and focus restoration._

3. **Preserve server validation details.** The planned `ApiError` keeps only `status`, `title`, and `detail` ([API layer](ExpenseTracker/plans/react-frontend-ui.md#L51-L57)), but `[ApiController]` returns field errors for invalid create/update models ([controller](ExpenseTracker/ExpenseTracker/Controllers/TransactionsController.cs#L7-L53), [request validation](ExpenseTracker/ExpenseTracker/Models/Transactions/ManualTransactionModels.cs#L8-L31)). _Fix: parse `ValidationProblemDetails.errors`, map messages to fields, provide an error summary, and move focus to the first invalid field._

4. **Make money aggregation precision-safe.** The backend accepts and stores `decimal` values beyond JavaScript’s exact numeric range ([request constraint](ExpenseTracker/ExpenseTracker/Models/Transactions/ManualTransactionModels.cs#L18-L19), [database precision](ExpenseTracker/ExpenseTracker/Data/ExpenseTrackerDbContext.cs#L81-L83)), while the plan aggregates JSON numbers client-side ([aggregation rules](ExpenseTracker/plans/react-frontend-ui.md#L84-L91)). _Fix: convert API totals to integer paise at the boundary or return decimal strings and use decimal-safe arithmetic; test cancellation and zero-net cases._

5. **Cover request-size failures and validate files before upload.** The friendly error map omits HTTP 413 ([upload errors](ExpenseTracker/plans/react-frontend-ui.md#L99-L103)), while Kestrel limits the entire multipart body to the configured file limit ([configuration](ExpenseTracker/ExpenseTracker/Program.cs#L27-L36)). _Fix: show the configured maximum size before upload, reject obviously oversized files locally, map 413 explicitly, and allow for multipart overhead at the server boundary._

6. **Define interruption behavior for long uploads.** Routing is justified partly by refresh resilience, but an active extraction cannot survive navigation or refresh because there is no job-status endpoint ([routing rationale](ExpenseTracker/plans/react-frontend-ui.md#L38-L40), [cancel behavior](ExpenseTracker/plans/react-frontend-ui.md#L99-L101)). _Fix: warn before route/unload while extraction is active, abort after confirmed departure, and tell users that processing cannot be resumed._

7. **Define dark mode rather than only requiring dark-theme legibility.** CSS variables and chart legibility are mentioned without an activation or persistence model ([styles](ExpenseTracker/plans/react-frontend-ui.md#L70-L72), [chart criteria](ExpenseTracker/plans/react-frontend-ui.md#L93-L95)). _Fix: specify system preference as the default, an accessible persisted override, `color-scheme`, semantic light/dark tokens, and contrast checks for charts, tooltips, controls, borders, focus, and statuses._

8. **Set deterministic chart and report limits.** “Top N,” “Other,” and custom range are not bounded or fully defined ([charts](ExpenseTracker/plans/react-frontend-ui.md#L89-L95), [range picker](ExpenseTracker/plans/react-frontend-ui.md#L67)). _Fix: choose `N`, ordering, color assignment, `Other` disclosure, long-label behavior, all-zero behavior, and legend placement by breakpoint. Set and enforce a maximum custom report range because the backend currently materializes all matching rows before grouping ([report service](ExpenseTracker/ExpenseTracker/Services/ReportService.cs#L29-L37))._

9. **Broaden regression coverage around client behavior.** Restricting tests to `src/lib` leaves ProblemDetails parsing, URL-filter normalization, cancellation, create/update reconciliation, focus behavior, and production hosting to a manual happy-path checklist ([testing plan](ExpenseTracker/plans/react-frontend-ui.md#L124-L155)). _Fix: add focused API-client and component interaction tests plus the clean-publish smoke test; retain manual browser review for visual and assistive-technology checks._

## Could Improve

1. **Define the visual philosophy and tokens before “polish.”** A single CSS file is named, but there is no aesthetic direction, type scale, spacing scale, density target, or page hierarchy, and shell styling is deferred until late ([styles](ExpenseTracker/plans/react-frontend-ui.md#L70-L72), [step order](ExpenseTracker/plans/react-frontend-ui.md#L129-L137)). _Suggestion: choose a quiet, work-focused financial-tool direction; define semantic color, type, spacing, surface, border, and focus tokens before page implementation._

2. **Specify user-facing terminology and empty-state copy.** Backend enum names are defined, but display labels for source formats, Debit/Credit, origin, and granularity are not ([type plan](ExpenseTracker/plans/react-frontend-ui.md#L41-L43)). _Suggestion: add a copy map and distinct next-action empty states for a new account, no filter matches, no spending in range, and a zero net._

3. **Improve PDF entry ergonomics.** The upload plan includes only a file input and optional password ([upload behavior](ExpenseTracker/plans/react-frontend-ui.md#L99-L103)). _Suggestion: retain the native input while adding drag-and-drop, selected filename/size, Replace, accessible password reveal, and clear PDF/size helper text._

4. **Pin the calendar and browser contract.** The plan names Node LTS and browser date math but not a Node major, browser support matrix, preset timezone owner, or parsing rule ([date plan](ExpenseTracker/plans/react-frontend-ui.md#L40-L42), [verification](ExpenseTracker/plans/react-frontend-ui.md#L139-L155)). _Suggestion: treat `yyyy-MM-dd` as calendar components rather than UTC timestamps, choose whether server or browser local time owns presets, pin the tested Node major, and test current Chromium, Firefox, and WebKit._

## What Works Well

- URL-addressable routes and search-parameter filters are appropriate for refreshable, shareable state without introducing a global state library.
- Classification, refund netting, uncategorized fallback, and chart aggregation rules are precise and testable ([aggregation rules](ExpenseTracker/plans/react-frontend-ui.md#L84-L91)).
- Upload progress, cancellation, duplicate handling, and friendly extraction-error mapping are unusually well specified ([upload behavior](ExpenseTracker/plans/react-frontend-ui.md#L99-L103)).
- Consistent category colors, neutral Uncategorized treatment, and INR-formatted axes/tooltips establish a useful chart baseline ([chart criteria](ExpenseTracker/plans/react-frontend-ui.md#L93-L95)).
- Same-origin production hosting, the Vite development proxy, and API-specific fallback ordering are sound architectural choices ([hosting notes](ExpenseTracker/plans/react-frontend-ui.md#L27-L30), [pipeline](ExpenseTracker/ExpenseTracker/Program.cs#L75-L86)).
- The dependency list is restrained, and the plan correctly keeps Node out of ordinary .NET builds while committing the lockfile.

## Implementation Review Gate

After the frontend exists, run a separate visual review against an explicit design brief. Capture full-page screenshots for all three routes at 1280x800, 768x1024, and 375x812 in light and dark modes, plus transaction detail, filters open, manual-form validation, upload progress/cancel/success/error, loading, empty, and chart edge states. Verify keyboard-only use, visible focus, 200% zoom, reduced motion, no horizontal page overflow, and chart textual alternatives before release.