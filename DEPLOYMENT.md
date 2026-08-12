# Deployment Guide

This application requires PostgreSQL and two external services for document extraction:

1. `qpdf`, used to decrypt password-protected PDFs before PdfSharpCore processes them.
2. Docling Serve, used to extract document content.
3. PostgreSQL, used to persist normalized imports and transactions.

The API itself is a .NET 10 ASP.NET Core application.

## Windows deployment

### 1. Install qpdf

Install qpdf on the server using one of these methods.

Using WinGet:

```powershell
winget install --id QPDF.QPDF -e --accept-package-agreements --accept-source-agreements
```

Using the official qpdf installer is also supported. After installation, verify it from a new PowerShell window:

```powershell
qpdf --version
```

If `qpdf` is not on `PATH`, locate `qpdf.exe` and configure its full path in the deployment configuration. For example:

```json
{
  "PdfDecryption": {
    "QpdfExecutablePath": "C:\\Program Files\\qpdf 12.3.2\\bin\\qpdf.exe",
    "TimeoutSeconds": 60
  }
}
```

Do not assume this exact versioned path on every server. A stable installation path or a machine-specific environment-variable override is preferable.

### 2. Configure qpdf for the application

The application reads the `PdfDecryption` section:

```json
"PdfDecryption": {
  "QpdfExecutablePath": "qpdf",
  "TimeoutSeconds": 60
}
```

`qpdf` means that the executable must be available on the application process `PATH`. Alternatively, use an absolute path in `appsettings.Production.json` or an environment variable:

```powershell
$env:PdfDecryption__QpdfExecutablePath = "C:\Program Files\qpdf 12.3.2\bin\qpdf.exe"
$env:PdfDecryption__TimeoutSeconds = "60"
```

ASP.NET Core environment variables use double underscores (`__`) for configuration nesting.

### 3. Configure and migrate PostgreSQL

Supply the connection string at runtime. Do not commit it to JSON configuration:

```powershell
$env:ConnectionStrings__ExpenseTracker = "Host=<database-host>;Port=5432;Database=expense_tracker;Username=<user>;Password=<password>"
dotnet tool restore
dotnet ef database update --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj --startup-project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj
```

For local development, use .NET user-secrets instead of a committed setting:

```powershell
dotnet user-secrets set "ConnectionStrings:ExpenseTracker" "Host=localhost;Port=5432;Database=expense_tracker;Username=<user>;Password=<password>" --project ExpenseTracker/ExpenseTracker/ExpenseTracker.csproj
```

Use a dedicated database role with access only to the expense-tracker database. The database contains financial PII, including descriptions, references, balances, and potentially signed receipt URLs. Apply storage encryption, backup controls, and access auditing appropriate to that data.

The app validates that a connection string is configured during startup but does not contact PostgreSQL or apply migrations automatically. An unreachable database or pending migration is detected on first database use.

### 4. Run the API

```powershell
dotnet ExpenseTracker.dll --environment Production
```

For IIS, configure the ASP.NET Core application pool to run under an identity that can:

- execute `qpdf.exe`;
- read the application files;
- create and delete files under the Windows temporary directory;
- reach the Docling Serve URL;
- connect to PostgreSQL using the configured application role.

The application creates random request-scoped temporary directories under the system temporary directory. Do not grant broader file-system permissions than the application identity needs.

## Linux or container deployment

Install qpdf using the operating system package manager. For Debian or Ubuntu:

```bash
apt-get update
apt-get install -y qpdf
qpdf --version
```

For an Alpine-based image:

```bash
apk add --no-cache qpdf
qpdf --version
```

Keep the executable as `qpdf` when it is installed on `PATH`, then configure:

```json
"PdfDecryption": {
  "QpdfExecutablePath": "qpdf",
  "TimeoutSeconds": 60
}
```

The container or service account must be able to execute qpdf and write to its temporary directory. If the API and Docling run in separate containers, configure `DocumentExtraction:BaseUrl` to the Docling service name and port rather than `localhost`.

## Docling configuration

Configure Docling under `DocumentExtraction`. A local development example is in `ExpenseTracker/ExpenseTracker/appsettings.Development.json`.

For deployment, set the provider URL to the address reachable from the API process. For example:

```json
"DocumentExtraction": {
  "BaseUrl": "http://docling:5001",
  "SubmitPath": "/v1/convert/file/async",
  "StatusPathTemplate": "/v1/status/poll/{task_id}",
  "ResultPathTemplate": "/v1/result/{task_id}"
}
```

Run Docling using the instructions in [ExpenseTracker/plans/docling-local-setup.md](ExpenseTracker/plans/docling-local-setup.md), or use the approved Docling deployment process for your environment.

## Verify the deployment

### Verify qpdf directly

Use a test PDF and its password. Do not place real passwords in shell history or source control. The application itself writes the password to a short-lived request-scoped password file so it is not passed as a process argument.

```powershell
qpdf --password-file=password.txt --decrypt --object-streams=disable input.pdf decrypted.pdf
```

```bash
qpdf --password-file=password.txt --decrypt --object-streams=disable input.pdf decrypted.pdf
```

A successful run creates `decrypted.pdf`. Delete the test password file and decrypted output after verification.

### Verify the API

Send a `multipart/form-data` request to:

```text
POST https://<api-host>/api/document-extractions
```

Use these fields:

- `file`: the PDF file;
- `password`: optional PDF password.

The API requires HTTPS and returns a saved-import envelope with `importId`, `isDuplicate`, `importedAt`, and normalized transactions. A password-protected PDF is first decrypted by qpdf, then converted to an unprotected staged PDF for Docling. Payment rows not marked `SUCCESS` are ignored. A valid document without a supported transaction table returns `422 Unprocessable Entity` and creates no database rows. Re-uploading byte-identical content returns the existing import without repeating PDF preparation or extraction.

## Configuration checklist

Before releasing:

- the PostgreSQL database is reachable from the API host and the EF migration has been applied;
- `ConnectionStrings__ExpenseTracker` is supplied outside source control using the deployment platform's secret mechanism;
- the database role, backups, encryption, and auditing are restricted for financial PII;
- qpdf is installed on the API host and `qpdf --version` succeeds under the application identity;
- `PdfDecryption:QpdfExecutablePath` is correct for that host;
- `PdfDecryption:TimeoutSeconds` is appropriate for the server size;
- Docling is reachable from the API host;
- `DocumentExtraction:BaseUrl` is not left at a development-only address;
- HTTPS is configured for the API;
- temporary-directory permissions allow cleanup;
- production secrets and passwords are supplied at runtime, not committed to JSON files;
- upload, response, timeout, polling, and concurrency limits match the deployment capacity.

## Where to find this file

This guide is stored at the repository root:

```text
DEPLOYMENT.md
```

It is included in the source repository and can be opened directly in VS Code or from the repository's GitHub page. The application-specific configuration files are under:

```text
ExpenseTracker/ExpenseTracker/appsettings.json
ExpenseTracker/ExpenseTracker/appsettings.Development.json
```

Use environment variables or a deployment-specific `appsettings.Production.json` for server-specific paths and URLs. Do not commit machine-specific paths or credentials.
