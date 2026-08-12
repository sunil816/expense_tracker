# Project Core

- ASP.NET Core API solution under `ExpenseTracker/ExpenseTracker.slnx`; app project is `ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj`, tests are in `ExpenseTracker/ExpenseTracker.Tests`.
- `POST /api/document-extractions` stages/decrypts an uploaded PDF, calls Docling Serve asynchronously, and returns supported transaction tables as JSON header/value objects; unsupported layouts return 422.
- Main ownership: controller HTTP/error mapping; `IPdfPreparationService` secure request-scoped PDF staging; `IDocumentExtractionProvider` provider transport/orchestration; options classes configuration validation.
- Design plans live in `ExpenseTracker/plans`; deployment requirements are in `DEPLOYMENT.md`.
- Read `mem:tech_stack` for version/dependencies, `mem:conventions` for local patterns, `mem:suggested_commands` for normal commands, and `mem:task_completion` before finishing changes.