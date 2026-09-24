using ExpenseTracker.Data;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services;

public sealed class TransactionSuggestionService(IDbContextFactory<ExpenseTrackerDbContext> contextFactory)
{
    public async Task<TransactionSuggestionPageResponse> ListAsync(
        TransactionSuggestionQuery query,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var suggestions = context.TransactionSuggestions
            .AsNoTracking()
            .Include(suggestion => suggestion.Transaction)
            .Where(suggestion => query.State == null || suggestion.State == query.State);
        var allSuggestions = await suggestions.ToListAsync(cancellationToken);
        var orderedSuggestions = allSuggestions
            .OrderByDescending(suggestion => suggestion.State == SuggestionState.Suggested)
            .ThenByDescending(suggestion => suggestion.SuggestedAt)
            .ThenBy(suggestion => suggestion.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);
        var items = orderedSuggestions
            .Select(suggestion => new TransactionSuggestionItemResponse(
                ToResponse(suggestion),
                new TransactionSummaryResponse(
                    suggestion.Transaction.Id,
                    suggestion.Transaction.Origin,
                    suggestion.Transaction.TransactionDate,
                    suggestion.Transaction.Description,
                    suggestion.Transaction.Note,
                    suggestion.Transaction.AccountLabel,
                    suggestion.Transaction.ExternalReference,
                    suggestion.Transaction.Direction,
                    suggestion.Transaction.Kind,
                    suggestion.Transaction.Amount,
                    suggestion.Transaction.Currency,
                    suggestion.Transaction.CategoryId,
                    suggestion.Transaction.ReceiptUrl,
                    suggestion.Transaction.LineExtractionStatus,
                    suggestion.Transaction.Lines.Count > 0)))
            .ToList();

        return new TransactionSuggestionPageResponse(items, query.Page, query.PageSize, allSuggestions.Count);
    }

    public async Task<TransactionSuggestionDecisionResult> DecideAsync(
        Guid transactionId,
        Guid suggestionId,
        SuggestionState state,
        TransactionKind? resolvedKind,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var suggestion = await context.TransactionSuggestions
            .SingleOrDefaultAsync(candidate => candidate.Id == suggestionId && candidate.TransactionId == transactionId, cancellationToken);
        if (suggestion is null)
        {
            return new(TransactionSuggestionDecisionOutcome.NotFound, null);
        }

        if (state is not SuggestionState.Confirmed and not SuggestionState.Rejected and not SuggestionState.Edited)
        {
            return new(TransactionSuggestionDecisionOutcome.InvalidState, ToResponse(suggestion));
        }

        if (suggestion.State == state
            && (state != SuggestionState.Edited || suggestion.ResolvedKind == resolvedKind))
        {
            return new(TransactionSuggestionDecisionOutcome.AlreadyDecided, ToResponse(suggestion));
        }

        if (suggestion.State != SuggestionState.Suggested)
        {
            return new(TransactionSuggestionDecisionOutcome.Conflict, ToResponse(suggestion));
        }

        if (suggestion.Field != SuggestionField.Kind)
        {
            return new(TransactionSuggestionDecisionOutcome.UnsupportedField, ToResponse(suggestion));
        }

        var transaction = await context.Transactions
            .SingleAsync(candidate => candidate.Id == transactionId, cancellationToken);
        var nextKind = state switch
        {
            SuggestionState.Confirmed => suggestion.SuggestedKind!.Value,
            SuggestionState.Rejected => suggestion.PreviousKind!.Value,
            SuggestionState.Edited when resolvedKind is not null => resolvedKind.Value,
            _ => (TransactionKind?)null
        };
        if (nextKind is null)
        {
            return new(TransactionSuggestionDecisionOutcome.MissingResolvedKind, ToResponse(suggestion));
        }

        transaction.Kind = nextKind.Value;
        if (transaction.Kind == TransactionKind.Transfer)
        {
            transaction.CategoryId = null;
        }

        suggestion.State = state;
        suggestion.ResolvedKind = nextKind;
        suggestion.DecidedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return new(TransactionSuggestionDecisionOutcome.Updated, ToResponse(suggestion));
    }

    public async Task<TransactionSuggestionBulkDecisionResponse?> BulkDecideAsync(
        IReadOnlyList<Guid> suggestionIds,
        SuggestionState state,
        CancellationToken cancellationToken)
    {
        if (state is not SuggestionState.Confirmed and not SuggestionState.Rejected)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var suggestions = await context.TransactionSuggestions
            .Include(suggestion => suggestion.Transaction)
            .Where(suggestion => suggestionIds.Contains(suggestion.Id))
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var updatedCount = 0;
        foreach (var suggestion in suggestions.Where(suggestion => suggestion.State == SuggestionState.Suggested))
        {
            var nextKind = state == SuggestionState.Confirmed
                ? suggestion.SuggestedKind!.Value
                : suggestion.PreviousKind!.Value;
            suggestion.Transaction.Kind = nextKind;
            if (nextKind == TransactionKind.Transfer)
            {
                suggestion.Transaction.CategoryId = null;
            }

            suggestion.State = state;
            suggestion.ResolvedKind = nextKind;
            suggestion.DecidedAt = now;
            updatedCount++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return new TransactionSuggestionBulkDecisionResponse(updatedCount);
    }

    private static TransactionSuggestionResponse ToResponse(TransactionSuggestion suggestion) =>
        new(
            suggestion.Id,
            suggestion.TransactionId,
            suggestion.Field,
            suggestion.PreviousKind,
            suggestion.SuggestedKind,
            suggestion.ResolvedKind,
            suggestion.State,
            suggestion.Source,
            suggestion.Reason,
            suggestion.SuggestedAt,
            suggestion.DecidedAt);
}