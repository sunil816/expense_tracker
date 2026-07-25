using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("api/document-extractions")]
public sealed class DocumentExtractionsController(
    IPdfPreparationService pdfPreparationService,
    IDocumentExtractionProvider extractionProvider,
    SemaphoreSlim extractionSlots,
    ILogger<DocumentExtractionsController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ExtractAsync([FromForm] IFormFile? file, [FromForm] string? password, CancellationToken cancellationToken)
    {
        if (!Request.IsHttps)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "HTTPS is required.", detail: "This endpoint only accepts encrypted transport.");
        }

        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "A PDF file is required.");
        }

        if (!await extractionSlots.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Extraction capacity is currently busy.");
        }

        try
        {
            await using var preparedPdf = await pdfPreparationService.PrepareAsync(file, password, cancellationToken);
            var result = await extractionProvider.ExtractAsync(preparedPdf.Path, cancellationToken);
            return File(result.Body, result.ContentType);
        }
        catch (DocumentExtractionException exception)
        {
            logger.LogInformation("Document extraction ended with category {Category}.", exception.Error);
            return exception.Error switch
            {
                DocumentExtractionError.InvalidFile => Problem(statusCode: 400, title: "The uploaded file is invalid."),
                DocumentExtractionError.InvalidPasswordOrPdf => Problem(statusCode: 400, title: "The PDF could not be opened."),
                DocumentExtractionError.ProviderResponseTooLarge => Problem(statusCode: 502, title: "The extraction result is too large."),
                DocumentExtractionError.DeadlineExceeded => Problem(statusCode: 504, title: "Document extraction timed out."),
                DocumentExtractionError.Cancelled => Problem(statusCode: 499, title: "The request was cancelled."),
                DocumentExtractionError.Busy => Problem(statusCode: 429, title: "Extraction capacity is currently busy."),
                _ => Problem(statusCode: 502, title: "Document extraction failed.")
            };
        }
        finally
        {
            extractionSlots.Release();
        }
    }
}