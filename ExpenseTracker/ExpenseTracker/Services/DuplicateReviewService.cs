using ExpenseTracker.Data;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services;

public sealed class DuplicateReviewService(IDbContextFactory<ExpenseTrackerDbContext> contextFactory)
{
    public async Task<DuplicateReviewPageResponse> ListAsync(
        DuplicateReviewQuery query,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var flags = context.TransactionDuplicateFlags
            .AsNoTracking()
            .Where(flag => query.State == null || flag.State == query.State);

        var allFlags = await flags
            .Include(flag => flag.Transaction)
            .Include(flag => flag.MatchedTransaction)
            .ToListAsync(cancellationToken);
        var orderedFlags = allFlags
            .OrderByDescending(flag => flag.State == DuplicateFlagState.Suggested)
            .ThenByDescending(flag => flag.SuggestedAt)
            .ThenBy(flag => flag.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);
        var totalCount = allFlags.Count;
        var items = orderedFlags
            .Select(flag => new DuplicateReviewItemResponse(
                new DuplicateFlagResponse(
                    flag.Id,
                    flag.TransactionId,
                    flag.MatchedTransactionId,
                    flag.Reason,
                    flag.State,
                    flag.SuggestedAt,
                    flag.DecidedAt),
                new DuplicateReviewTransactionResponse(
                    flag.Transaction.Id,
                    flag.Transaction.Origin,
                    flag.Transaction.TransactionDate,
                    flag.Transaction.Description,
                    flag.Transaction.AccountLabel,
                    flag.Transaction.ExternalReference,
                    flag.Transaction.Direction,
                    flag.Transaction.Kind,
                    flag.Transaction.Amount,
                    flag.Transaction.Currency,
                    flag.Transaction.SourceFormat),
                new DuplicateReviewTransactionResponse(
                    flag.MatchedTransaction.Id,
                    flag.MatchedTransaction.Origin,
                    flag.MatchedTransaction.TransactionDate,
                    flag.MatchedTransaction.Description,
                    flag.MatchedTransaction.AccountLabel,
                    flag.MatchedTransaction.ExternalReference,
                    flag.MatchedTransaction.Direction,
                    flag.MatchedTransaction.Kind,
                    flag.MatchedTransaction.Amount,
                    flag.MatchedTransaction.Currency,
                    flag.MatchedTransaction.SourceFormat)))
            .ToList();

        return new DuplicateReviewPageResponse(items, query.Page, query.PageSize, totalCount);
    }
}