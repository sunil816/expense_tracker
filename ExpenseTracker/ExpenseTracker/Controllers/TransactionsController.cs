using ExpenseTracker.Models.Transactions;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController(TransactionService transactionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] TransactionListQuery query, CancellationToken cancellationToken)
    {
        if (query.From is { } from && query.To is { } to && from > to)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The date range is invalid.",
                detail: "from must not be after to.");
        }

        if (query.CategoryId is not null && query.Uncategorized is true)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The filter combination is invalid.",
                detail: "categoryId must not be combined with uncategorized=true.");
        }

        return Ok(await transactionService.ListAsync(query, cancellationToken));
    }

    [HttpGet("{transactionId:guid}")]
    public async Task<IActionResult> GetAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var transaction = await transactionService.GetAsync(transactionId, cancellationToken);
        return transaction is null ? TransactionNotFound() : Ok(transaction);
    }

    [HttpGet("matches")]
    public async Task<IActionResult> FindMatchingAsync([FromQuery] MatchingTransactionQuery query, CancellationToken cancellationToken) =>
        Ok(await transactionService.FindMatchingAsync(query, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ManualTransactionRequest request, CancellationToken cancellationToken)
    {
        if (RequireHttps() is { } insecure)
        {
            return insecure;
        }

        var created = await transactionService.CreateManualAsync(request, cancellationToken);
        return created is null ? UnknownCategory() : StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{transactionId:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid transactionId, [FromBody] TransactionUpdateRequest request, CancellationToken cancellationToken)
    {
        if (RequireHttps() is { } insecure)
        {
            return insecure;
        }

        var result = await transactionService.UpdateAsync(transactionId, request, cancellationToken);
        return result.Outcome switch
        {
            TransactionUpdateOutcome.Updated => Ok(result.Transaction),
            TransactionUpdateOutcome.UnknownCategory => UnknownCategory(),
            _ => TransactionNotFound()
        };
    }

    [HttpPut("category/matches")]
    public async Task<IActionResult> UpdateCategoryForMatchesAsync([FromBody] BulkCategoryRequest request, CancellationToken cancellationToken)
    {
        if (RequireHttps() is { } insecure)
        {
            return insecure;
        }

        var result = await transactionService.UpdateCategoryForDescriptionAsync(request, cancellationToken);
        return result.Outcome == TransactionUpdateOutcome.UnknownCategory
            ? UnknownCategory()
            : Ok(new BulkCategoryResponse(result.UpdatedCount));
    }

    [HttpPost("{transactionId:guid}/tags")]
    public async Task<IActionResult> AddTagAsync(Guid transactionId, [FromBody] TransactionTagRequest request, CancellationToken cancellationToken)
    {
        if (RequireHttps() is { } insecure)
        {
            return insecure;
        }

        var result = await transactionService.AddTagAsync(transactionId, request.TagId!.Value, cancellationToken);
        return result.Outcome switch
        {
            TransactionTagUpdateOutcome.Updated => Ok(result.Transaction),
            TransactionTagUpdateOutcome.AlreadyAssigned => Ok(result.Transaction),
            TransactionTagUpdateOutcome.TagNotFound => TagNotFound(),
            _ => TransactionNotFound()
        };
    }

    [HttpDelete("{transactionId:guid}/tags/{tagId:guid}")]
    public async Task<IActionResult> RemoveTagAsync(Guid transactionId, Guid tagId, CancellationToken cancellationToken)
    {
        if (RequireHttps() is { } insecure)
        {
            return insecure;
        }

        var result = await transactionService.RemoveTagAsync(transactionId, tagId, cancellationToken);
        return result.Outcome switch
        {
            TransactionTagUpdateOutcome.Updated => Ok(result.Transaction),
            _ => TransactionNotFound()
        };
    }

    private IActionResult UnknownCategory() =>
        Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The category does not exist.",
            detail: "categoryId must reference an existing category.");

    private IActionResult TransactionNotFound() =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "The transaction does not exist.",
            detail: "The requested transaction could not be found.");

    private IActionResult TagNotFound() =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "The tag does not exist.",
            detail: "tagId must reference an existing tag.");

    private IActionResult? RequireHttps() =>
        Request.IsHttps
            ? null
            : Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "HTTPS is required.",
                detail: "This endpoint only accepts encrypted transport.");
}
