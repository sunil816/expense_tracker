# Conventions

- File-scoped namespaces; nullable reference types; async methods use the `Async` suffix and accept `CancellationToken`.
- Provider failures are categorized as `DocumentExtractionException`/`DocumentExtractionError`; controllers convert categories to API-owned `ProblemDetails` and avoid exposing provider bodies or document-derived data in logs.
- Provider routes, limits, headers, and conversion options belong in `DocumentExtractionOptions`/`appsettings*.json`, not hard-coded provider logic.
- Keep the controller provider-agnostic. Docling submit/poll/result protocol remains inside `DoclingDocumentExtractionProvider`.
- Tests use xUnit and an in-process stub `HttpMessageHandler`; unit tests must not require a live Docling container.