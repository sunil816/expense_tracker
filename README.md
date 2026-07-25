# expense_tracker

1. Categories:
    a.Investment
        -MF
        -PPF
        -FD
        -Bonds
        -Stocks
        -Gold
    b.food
    c.grocery
    d.rent
    e.salary
    f.travel

2. How to divide the data?
    - Divide the data into 2 tables one is 
        -transaction
        -transaction_details
    
    user
        -id
        -name
        -email
        -password

    transaction
        -id
        -user_id
        -date
        -details
        -reference_no
        -mode {DEBIT, CREDIT}
        -status
        -category
        -location
        -amount
    
    transaction_details
        -id
        -transaction_id
        -details
        -bank_or_app_name
        -type (PRODUCT,TAX)
        -quantity
        -status
        -amount


LLD:
-accept statement pdfs and extract info from them and convert them into statements

## Document extraction prototype

Run the local Docling service using the instructions in [ExpenseTracker/plans/docling-local-setup.md](ExpenseTracker/plans/docling-local-setup.md), then start the API with `dotnet run --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj`.

The endpoint is `POST /api/document-extractions` over HTTPS and accepts multipart fields `file` and optional `password`. Conversion settings, limits, polling, and provider routes are configured under `DocumentExtraction` in `appsettings.json`. Extraction is asynchronous and may take several minutes on a CPU-only local service.

The endpoint intentionally returns the provider's raw JSON response unchanged. It does not normalize the result into transaction records.
