using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Models.Reports;

public enum SpendingGranularity
{
    Week,
    Month
}

public sealed class SpendingReportQuery
{
    public string? Granularity { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}

public sealed record SpendingReportRow(
    DateOnly PeriodStart,
    Guid? CategoryId,
    string? CategoryName,
    CategoryKind? CategoryKind,
    TransactionDirection Direction,
    decimal Total,
    int Count);

public sealed record SpendingReportResponse(
    SpendingGranularity Granularity,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<SpendingReportRow> Rows);

public enum SpendingReportOutcome
{
    Success,
    InvalidRange
}

public sealed record SpendingReportResult(
    SpendingReportOutcome Outcome,
    SpendingReportResponse? Report);
