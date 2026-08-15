using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Transactions;
using ExpenseTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class DuplicateReviewTests
{
    [Fact]
    public async Task QueueDefaultsToSuggestedAndReturnsBothTransactionSides()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = Manual(new DateOnly(2026, 8, 1), "Original");
        var suggested = Manual(new DateOnly(2026, 8, 2), "Suggested duplicate");
        var confirmed = Manual(new DateOnly(2026, 8, 3), "Confirmed duplicate");
        suggested.DuplicateFlags.Add(Flag(suggested.Id, original.Id, DuplicateFlagState.Suggested, 2));
        confirmed.DuplicateFlags.Add(Flag(confirmed.Id, original.Id, DuplicateFlagState.Confirmed, 3));

        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.AddRange(original, suggested, confirmed);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<DuplicateReviewService>();
        var result = await service.ListAsync(new DuplicateReviewQuery
        {
            State = DuplicateFlagState.Suggested
        }, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Suggested duplicate", item.FlaggedTransaction.Description);
        Assert.Equal("Original", item.MatchedTransaction.Description);
        Assert.Equal(DuplicateFlagState.Suggested, item.Flag.State);
    }

    [Fact]
    public async Task QueueFiltersHistoryAndPagesDeterministically()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = Manual(new DateOnly(2026, 8, 1), "Original");
        var first = Manual(new DateOnly(2026, 8, 2), "First confirmed");
        var second = Manual(new DateOnly(2026, 8, 3), "Second confirmed");
        first.DuplicateFlags.Add(Flag(first.Id, original.Id, DuplicateFlagState.Confirmed, 1));
        second.DuplicateFlags.Add(Flag(second.Id, original.Id, DuplicateFlagState.Confirmed, 2));

        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.AddRange(original, first, second);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<DuplicateReviewService>();
        var result = await service.ListAsync(new DuplicateReviewQuery
        {
            State = DuplicateFlagState.Confirmed,
            Page = 2,
            PageSize = 1
        }, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(["First confirmed"], result.Items.Select(item => item.FlaggedTransaction.Description));
    }

    [Fact]
    public async Task QueueReturnsEmptyPageWhenNoFlagsMatchState()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<DuplicateReviewService>();

        var result = await service.ListAsync(new DuplicateReviewQuery
        {
            State = DuplicateFlagState.Rejected
        }, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    private static ExpenseTransaction Manual(DateOnly date, string description) =>
        new()
        {
            Id = Guid.NewGuid(),
            Origin = TransactionOrigin.Manual,
            TransactionDate = date,
            Description = description,
            Direction = TransactionDirection.Debit,
            Amount = 100m,
            LineExtractionStatus = LineExtractionStatus.NotApplicable
        };

    private static TransactionDuplicateFlag Flag(Guid transactionId, Guid matchedTransactionId, DuplicateFlagState state, int day) =>
        new()
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            MatchedTransactionId = matchedTransactionId,
            Reason = DuplicateMatchReason.Composite,
            State = state,
            SuggestedAt = new DateTimeOffset(2026, 8, day, 0, 0, 0, TimeSpan.Zero),
            DecidedAt = state == DuplicateFlagState.Suggested ? null : DateTimeOffset.UtcNow
        };
}
