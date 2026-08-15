# Expense Tracker Frontend

React + TypeScript frontend for the Expense Tracker application.

## Prerequisites

- .NET 10 SDK
- Node.js 22
- PostgreSQL
- `qpdf` and Docling if PDF statement importing is required

## Run Locally

The API and frontend must run in separate terminals.

### 1. Configure the database

From the repository root, load the user-scoped connection string:

```powershell
Set-Location C:\learn\Learn\expense_tracker

$env:ConnectionStrings__ExpenseTracker = [Environment]::GetEnvironmentVariable(
  "ConnectionStrings__ExpenseTracker",
  "User"
)
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

If the connection string has not been configured, set it with .NET user-secrets:

```powershell
dotnet user-secrets set `
  "ConnectionStrings:ExpenseTracker" `
  "Host=localhost;Port=5432;Database=expense_tracker;Username=<user>;Password=<password>" `
  --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj
```

Restore the local EF tool and apply migrations when needed:

```powershell
dotnet tool restore
dotnet ef database update `
  --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj `
  --startup-project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj
```

### 2. Start the ASP.NET API

In terminal 1:

```powershell
Set-Location C:\learn\Learn\expense_tracker

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__ExpenseTracker = [Environment]::GetEnvironmentVariable(
  "ConnectionStrings__ExpenseTracker",
  "User"
)

dotnet run --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj
```

The API normally runs at:

```text
https://localhost:7085
```

In Development, pending EF Core migrations are applied automatically during startup.

### 3. Start the React frontend

In terminal 2:

```powershell
Set-Location C:\learn\Learn\expense_tracker\frontend
npm ci
npm run dev -- --host localhost
```

Open the application at:

```text
http://localhost:5173/
```

Use `http`, not `https`, for the Vite frontend. The local Vite TLS listener is disabled because some machines reject its self-signed development certificate. Requests under `/api/*` are still proxied to the HTTPS ASP.NET API.

## Validation

Run frontend tests:

```powershell
Set-Location C:\learn\Learn\expense_tracker\frontend
npm test
```

Create a production frontend build:

```powershell
npm run build
```

The build output is written to:

```text
ExpenseTracker/ExpenseTracker/wwwroot
```

Run backend tests:

```powershell
Set-Location C:\learn\Learn\expense_tracker\ExpenseTracker
dotnet test --no-restore
```

## PDF Import

PDF upload additionally requires:

- PostgreSQL
- `qpdf`
- the configured Docling service

The dashboard, transaction list, transaction details, and manual transaction pages can run without performing a document import.
