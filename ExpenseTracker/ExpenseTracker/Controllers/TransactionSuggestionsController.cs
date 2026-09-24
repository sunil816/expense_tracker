using ExpenseTracker.Models.Transactions;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("api/transaction-suggestions")]
public sealed class TransactionSuggestionsController(TransactionSuggestionService suggestionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] TransactionSuggestionQuery query,
        CancellationToken cancellationToken) =>
        Ok(await suggestionService.ListAsync(query, cancellationToken));

    [HttpPost("bulk-decision")]
    public async Task<IActionResult> BulkDecideAsync(
        [FromBody] TransactionSuggestionBulkDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Request.IsHttps)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "HTTPS is required.",
                detail: "This endpoint only accepts encrypted transport.");
        }

        var result = await suggestionService.BulkDecideAsync(request.SuggestionIds, request.State!.Value, cancellationToken);
        return result is null
            ? Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The suggestion state is invalid.",
                detail: "Bulk decisions can only confirm or reject suggestions.")
            : Ok(result);
    }
}