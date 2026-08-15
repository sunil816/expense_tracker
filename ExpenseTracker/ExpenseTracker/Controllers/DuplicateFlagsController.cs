using ExpenseTracker.Models.Transactions;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("api/duplicate-flags")]
public sealed class DuplicateFlagsController(DuplicateReviewService duplicateReviewService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] DuplicateReviewQuery query,
        CancellationToken cancellationToken) =>
        Ok(await duplicateReviewService.ListAsync(query, cancellationToken));
}