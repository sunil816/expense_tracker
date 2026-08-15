using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Transactions;
using ExpenseTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class TransactionUpdateTests
{
    private static readonly Guid FoodCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000008");

    [Fact]
    public async Task UpdateReplacesEditableFieldsAndTrimsBlanksToNull()
    {
        await using var database = await TestDatabase.CreateAsync();
        var transactionId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            var transaction = Manual(transactionId, "Cash lunch");
            transaction.AccountLabel = "Cash";
            transaction.ReceiptUrl = "https://example.test/old";
            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var result = await service.UpdateAsync(transactionId, new TransactionUpdateRequest
        {
            TransactionDate = new DateOnly(2026, 8, 9),
            Description = "  Team dinner  ",
            Note = "  Reimbursed by Alex  ",
            Direction = TransactionDirection.Credit,
            Amount = 999m,
            AccountLabel = "   ",
            ExternalReference = " REF-1 ",
            CategoryId = FoodCategoryId,
            ReceiptUrl = null
        }, CancellationToken.None);

        Assert.Equal(TransactionUpdateOutcome.Updated, result.Outcome);
        Assert.NotNull(result.Transaction);
        Assert.Equal("Team dinner", result.Transaction.Description);
        Assert.Equal("Reimbursed by Alex", result.Transaction.Note);
        Assert.Null(result.Transaction.AccountLabel);
        Assert.Equal("REF-1", result.Transaction.ExternalReference);

        await using var verification = await database.CreateContextAsync();
        var stored = await verification.Transactions.SingleAsync();
        Assert.Equal(new DateOnly(2026, 8, 9), stored.TransactionDate);
        Assert.Equal(TransactionDirection.Credit, stored.Direction);
        Assert.Equal(999m, stored.Amount);
        Assert.Equal(FoodCategoryId, stored.CategoryId);
        Assert.Null(stored.ReceiptUrl);
        Assert.Equal("Reimbursed by Alex", stored.Note);
    }

    [Fact]
    public async Task UnknownTransactionAndUnknownCategoryLeaveTheRowUntouched()
    {
        await using var database = await TestDatabase.CreateAsync();
        var transactionId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.Add(Manual(transactionId, "Cash lunch"));
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var missing = await service.UpdateAsync(Guid.NewGuid(), Request("Renamed"), CancellationToken.None);
        var badCategory = await service.UpdateAsync(transactionId, Request("Renamed", Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TransactionUpdateOutcome.NotFound, missing.Outcome);
        Assert.Null(missing.Transaction);
        Assert.Equal(TransactionUpdateOutcome.UnknownCategory, badCategory.Outcome);
        Assert.Null(badCategory.Transaction);

        await using var verification = await database.CreateContextAsync();
        var stored = await verification.Transactions.SingleAsync();
        Assert.Equal("Cash lunch", stored.Description);
    }

    [Fact]
    public async Task MatchingDescriptionsCanBeListedAndCategorizedTogether()
    {
        await using var database = await TestDatabase.CreateAsync();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.Add(Manual(firstId, "Coffee shop"));
            context.Transactions.Add(Manual(secondId, "Coffee shop"));
            context.Transactions.Add(Manual(Guid.NewGuid(), "Different shop"));
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var matches = await service.FindMatchingAsync(new MatchingTransactionQuery
        {
            Description = "  Coffee shop  ",
            ExcludeId = firstId
        }, CancellationToken.None);

        Assert.Single(matches);
        Assert.Equal(secondId, matches[0].Id);

        var update = await service.UpdateCategoryForDescriptionAsync(new BulkCategoryRequest
        {
            Description = "Coffee shop",
            CategoryId = FoodCategoryId
        }, CancellationToken.None);

        Assert.Equal(TransactionUpdateOutcome.Updated, update.Outcome);
        Assert.Equal(2, update.UpdatedCount);

        await using var verification = await database.CreateContextAsync();
        var stored = await verification.Transactions.OrderBy(transaction => transaction.Description).ToListAsync();
        Assert.All(stored.Where(transaction => transaction.Description == "Coffee shop"), transaction => Assert.Equal(FoodCategoryId, transaction.CategoryId));
        Assert.Null(stored.Single(transaction => transaction.Description == "Different shop").CategoryId);
    }

    [Fact]
    public async Task ImportedTransactionIsEditableWithoutDisturbingProvenance()
    {
        await using var database = await TestDatabase.CreateAsync();
        var transactionId = Guid.NewGuid();
        var importId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            context.DocumentImports.Add(new DocumentImport
            {
                Id = importId,
                ContentHash = new string('c', 64),
                ImportedAt = DateTimeOffset.UtcNow
            });
            context.Transactions.Add(new ExpenseTransaction
            {
                Id = transactionId,
                Origin = TransactionOrigin.Imported,
                DocumentImportId = importId,
                ImportPosition = 3,
                SourceFormat = SourceDocumentFormat.BankStatement,
                TransactionDate = new DateOnly(2026, 8, 2),
                Description = "UPI/1234",
                Direction = TransactionDirection.Debit,
                Amount = 250m,
                BalanceAfter = 5000m,
                LineExtractionStatus = LineExtractionStatus.Unavailable
            });
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var result = await service.UpdateAsync(transactionId, Request("Corner shop", FoodCategoryId), CancellationToken.None);

        Assert.Equal(TransactionUpdateOutcome.Updated, result.Outcome);

        await using var verification = await database.CreateContextAsync();
        var stored = await verification.Transactions.SingleAsync();
        Assert.Equal("Corner shop", stored.Description);
        Assert.Equal(TransactionOrigin.Imported, stored.Origin);
        Assert.Equal(importId, stored.DocumentImportId);
        Assert.Equal(3, stored.ImportPosition);
        Assert.Equal(SourceDocumentFormat.BankStatement, stored.SourceFormat);
        Assert.Equal(5000m, stored.BalanceAfter);
        Assert.Equal("INR", stored.Currency);
        Assert.Equal(LineExtractionStatus.Unavailable, stored.LineExtractionStatus);
    }

    private static TransactionUpdateRequest Request(string description, Guid? categoryId = null) =>
        new()
        {
            TransactionDate = new DateOnly(2026, 8, 9),
            Description = description,
            Direction = TransactionDirection.Debit,
            Amount = 10m,
            CategoryId = categoryId
        };

    private static ExpenseTransaction Manual(Guid id, string description) =>
        new()
        {
            Id = id,
            Origin = TransactionOrigin.Manual,
            TransactionDate = new DateOnly(2026, 8, 2),
            Description = description,
            Direction = TransactionDirection.Debit,
            Amount = 250m,
            LineExtractionStatus = LineExtractionStatus.NotApplicable
        };
}
