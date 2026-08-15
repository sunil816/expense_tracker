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

public sealed class TransactionQueryTests
{
    [Fact]
    public async Task ListFiltersByDateRangeAndDirection()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.AddRange(
                Manual(new DateOnly(2026, 7, 31), "Before the window", TransactionDirection.Debit),
                Manual(new DateOnly(2026, 8, 1), "Rent", TransactionDirection.Debit),
                Manual(new DateOnly(2026, 8, 5), "Salary", TransactionDirection.Credit),
                Manual(new DateOnly(2026, 8, 31), "After the window", TransactionDirection.Debit));
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var window = await service.ListAsync(new TransactionListQuery
        {
            From = new DateOnly(2026, 8, 1),
            To = new DateOnly(2026, 8, 10)
        }, CancellationToken.None);
        var debitsOnly = await service.ListAsync(new TransactionListQuery
        {
            From = new DateOnly(2026, 8, 1),
            To = new DateOnly(2026, 8, 10),
            Direction = TransactionDirection.Debit
        }, CancellationToken.None);

        Assert.Equal(2, window.TotalCount);
        Assert.Equal(["Salary", "Rent"], window.Items.Select(item => item.Description));
        Assert.Equal(["Rent"], debitsOnly.Items.Select(item => item.Description));
    }

    [Fact]
    public async Task ListReturnsNewestFirstAcrossPagesWithTotalCount()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            for (var day = 1; day <= 5; day++)
            {
                context.Transactions.Add(Manual(new DateOnly(2026, 8, day), $"Day {day}", TransactionDirection.Debit));
            }

            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var firstPage = await service.ListAsync(new TransactionListQuery { Page = 1, PageSize = 2 }, CancellationToken.None);
        var secondPage = await service.ListAsync(new TransactionListQuery { Page = 2, PageSize = 2 }, CancellationToken.None);

        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(5, secondPage.TotalCount);
        Assert.Equal(["Day 5", "Day 4"], firstPage.Items.Select(item => item.Description));
        Assert.Equal(["Day 3", "Day 2"], secondPage.Items.Select(item => item.Description));
    }

    [Fact]
    public async Task DetailReturnsLinesInPositionOrderWithTagNames()
    {
        await using var database = await TestDatabase.CreateAsync();
        var transactionId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            context.Tags.Add(new Tag { Id = tagId, Slug = "groceries", Name = "Groceries" });
            var transaction = Manual(new DateOnly(2026, 8, 4), "Supermarket", TransactionDirection.Debit);
            transaction.Id = transactionId;
            transaction.LineExtractionStatus = LineExtractionStatus.Available;
            transaction.Lines =
            [
                Line(2, "Milk", 30m),
                Line(1, "Bread", 20m)
            ];
            transaction.TagAssignments =
            [
                new TransactionTag
                {
                    TagId = tagId,
                    State = TagAssignmentState.Confirmed,
                    Source = TagAssignmentSource.Manual,
                    DecidedAt = DateTimeOffset.UtcNow
                }
            ];
            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var detail = await service.GetAsync(transactionId, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal([1, 2], detail.Lines.Select(line => line.Position));
        Assert.Equal(["Bread", "Milk"], detail.Lines.Select(line => line.Description));
        var tag = Assert.Single(detail.Tags);
        Assert.Equal("Groceries", tag.Name);
        Assert.Equal(TagAssignmentState.Confirmed, tag.State);
    }

    [Fact]
    public async Task DetailReturnsNullForUnknownTransaction()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<TransactionService>();

        Assert.Null(await service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ListFiltersByCategoryIdAndByUncategorized()
    {
        var foodCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000008");
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            var categorized = Manual(new DateOnly(2026, 8, 1), "Groceries", TransactionDirection.Debit);
            categorized.CategoryId = foodCategoryId;
            var uncategorized = Manual(new DateOnly(2026, 8, 2), "Unknown charge", TransactionDirection.Debit);
            context.Transactions.AddRange(categorized, uncategorized);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var byCategory = await service.ListAsync(new TransactionListQuery { CategoryId = foodCategoryId }, CancellationToken.None);
        var uncategorizedOnly = await service.ListAsync(new TransactionListQuery { Uncategorized = true }, CancellationToken.None);

        Assert.Equal(["Groceries"], byCategory.Items.Select(item => item.Description));
        Assert.Equal(["Unknown charge"], uncategorizedOnly.Items.Select(item => item.Description));
    }

    [Fact]
    public async Task ListFiltersByOriginAndSourceFormat()
    {
        await using var database = await TestDatabase.CreateAsync();
        var importId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            context.DocumentImports.Add(new DocumentImport
            {
                Id = importId,
                ContentHash = new string('a', 64),
                ImportedAt = DateTimeOffset.UtcNow
            });
            context.Transactions.Add(new ExpenseTransaction
            {
                Id = Guid.NewGuid(),
                Origin = TransactionOrigin.Imported,
                DocumentImportId = importId,
                ImportPosition = 0,
                SourceFormat = SourceDocumentFormat.BankStatement,
                TransactionDate = new DateOnly(2026, 8, 1),
                Description = "Imported row",
                Direction = TransactionDirection.Debit,
                Amount = 50m,
                LineExtractionStatus = LineExtractionStatus.Unavailable
            });
            context.Transactions.Add(Manual(new DateOnly(2026, 8, 2), "Manual row", TransactionDirection.Debit));
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var imported = await service.ListAsync(new TransactionListQuery { Origin = TransactionOrigin.Imported }, CancellationToken.None);
        var manual = await service.ListAsync(new TransactionListQuery { Origin = TransactionOrigin.Manual }, CancellationToken.None);
        var bankStatements = await service.ListAsync(new TransactionListQuery { SourceFormat = SourceDocumentFormat.BankStatement }, CancellationToken.None);

        Assert.Equal(["Imported row"], imported.Items.Select(item => item.Description));
        Assert.Equal(["Manual row"], manual.Items.Select(item => item.Description));
        Assert.Equal(["Imported row"], bankStatements.Items.Select(item => item.Description));
    }

    [Fact]
    public async Task ConfirmedDuplicatesAreExcludedFromListsButRemainAvailableById()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = Manual(new DateOnly(2026, 8, 1), "Original", TransactionDirection.Debit);
        var confirmed = Manual(new DateOnly(2026, 8, 2), "Confirmed duplicate", TransactionDirection.Debit);
        var suggested = Manual(new DateOnly(2026, 8, 3), "Suggested duplicate", TransactionDirection.Debit);
        var rejected = Manual(new DateOnly(2026, 8, 4), "Rejected duplicate", TransactionDirection.Debit);
        confirmed.DuplicateFlags.Add(Flag(confirmed.Id, original.Id, DuplicateFlagState.Confirmed));
        suggested.DuplicateFlags.Add(Flag(suggested.Id, original.Id, DuplicateFlagState.Suggested));
        rejected.DuplicateFlags.Add(Flag(rejected.Id, original.Id, DuplicateFlagState.Rejected));

        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.AddRange(original, confirmed, suggested, rejected);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var result = await service.ListAsync(new TransactionListQuery { PageSize = 10 }, CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.DoesNotContain(result.Items, item => item.Id == confirmed.Id);
        Assert.Contains(result.Items, item => item.Id == original.Id);
        Assert.Contains(result.Items, item => item.Id == suggested.Id);
        Assert.Contains(result.Items, item => item.Id == rejected.Id);
        Assert.NotNull(await service.GetAsync(confirmed.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateDecisionIsIdempotentButCannotBeReversed()
    {
        await using var database = await TestDatabase.CreateAsync();
        var original = Manual(new DateOnly(2026, 8, 1), "Original", TransactionDirection.Debit);
        var duplicate = Manual(new DateOnly(2026, 8, 2), "Duplicate", TransactionDirection.Debit);
        duplicate.DuplicateFlags.Add(Flag(duplicate.Id, original.Id, DuplicateFlagState.Suggested));

        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.AddRange(original, duplicate);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var updated = await service.DecideDuplicateFlagAsync(duplicate.Id, DuplicateFlagState.Confirmed, CancellationToken.None);
        var repeated = await service.DecideDuplicateFlagAsync(duplicate.Id, DuplicateFlagState.Confirmed, CancellationToken.None);
        var reversed = await service.DecideDuplicateFlagAsync(duplicate.Id, DuplicateFlagState.Rejected, CancellationToken.None);

        Assert.Equal(DuplicateFlagDecisionOutcome.Updated, updated.Outcome);
        Assert.Equal(DuplicateFlagState.Confirmed, updated.Flag!.State);
        Assert.NotNull(updated.Flag.DecidedAt);
        Assert.Equal(DuplicateFlagDecisionOutcome.AlreadyDecided, repeated.Outcome);
        Assert.Equal(DuplicateFlagDecisionOutcome.Conflict, reversed.Outcome);
    }

    [Fact]
    public async Task UncategorizedTrueIgnoresCategoryIdWhenBothAreSupplied()
    {
        var foodCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000008");
        await using var database = await TestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            var categorized = Manual(new DateOnly(2026, 8, 1), "Groceries", TransactionDirection.Debit);
            categorized.CategoryId = foodCategoryId;
            context.Transactions.Add(categorized);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var result = await service.ListAsync(
            new TransactionListQuery { CategoryId = foodCategoryId, Uncategorized = true },
            CancellationToken.None);

        Assert.Empty(result.Items);
    }

    private static ExpenseTransaction Manual(DateOnly date, string description, TransactionDirection direction) =>
        new()
        {
            Id = Guid.NewGuid(),
            Origin = TransactionOrigin.Manual,
            TransactionDate = date,
            Description = description,
            Direction = direction,
            Amount = 100m,
            LineExtractionStatus = LineExtractionStatus.NotApplicable
        };

    private static TransactionLine Line(int position, string description, decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            Position = position,
            LineType = TransactionLineType.Product,
            Description = description,
            Amount = amount
        };

    private static TransactionDuplicateFlag Flag(Guid transactionId, Guid matchedTransactionId, DuplicateFlagState state) =>
        new()
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            MatchedTransactionId = matchedTransactionId,
            Reason = DuplicateMatchReason.Composite,
            State = state,
            SuggestedAt = DateTimeOffset.UtcNow,
            DecidedAt = state == DuplicateFlagState.Suggested ? null : DateTimeOffset.UtcNow
        };
}
