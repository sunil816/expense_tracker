using ExpenseTracker.Data;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Reports;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services;

public sealed class ReportService(IDbContextFactory<ExpenseTrackerDbContext> contextFactory, TimeProvider timeProvider)
{
    public async Task<SpendingReportResult> GetSpendingAsync(
        SpendingGranularity granularity,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var resolvedTo = to ?? today;
        var resolvedFrom = from ?? new DateOnly(resolvedTo.Year, resolvedTo.Month, 1).AddMonths(-5);

        if (resolvedFrom > resolvedTo)
        {
            return new SpendingReportResult(SpendingReportOutcome.InvalidRange, null);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var slimTransactions = await context.Transactions
            .AsNoTracking()
            .ExcludingConfirmedDuplicates()
            .Where(transaction => transaction.Kind != TransactionKind.Transfer)
            .Where(transaction => transaction.TransactionDate >= resolvedFrom && transaction.TransactionDate <= resolvedTo)
            .Select(transaction => new
            {
                transaction.TransactionDate,
                transaction.Amount,
                transaction.Direction,
                transaction.CategoryId
            })
            .ToListAsync(cancellationToken);

        var categoryIds = slimTransactions
            .Where(transaction => transaction.CategoryId is not null)
            .Select(transaction => transaction.CategoryId!.Value)
            .Distinct()
            .ToList();
        var categories = await context.Categories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => (category.Name, category.Kind), cancellationToken);

        // Bucketing/grouping happens in C# (not SQL) so SQLite tests and PostgreSQL share identical semantics.
        var rows = slimTransactions
            .GroupBy(transaction => (
                PeriodStart: BucketStart(transaction.TransactionDate, granularity),
                transaction.CategoryId,
                transaction.Direction))
            .Select(group =>
            {
                var category = group.Key.CategoryId is { } categoryId && categories.TryGetValue(categoryId, out var found)
                    ? found
                    : ((string Name, CategoryKind Kind)?)null;
                return new SpendingReportRow(
                    group.Key.PeriodStart,
                    group.Key.CategoryId,
                    category?.Name,
                    category?.Kind,
                    group.Key.Direction,
                    group.Sum(transaction => transaction.Amount),
                    group.Count());
            })
            .OrderBy(row => row.PeriodStart)
            .ThenBy(row => row.CategoryKind switch { CategoryKind.Income => 0, CategoryKind.Expense => 1, _ => 2 })
            .ThenBy(row => row.CategoryName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(row => row.Direction)
            .ToList();

        return new SpendingReportResult(
            SpendingReportOutcome.Success,
            new SpendingReportResponse(granularity, resolvedFrom, resolvedTo, rows));
    }

    /// <summary>ISO-8601 Monday-start for weeks; first of month for months.</summary>
    private static DateOnly BucketStart(DateOnly date, SpendingGranularity granularity) =>
        granularity switch
        {
            SpendingGranularity.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
            _ => new DateOnly(date.Year, date.Month, 1)
        };
}
