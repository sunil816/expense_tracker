# Task Completion

- Run focused tests for the touched behavior first.
- Run `dotnet test ExpenseTracker/ExpenseTracker.slnx`.
- Run `dotnet build ExpenseTracker/ExpenseTracker.slnx`.
- For document-provider integration changes, manually validate separately against Docling/qpdf with synthetic or redacted PDFs; unit tests should remain offline.
- Preserve bounded upload/response handling, HTTPS enforcement, temporary-file cleanup, safe `ProblemDetails`, and no sensitive logging.