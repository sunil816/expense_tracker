# Local Docling Serve Setup

This guide documents the local CPU-based Docling Serve instance used by the document extraction prototype.

## Prerequisites

- Docker Desktop is installed and running.
- The machine has enough CPU and memory for Docling's document and table models.
- Port `5001` is available on the host.

## Start the Local Service

Run the user-verified command from PowerShell:

```powershell
docker run -p 127.0.0.1:5001:5001 -e DOCLING_SERVE_ENABLE_UI=1 ghcr.io/docling-project/docling-serve-cpu
```

The port mapping binds Docling to `127.0.0.1`, so the service is reachable from the local machine but is not exposed to the network through this command. The current setup does not configure an API key.

The first conversion may take longer if model artifacts need to be prepared. The current container has already made the required artifacts available. Recreating the container without a mounted artifacts directory may require that setup again.

For a named container that is easier to inspect or stop:

```powershell
docker run --name docling-serve -p 127.0.0.1:5001:5001 -e DOCLING_SERVE_ENABLE_UI=1 ghcr.io/docling-project/docling-serve-cpu
```

## Optional Persistent Artifacts

Use a host directory to retain model artifacts across container recreation. Choose a directory appropriate for the local machine and keep it out of source control:

```powershell
New-Item -ItemType Directory -Force .\docling-artifacts | Out-Null
docker run --name docling-serve `
  -p 127.0.0.1:5001:5001 `
  -e DOCLING_SERVE_ENABLE_UI=1 `
  -v "${PWD}\docling-artifacts:/root/.cache/docling" `
  ghcr.io/docling-project/docling-serve-cpu
```

The exact cache path can vary by image version. Confirm the image's artifact path in the container logs or image documentation before standardizing this volume in a shared setup.

## Useful URLs

With the container running at the default port:

- API root: `http://localhost:5001/`
- OpenAPI documentation: `http://localhost:5001/docs`
- Optional interactive UI: `http://localhost:5001/ui`
- Async file submission: `POST http://localhost:5001/v1/convert/file/async`
- Task status: `GET http://localhost:5001/v1/status/poll/{task_id}`
- Completed result: `GET http://localhost:5001/v1/result/{task_id}`

Open `/docs` first when checking the installed image version because it shows the exact request fields and response schemas exposed by the running service.

## Smoke Checks

Check that the port is accepting connections:

```powershell
Test-NetConnection localhost -Port 5001
```

Check the API root:

```powershell
curl.exe http://localhost:5001/
```

Open the API documentation in a browser:

```powershell
Start-Process http://localhost:5001/docs
```

## Async Conversion Test

The synchronous endpoint is not suitable for the observed CPU runtime. A real document took approximately 199 to 204 seconds in the local environment, while the synchronous service default returns HTTP 504 after approximately 120 seconds. Use the async submit, poll, and result sequence instead.

Set the input path and submit the file. The following example uses the previously tested PDF:

```powershell
$pdf = 'C:\Users\Sunil.patil\Downloads\sm_receipt_1783845338269.pdf'

$submit = curl.exe -sS -X POST 'http://localhost:5001/v1/convert/file/async' `
  -F "files=@$pdf;type=application/pdf" `
  -F 'from_formats=pdf' `
  -F 'to_formats=json' `
  -F 'do_ocr=false' `
  -F 'do_table_structure=true' `
  -F 'table_mode=accurate' `
  -F 'pdf_backend=docling_parse'

$submit | Set-Content -Encoding utf8 .\docling-submit.json
$submit
```

Read the returned task identifier. The response field is normally named `task_id`; confirm the exact envelope in `/docs` for the installed image:

```powershell
$taskId = ($submit | ConvertFrom-Json).task_id
$taskId
```

Poll the task until it reaches a terminal state. Do not use `Start-Sleep` in an automated agent session; when running this manually, a short delay between requests avoids unnecessary polling:

```powershell
$status = curl.exe -sS "http://localhost:5001/v1/status/poll/$taskId"
$status
```

Repeat the status request until the response reports success or failure. When the task succeeds, retrieve the raw result:

```powershell
curl.exe -sS "http://localhost:5001/v1/result/$taskId" `
  -o .\docling-response.json

Get-Content .\docling-response.json -Raw
```

The .NET integration follows this same sequence and returns the successful result body unchanged. It parses only the small submission and status envelopes needed to control polling.

## Logs and Shutdown

List the running containers:

```powershell
docker ps
```

Follow logs for a named container:

```powershell
docker logs -f docling-serve
```

Stop the container:

```powershell
docker stop docling-serve
```

Remove a stopped named container when it is no longer needed:

```powershell
docker rm docling-serve
```

If the container was started without `--name`, find its generated name with `docker ps -a` and use that name or container ID with the same commands.

## Operational Notes

- Keep the local binding as `127.0.0.1:5001:5001` for a local-only development service.
- There is no API key configured by the current Docker command. Do not treat this unauthenticated local service as suitable for network exposure.
- The public ASP.NET Core endpoint should keep the Docling URL, route templates, polling interval, deadlines, multipart field names, and conversion options in JSON configuration. Callers should not be able to override those values.
- The public endpoint accepts only a file and optional PDF password. Passwords must not be logged or persisted.
- The API first creates an unprotected request-scoped PDF because the provider boundary receives an unprotected document.
- Temporary PDFs must be deleted after every request, including cancellation and provider failure.
- The current prototype intentionally passes through the provider JSON. It does not yet normalize tables into transactions, validate account balances, persist documents, or provide a user interface.
