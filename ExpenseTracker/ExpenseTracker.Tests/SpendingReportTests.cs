using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Reports;
using ExpenseTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class SpendingReportTests
{
    private static readonly Guid FoodCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000008");
    private static readonly Guid SalaryCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000011");

    [Fact]
    public async Task MonthlyReportGroupsByMonthCategoryAndDirection()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.AddRange(
                Manual(new DateOnly(2026, 7, 5), TransactionDirection.Debit, 100m, FoodCategoryId),
                Manual(new DateOnly(2026, 7, 20), TransactionDirection.Debit, 50m, FoodCategoryId),
                Manual(new DateOnly(2026, 8, 1), TransactionDirection.Debit, 25m, FoodCategoryId),
                Manual(new DateOnly(2026, 7, 10), TransactionDirection.Credit, 1000m, SalaryCategoryId),
                Manual(new DateOnly(2026, 7, 15), TransactionDirection.Debit, 30m, null));
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<ReportService>();
        var result = await service.GetSpendingAsync(
            SpendingGranularity.Month,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 8, 31),
            CancellationToken.None);

        Assert.Equal(SpendingReportOutcome.Success, result.Outcome);
        var rows = result.Report!.Rows;

        var julyFood = Assert.Single(rows, row => row.PeriodStart == new DateOnly(2026, 7, 1) && row.CategoryId == FoodCategoryId);
        Assert.Equal(150m, julyFood.Total);
        Assert.Equal(2, julyFood.Count);
        Assert.Equal(CategoryKind.Expense, julyFood.CategoryKind);
        Assert.Equal("Food", julyFood.CategoryName);

        var augustFood = Assert.Single(rows, row => row.PeriodStart == new DateOnly(2026, 8, 1) && row.CategoryId == FoodCategoryId);
        Assert.Equal(25m, augustFood.Total);

        var julySalary = Assert.Single(rows, row => row.CategoryId == SalaryCategoryId);
        Assert.Equal(TransactionDirection.Credit, julySalary.Direction);
        Assert.Equal(1000m, julySalary.Total);

        var uncategorized = Assert.Single(rows, row => row.CategoryId == null);
        Assert.Null(uncategorized.CategoryName);
        Assert.Null(uncategorized.CategoryKind);
        Assert.Equal(TransactionDirection.Debit, uncategorized.Direction);
        Assert.Equal(30m, uncategorized.Total);
    }

    [Fact]
    public async Task WeeklyReportUsesIsoMondayStartBuckets()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            // Sunday belongs to the week that started the previous Monday.
            context.Transactions.AddRange(
                Manual(new DateOnly(2026, 1, 4), TransactionDirection.Debit, 10m, null),
                Manual(new DateOnly(2026, 1, 5), TransactionDirection.Debit, 20m, null));
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<ReportService>();
        var result = await service.GetSpendingAsync(
            SpendingGranularity.Week,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 11),
            CancellationToken.None);

        var rows = result.Report!.Rows;
        var sundayBucket = Assert.Single(rows, row => row.Total == 10m);
        Assert.Equal(new DateOnly(2025, 12, 29), sundayBucket.PeriodStart);

        var mondayBucket = Assert.Single(rows, row => row.Total == 20m);
        Assert.Equal(new DateOnly(2026, 1, 5), mondayBucket.PeriodStart);
    }

    [Fact]
    public async Task DefaultsSpanFiveMonthsBeforeTodayWhenDatesAreOmitted()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        await using var database = await TestDatabase.CreateAsync(clock);
        var service = database.Services.GetRequiredService<ReportService>();

        var result = await service.GetSpendingAsync(SpendingGranularity.Month, null, null, CancellationToken.None);

        Assert.Equal(SpendingReportOutcome.Success, result.Outcome);
        Assert.Equal(new DateOnly(2026, 8, 10), result.Report!.To);
        Assert.Equal(new DateOnly(2026, 3, 1), result.Report.From);
    }

    [Fact]
    public async Task FromAfterToIsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<ReportService>();

        var result = await service.GetSpendingAsync(
            SpendingGranularity.Month,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 7, 1),
            CancellationToken.None);

        Assert.Equal(SpendingReportOutcome.InvalidRange, result.Outcome);
        Assert.Null(result.Report);
    }

    [Fact]
    public async Task ReportedCategoryKindReflectsStoredAssignmentEvenWhenDirectionDiffers()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            // A Credit transaction filed under the Expense "Food" category — an intentionally mismatched assignment.
            context.Transactions.Add(Manual(new DateOnly(2026, 7, 5), TransactionDirection.Credit, 40m, FoodCategoryId));
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<ReportService>();
        var result = await service.GetSpendingAsync(
            SpendingGranularity.Month,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            CancellationToken.None);

        var row = Assert.Single(result.Report!.Rows);
        Assert.Equal(CategoryKind.Expense, row.CategoryKind);
        Assert.Equal(TransactionDirection.Credit, row.Direction);
    }

    [Fact]
    public async Task ConfirmedDuplicateIsExcludedFromReportTotalsAndCounts()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = Manual(new DateOnly(2026, 8, 1), TransactionDirection.Debit, 40m, FoodCategoryId);
        var confirmed = Manual(new DateOnly(2026, 8, 2), TransactionDirection.Debit, 60m, FoodCategoryId);
        confirmed.DuplicateFlags.Add(new TransactionDuplicateFlag
        {
            Id = Guid.NewGuid(),
            TransactionId = confirmed.Id,
            MatchedTransactionId = original.Id,
            Reason = DuplicateMatchReason.Composite,
            State = DuplicateFlagState.Confirmed,
            SuggestedAt = DateTimeOffset.UtcNow,
            DecidedAt = DateTimeOffset.UtcNow
        });

        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.AddRange(original, confirmed);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<ReportService>();
        var result = await service.GetSpendingAsync(
            SpendingGranularity.Month,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            CancellationToken.None);

        var row = Assert.Single(result.Report!.Rows);
        Assert.Equal(40m, row.Total);
        Assert.Equal(1, row.Count);
    }

    private static ExpenseTransaction Manual(DateOnly date, TransactionDirection direction, decimal amount, Guid? categoryId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Origin = TransactionOrigin.Manual,
            TransactionDate = date,
            Description = "Test",
            Direction = direction,
            Amount = amount,
            CategoryId = categoryId,
            LineExtractionStatus = LineExtractionStatus.NotApplicable
        };
}
