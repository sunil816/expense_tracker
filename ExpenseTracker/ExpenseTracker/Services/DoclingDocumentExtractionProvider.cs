using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Options;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Services;

public sealed class DoclingDocumentExtractionProvider(
    IHttpClientFactory clientFactory,
    IOptions<DocumentExtractionOptions> options,
    ILogger<DoclingDocumentExtractionProvider> logger) : IDocumentExtractionProvider
{
    private readonly DocumentExtractionOptions options = options.Value;

    public async Task<ExtractionResult> ExtractAsync(string pdfPath, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.ExtractionDeadlineSeconds));

        try
        {
            var taskId = await SubmitAsync(pdfPath, deadline.Token);
            await PollUntilCompleteAsync(taskId, deadline.Token);
            return await GetResultAsync(taskId, deadline.Token);
        }
        catch (DocumentExtractionException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new DocumentExtractionException(DocumentExtractionError.Cancelled, "The extraction request was cancelled.");
        }
        catch (OperationCanceledException)
        {
            throw new DocumentExtractionException(DocumentExtractionError.DeadlineExceeded, "The extraction deadline was exceeded.");
        }
        catch (Exception exception)
        {
            logger.LogWarning("Document extraction provider request failed with {ErrorType}.", exception.GetType().Name);
            throw new DocumentExtractionException(DocumentExtractionError.ProviderUnavailable, "The extraction provider could not be reached.", exception);
        }
    }

    private async Task<string> SubmitAsync(string pdfPath, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(pdfPath);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, options.FileFieldName, "document.pdf");
        AddConversionOptions(content, options.ConversionOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, options.SubmitPath) { Content = content };
        AddHeaders(request);
        using var response = await clientFactory.CreateClient("document-extraction").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new DocumentExtractionException(DocumentExtractionError.ProviderFailed, "The extraction provider rejected the document.");
        }

        using var document = await ParseJsonAsync(response, cancellationToken);
        if (!TryGetString(document.RootElement, "task_id", out var taskId) || string.IsNullOrWhiteSpace(taskId))
        {
            throw new DocumentExtractionException(DocumentExtractionError.ProviderFailed, "The extraction provider returned an invalid task.");
        }

        return taskId;
    }

    private async Task PollUntilCompleteAsync(string taskId, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var document = await SendJsonWithRetryAsync(HttpMethod.Get, FormatPath(options.StatusPathTemplate, taskId), cancellationToken);
            if (!TryGetString(document.RootElement, "task_status", out var status))
            {
                throw new DocumentExtractionException(DocumentExtractionError.ProviderFailed, "The extraction provider returned an invalid status.");
            }

            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(status, "failure", StringComparison.OrdinalIgnoreCase))
            {
                throw new DocumentExtractionException(DocumentExtractionError.ProviderFailed, "The extraction provider failed to process the document.");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), cancellationToken);
        }
    }

    private async Task<ExtractionResult> GetResultAsync(string taskId, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(HttpMethod.Get, FormatPath(options.ResultPathTemplate, taskId), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new DocumentExtractionException(DocumentExtractionError.ProviderFailed, "The extraction provider could not return the result.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > options.MaximumResponseSizeBytes)
            {
                throw new DocumentExtractionException(DocumentExtractionError.ProviderResponseTooLarge, "The extraction provider response exceeds the configured size limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return new ExtractionResult(buffer.ToArray(), response.Content.Headers.ContentType?.MediaType ?? "application/json");
    }

    private async Task<JsonDocument> SendJsonWithRetryAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(method, path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new DocumentExtractionException(DocumentExtractionError.ProviderFailed, "The extraction provider returned an invalid status response.");
        }

        return await ParseJsonAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, path);
                AddHeaders(request);
                var response = await clientFactory.CreateClient("document-extraction").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt >= options.PollRetryAttempts)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < options.PollRetryAttempts)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < options.PollRetryAttempts)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(options.PollRetryBackoffMilliseconds * (attempt + 1)), cancellationToken);
        }
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        foreach (var header in options.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) => (int)statusCode >= 500 && (int)statusCode <= 599;

    private static string FormatPath(string template, string taskId) => template.Replace("{task_id}", Uri.EscapeDataString(taskId), StringComparison.OrdinalIgnoreCase);

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new DocumentExtractionException(DocumentExtractionError.ProviderFailed, "The extraction provider returned malformed JSON.", exception);
        }
    }

    private static bool TryGetString(JsonElement root, string name, out string? value)
    {
        if (root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static void AddConversionOptions(MultipartFormDataContent content, JsonElement options)
    {
        if (options.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in options.EnumerateObject())
        {
            AddOption(content, property.Name, property.Value);
        }
    }

    private static void AddOption(MultipartFormDataContent content, string name, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                AddOption(content, name, item);
            }

            return;
        }

        var text = value.ValueKind == JsonValueKind.Object ? value.GetRawText() : value.ToString();
        content.Add(new StringContent(text, Encoding.UTF8), name);
    }
}