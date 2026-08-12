using ExpenseTracker.Models.Reports;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(ReportService reportService) : ControllerBase
{
    [HttpGet("spending")]
    public async Task<IActionResult> GetSpendingAsync([FromQuery] SpendingReportQuery query, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SpendingGranularity>(query.Granularity, ignoreCase: true, out var granularity))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The granularity is invalid.",
                detail: "granularity must be 'week' or 'month'.");
        }

        var result = await reportService.GetSpendingAsync(granularity, query.From, query.To, cancellationToken);
        return result.Outcome switch
        {
            SpendingReportOutcome.Success => Ok(result.Report),
            _ => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The date range is invalid.",
                detail: "from must not be after to.")
        };
    }
}
