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

public sealed class TransactionSuggestionTests
{
    [Fact]
    public async Task RejectRestoresPreviousKindAndClosesSuggestion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var transactionId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            var import = new DocumentImport
            {
                Id = Guid.NewGuid(),
                ContentHash = new string('s', 64),
                ImportedAt = DateTimeOffset.UtcNow,
                Provider = SourceProvider.BankStatement
            };
            var transaction = new ExpenseTransaction
            {
                Id = transactionId,
                Origin = TransactionOrigin.Imported,
                DocumentImport = import,
                ImportPosition = 0,
                SourceFormat = SourceDocumentFormat.BankStatement,
                TransactionDate = new DateOnly(2026, 8, 12),
                Description = "ATM CASH WITHDRAWAL",
                Direction = TransactionDirection.Debit,
                Kind = TransactionKind.Transfer,
                Amount = 500m,
                LineExtractionStatus = LineExtractionStatus.NotApplicable
            };
            transaction.Suggestions.Add(new TransactionSuggestion
            {
                Id = suggestionId,
                Field = SuggestionField.Kind,
                PreviousKind = TransactionKind.Expense,
                SuggestedKind = TransactionKind.Transfer,
                State = SuggestionState.Suggested,
                Source = "transfer-keyword",
                Reason = "Matched keyword: ATM",
                SuggestedAt = DateTimeOffset.UtcNow
            });
            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionSuggestionService>();
        var result = await service.DecideAsync(
            transactionId,
            suggestionId,
            SuggestionState.Rejected,
            null,
            CancellationToken.None);

        Assert.Equal(TransactionSuggestionDecisionOutcome.Updated, result.Outcome);
        Assert.Equal(SuggestionState.Rejected, result.Suggestion!.State);

        await using var verification = await database.CreateContextAsync();
        var stored = await verification.Transactions
            .Include(transaction => transaction.Suggestions)
            .SingleAsync();
        Assert.Equal(TransactionKind.Expense, stored.Kind);
        var suggestion = Assert.Single(stored.Suggestions);
        Assert.Equal(TransactionKind.Expense, suggestion.ResolvedKind);
        Assert.NotNull(suggestion.DecidedAt);
    }

    [Fact]
    public async Task AcceptingTheSameDecisionIsIdempotent()
    {
        await using var database = await TestDatabase.CreateAsync();
        var transactionId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            var transaction = Imported(transactionId, "IMPS TO SAVINGS", TransactionKind.Transfer);
            transaction.DocumentImport = new DocumentImport
            {
                Id = Guid.NewGuid(),
                ContentHash = new string('t', 64),
                ImportedAt = DateTimeOffset.UtcNow,
                Provider = SourceProvider.BankStatement
            };
            transaction.ImportPosition = 0;
            transaction.SourceFormat = SourceDocumentFormat.BankStatement;
            transaction.Suggestions.Add(Suggestion(suggestionId, TransactionKind.Expense, TransactionKind.Transfer));
            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionSuggestionService>();
        var first = await service.DecideAsync(transactionId, suggestionId, SuggestionState.Confirmed, null, CancellationToken.None);
        var second = await service.DecideAsync(transactionId, suggestionId, SuggestionState.Confirmed, null, CancellationToken.None);

        Assert.Equal(TransactionSuggestionDecisionOutcome.Updated, first.Outcome);
        Assert.Equal(TransactionSuggestionDecisionOutcome.AlreadyDecided, second.Outcome);
    }

    [Fact]
    public async Task DirectKindEditResolvesPendingSuggestion()
    {
        await using var database = await TestDatabase.CreateAsync();
        var transactionId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        await using (var context = await database.CreateContextAsync())
        {
            var transaction = Imported(transactionId, "ATM CASH WITHDRAWAL", TransactionKind.Transfer);
            transaction.DocumentImport = new DocumentImport
            {
                Id = Guid.NewGuid(),
                ContentHash = new string('u', 64),
                ImportedAt = DateTimeOffset.UtcNow,
                Provider = SourceProvider.BankStatement
            };
            transaction.ImportPosition = 0;
            transaction.SourceFormat = SourceDocumentFormat.BankStatement;
            transaction.Suggestions.Add(Suggestion(suggestionId, TransactionKind.Expense, TransactionKind.Transfer));
            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
        }

        var service = database.Services.GetRequiredService<TransactionService>();
        var result = await service.UpdateAsync(
            transactionId,
            new TransactionUpdateRequest
            {
                TransactionDate = new DateOnly(2026, 8, 12),
                Description = "ATM CASH WITHDRAWAL",
                Direction = TransactionDirection.Debit,
                Kind = TransactionKind.Expense,
                Amount = 500m
            },
            CancellationToken.None);

        Assert.Equal(TransactionUpdateOutcome.Updated, result.Outcome);
        await using var verification = await database.CreateContextAsync();
        var stored = await verification.TransactionSuggestions.SingleAsync();
        Assert.Equal(SuggestionState.Rejected, stored.State);
        Assert.Equal(TransactionKind.Expense, stored.ResolvedKind);
    }

    private static ExpenseTransaction Imported(Guid id, string description, TransactionKind kind) =>
        new()
        {
            Id = id,
            Origin = TransactionOrigin.Imported,
            TransactionDate = new DateOnly(2026, 8, 12),
            Description = description,
            Direction = TransactionDirection.Debit,
            Kind = kind,
            Amount = 500m,
            LineExtractionStatus = LineExtractionStatus.NotApplicable
        };

    private static TransactionSuggestion Suggestion(Guid id, TransactionKind previous, TransactionKind suggested) =>
        new()
        {
            Id = id,
            Field = SuggestionField.Kind,
            PreviousKind = previous,
            SuggestedKind = suggested,
            State = SuggestionState.Suggested,
            Source = "transfer-keyword",
            Reason = "Matched keyword: transfer",
            SuggestedAt = DateTimeOffset.UtcNow
        };
}