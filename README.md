# expense_tracker

The API accepts supported payment, bank-statement, and order-summary PDFs, extracts successful transactions, normalizes them, and saves them to PostgreSQL.

## Database

The EF Core model contains:

- `document_imports`: SHA-256 upload hashes and import timestamps for idempotency;
- `transactions`: normalized INR debit/credit rows, either imported from a document or entered manually;
- `transaction_lines`: schema for future receipt products, taxes, fees, discounts, and other lines;
- `categories`: seeded income/expense categories, including the Investment hierarchy.

Each transaction records two independent facts. `origin` is `Imported` or `Manual`. `source_format` identifies the parsed document layout (`PaymentExport`, `BankStatement`, `OrderHistory`) and is set only for imported rows. A database check constraint keeps the pair consistent: manual rows carry no import, position, or format, and imported rows carry all three.

Source PDFs, passwords, filenames, and raw provider JSON are not stored. Re-uploading byte-identical content returns the existing import without running PDF preparation or Docling. Different files with overlapping statement ranges can still contain duplicate real-world transactions.

Configure the development connection string with .NET user-secrets; do not add credentials to `appsettings*.json`:

```powershell
dotnet user-secrets set "ConnectionStrings:ExpenseTracker" "Host=localhost;Port=5432;Database=expense_tracker;Username=<user>;Password=<password>" --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj
dotnet tool restore
dotnet ef database update --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj --startup-project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj
```

The application fails at startup when the connection string is absent. In Development, pending EF Core migrations are applied during startup, so an unreachable or invalidly\ authenticated configured database prevents the application from starting. Production does not apply migrations automatically.

## Document extraction prototype

Run the local Docling service using the instructions in [ExpenseTracker/plans/docling-local-setup.md](ExpenseTracker/plans/docling-local-setup.md), configure and migrate PostgreSQL as above, then start the API with `dotnet run --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj`.

The endpoint is `POST /api/document-extractions` over HTTPS and accepts multipart fields `file` and optional `password`. Conversion settings, limits, polling, and provider routes are configured under `DocumentExtraction` in `appsettings.json`. Extraction is asynchronous and may take several minutes on a CPU-only local service.

The endpoint returns a saved-import envelope containing `importId`, `isDuplicate`, `importedAt`, and normalized `transactions`. Amounts are positive decimals paired with a `Debit` or `Credit` direction; dates use ISO format, and enums are serialized as strings. The supported layouts are payment exports (`Name`, `Bank`, `Amount`, `Date`, `Status`), bank statements (`#`, `Date`, `Description`, `Chq/Ref. No.`, `Withdrawal (Dr.)`, `Deposit (Cr.)`, `Balance`), and Instamart order summaries (`Date / Time`, `Order ID`, `Pod Name`, `Amount`, `View`). Payment rows whose status is not `SUCCESS` are ignored. Documents without a supported transaction table return `422 Unprocessable Entity` and are not persisted.

```json
{
    "importId": "97ac59f2-e45e-4abe-a524-88f7a36098d1",
    "isDuplicate": false,
    "importedAt": "2026-08-01T10:30:00+00:00",
    "transactions": [
        {
            "id": "a08ce739-f584-49d5-9ad2-51a0df040ee8",
            "importPosition": 0,
            "sourceFormat": "PaymentExport",
            "transactionDate": "2026-07-12",
            "description": "SHOP",
            "accountLabel": "Kotak 4603",
            "direction": "Debit",
            "amount": 160.00,
            "currency": "INR",
            "lineExtractionStatus": "NotApplicable"
        }
    ]
}
```

For Instamart order summaries, `receiptUrl` contains the HTTP(S) hyperlink embedded in the source PDF when one is available. These links may be signed, grant access to receipt data, and expire according to the source document's URL.

## Manual transactions

Cash and other transactions that have no source document are recorded with `POST /api/transactions` over HTTPS. The server sets provenance, so the request cannot supply `origin`, `sourceFormat`, `importPosition`, or an import reference. Amounts are non-negative and paired with a `Debit` or `Credit` direction, and the currency is always `INR`.

```json
{
    "transactionDate": "2026-08-02",
    "description": "Cash lunch",
    "direction": "Debit",
    "amount": 250.00,
    "accountLabel": "Cash",
    "categoryId": "10000000-0000-0000-0000-000000000008"
}
```

A successful request returns `201 Created` with the saved transaction. An unknown `categoryId` returns `400 Bad Request` and stores nothing. Manual entries are not deduplicated, so a retried submit creates a second row.

For qpdf installation, server configuration, Docling connectivity, and deployment verification, see [DEPLOYMENT.md](DEPLOYMENT.md).
