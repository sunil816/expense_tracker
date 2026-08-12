# Tech Stack

- .NET 10 (`net10.0`), ASP.NET Core controllers/OpenAPI, nullable reference types enabled.
- Provider JSON handled with `System.Text.Json`; HTTP integration uses `IHttpClientFactory`.
- PDF handling: PdfSharpCore 1.3.67 plus externally installed `qpdf`; extraction provider: Docling Serve.
- Tests: xUnit 2.9.3 and Microsoft.NET.Test.Sdk 17.14.1.
- No ORM/database layer or transaction domain models currently exist.