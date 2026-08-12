using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("api/document-extractions")]
public sealed class DocumentExtractionsController(
    DocumentExtractionService documentExtractionService,
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

        try
        {
            await using var input = file.OpenReadStream();
            return Ok(await documentExtractionService.ExtractAndImportAsync(input, password, cancellationToken));
        }
        catch (DocumentExtractionException exception)
        {
            logger.LogInformation("Document extraction ended with category {Category}.", exception.Error);
            return exception.Error switch
            {
                DocumentExtractionError.InvalidFile => Problem(statusCode: 400, title: "The uploaded file is invalid."),
                DocumentExtractionError.NoSupportedTransactionData => Problem(statusCode: 422, title: "No supported transaction data was found."),
                DocumentExtractionError.InvalidPasswordOrPdf => Problem(statusCode: 400, title: "The PDF could not be opened."),
                DocumentExtractionError.DecryptionUnavailable => Problem(statusCode: 503, title: "The PDF decryption tool is unavailable."),
                DocumentExtractionError.ProviderResponseTooLarge => Problem(statusCode: 502, title: "The extraction result is too large."),
                DocumentExtractionError.DeadlineExceeded => Problem(statusCode: 504, title: "Document extraction timed out."),
                DocumentExtractionError.Cancelled => Problem(statusCode: 499, title: "The request was cancelled."),
                DocumentExtractionError.Busy => Problem(statusCode: 429, title: "Extraction capacity is currently busy."),
                _ => Problem(statusCode: 502, title: "Document extraction failed.")
            };
        }
    }
}