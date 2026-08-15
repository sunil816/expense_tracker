using ExpenseTracker.Data;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services;

public sealed class TransactionService(IDbContextFactory<ExpenseTrackerDbContext> contextFactory)
{
    /// <summary>Returns null when the requested category does not exist.</summary>
    public async Task<ManualTransactionResponse?> CreateManualAsync(
        ManualTransactionRequest request,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (request.CategoryId is { } categoryId
            && !await context.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
        {
            return null;
        }

        var transaction = new ExpenseTransaction
        {
            Id = Guid.NewGuid(),
            Origin = TransactionOrigin.Manual,
            TransactionDate = request.TransactionDate!.Value,
            Description = request.Description.Trim(),
            Note = NullIfWhiteSpace(request.Note),
            AccountLabel = NullIfWhiteSpace(request.AccountLabel),
            ExternalReference = NullIfWhiteSpace(request.ExternalReference),
            Direction = request.Direction!.Value,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            ReceiptUrl = NullIfWhiteSpace(request.ReceiptUrl),
            LineExtractionStatus = LineExtractionStatus.NotApplicable
        };

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);

        return new ManualTransactionResponse(
            transaction.Id,
            transaction.TransactionDate,
            transaction.Description,
            transaction.Note,
            transaction.AccountLabel,
            transaction.ExternalReference,
            transaction.Direction,
            transaction.Amount,
            transaction.Currency,
            transaction.CategoryId,
            transaction.ReceiptUrl);
    }

    public async Task<TransactionPageResponse> ListAsync(TransactionListQuery query, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var transactions = context.Transactions.AsNoTracking().ExcludingConfirmedDuplicates();

        if (query.From is { } from)
        {
            transactions = transactions.Where(transaction => transaction.TransactionDate >= from);
        }

        if (query.To is { } to)
        {
            transactions = transactions.Where(transaction => transaction.TransactionDate <= to);
        }

        if (query.Direction is { } direction)
        {
            transactions = transactions.Where(transaction => transaction.Direction == direction);
        }

        if (query.Uncategorized is true)
        {
            transactions = transactions.Where(transaction => transaction.CategoryId == null);
        }
        else if (query.CategoryId is { } categoryId)
        {
            transactions = transactions.Where(transaction => transaction.CategoryId == categoryId);
        }

        if (query.Origin is { } origin)
        {
            transactions = transactions.Where(transaction => transaction.Origin == origin);
        }

        if (query.SourceFormat is { } sourceFormat)
        {
            transactions = transactions.Where(transaction => transaction.SourceFormat == sourceFormat);
        }

        var totalCount = await transactions.CountAsync(cancellationToken);
        var items = await transactions
            .OrderByDescending(transaction => transaction.TransactionDate)
            .ThenBy(transaction => transaction.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(transaction => new TransactionSummaryResponse(
                transaction.Id,
                transaction.Origin,
                transaction.TransactionDate,
                transaction.Description,
                transaction.Note,
                transaction.AccountLabel,
                transaction.ExternalReference,
                transaction.Direction,
                transaction.Amount,
                transaction.Currency,
                transaction.CategoryId,
                transaction.ReceiptUrl,
                transaction.LineExtractionStatus,
                transaction.Lines.Any()))
            .ToListAsync(cancellationToken);

        return new TransactionPageResponse(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<TransactionDetailResponse?> GetAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await LoadDetailAsync(context, transactionId, cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionSummaryResponse>> FindMatchingAsync(
        MatchingTransactionQuery query,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var description = query.Description.Trim();
        var transactions = context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.Description == description);

        if (query.ExcludeId is { } excludeId)
        {
            transactions = transactions.Where(transaction => transaction.Id != excludeId);
        }

        return await transactions
            .OrderByDescending(transaction => transaction.TransactionDate)
            .ThenBy(transaction => transaction.Id)
            .Select(transaction => new TransactionSummaryResponse(
                transaction.Id,
                transaction.Origin,
                transaction.TransactionDate,
                transaction.Description,
                transaction.Note,
                transaction.AccountLabel,
                transaction.ExternalReference,
                transaction.Direction,
                transaction.Amount,
                transaction.Currency,
                transaction.CategoryId,
                transaction.ReceiptUrl,
                transaction.LineExtractionStatus,
                transaction.Lines.Any()))
            .ToListAsync(cancellationToken);
    }

    public async Task<(TransactionUpdateOutcome Outcome, int UpdatedCount)> UpdateCategoryForDescriptionAsync(
        BulkCategoryRequest request,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (request.CategoryId is { } categoryId
            && !await context.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
        {
            return (TransactionUpdateOutcome.UnknownCategory, 0);
        }

        var description = request.Description.Trim();
        var transactions = await context.Transactions
            .Where(transaction => transaction.Description == description)
            .ToListAsync(cancellationToken);
        foreach (var transaction in transactions)
        {
            transaction.CategoryId = request.CategoryId;
        }

        await context.SaveChangesAsync(cancellationToken);
        return (TransactionUpdateOutcome.Updated, transactions.Count);
    }

    public async Task<TransactionTagUpdateResult> AddTagAsync(Guid transactionId, Guid tagId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await context.Transactions.AnyAsync(transaction => transaction.Id == transactionId, cancellationToken))
        {
            return new TransactionTagUpdateResult(TransactionTagUpdateOutcome.NotFound, null);
        }

        if (!await context.Tags.AnyAsync(tag => tag.Id == tagId, cancellationToken))
        {
            return new TransactionTagUpdateResult(TransactionTagUpdateOutcome.TagNotFound, null);
        }

        var assignment = await context.TransactionTags
            .SingleOrDefaultAsync(candidate => candidate.TransactionId == transactionId && candidate.TagId == tagId, cancellationToken);
        if (assignment is null)
        {
            context.TransactionTags.Add(new TransactionTag
            {
                TransactionId = transactionId,
                TagId = tagId,
                State = TagAssignmentState.Confirmed,
                Source = TagAssignmentSource.Manual,
                DecidedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        return new TransactionTagUpdateResult(
            assignment is null ? TransactionTagUpdateOutcome.Updated : TransactionTagUpdateOutcome.AlreadyAssigned,
            await LoadDetailAsync(context, transactionId, cancellationToken));
    }

    public async Task<TransactionTagUpdateResult> RemoveTagAsync(Guid transactionId, Guid tagId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var assignment = await context.TransactionTags
            .SingleOrDefaultAsync(candidate => candidate.TransactionId == transactionId && candidate.TagId == tagId, cancellationToken);
        if (assignment is null)
        {
            return new TransactionTagUpdateResult(TransactionTagUpdateOutcome.NotFound, null);
        }

        context.TransactionTags.Remove(assignment);
        await context.SaveChangesAsync(cancellationToken);
        return new TransactionTagUpdateResult(
            TransactionTagUpdateOutcome.Updated,
            await LoadDetailAsync(context, transactionId, cancellationToken));
    }

    public async Task<DuplicateFlagDecisionResult> DecideDuplicateFlagAsync(
        Guid transactionId,
        DuplicateFlagState state,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var flag = await context.TransactionDuplicateFlags
            .SingleOrDefaultAsync(candidate => candidate.TransactionId == transactionId, cancellationToken);
        if (flag is null)
        {
            return new DuplicateFlagDecisionResult(DuplicateFlagDecisionOutcome.NotFound, null);
        }

        if (state is not DuplicateFlagState.Confirmed and not DuplicateFlagState.Rejected)
        {
            return new DuplicateFlagDecisionResult(DuplicateFlagDecisionOutcome.InvalidState, ToDuplicateFlagResponse(flag));
        }

        if (flag.State == state)
        {
            return new DuplicateFlagDecisionResult(DuplicateFlagDecisionOutcome.AlreadyDecided, ToDuplicateFlagResponse(flag));
        }

        if (flag.State != DuplicateFlagState.Suggested)
        {
            return new DuplicateFlagDecisionResult(DuplicateFlagDecisionOutcome.Conflict, ToDuplicateFlagResponse(flag));
        }

        flag.State = state;
        flag.DecidedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return new DuplicateFlagDecisionResult(DuplicateFlagDecisionOutcome.Updated, ToDuplicateFlagResponse(flag));
    }

    /// <summary>Replaces the user-editable columns; provenance, currency, lines and tags are left untouched.</summary>
    public async Task<TransactionUpdateResult> UpdateAsync(
        Guid transactionId,
        TransactionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await context.Transactions
            .SingleOrDefaultAsync(candidate => candidate.Id == transactionId, cancellationToken);
        if (transaction is null)
        {
            return new TransactionUpdateResult(TransactionUpdateOutcome.NotFound, null);
        }

        if (request.CategoryId is { } categoryId
            && !await context.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
        {
            return new TransactionUpdateResult(TransactionUpdateOutcome.UnknownCategory, null);
        }

        transaction.TransactionDate = request.TransactionDate!.Value;
        transaction.Description = request.Description.Trim();
        transaction.Note = NullIfWhiteSpace(request.Note);
        transaction.Direction = request.Direction!.Value;
        transaction.Amount = request.Amount;
        transaction.AccountLabel = NullIfWhiteSpace(request.AccountLabel);
        transaction.ExternalReference = NullIfWhiteSpace(request.ExternalReference);
        transaction.CategoryId = request.CategoryId;
        transaction.ReceiptUrl = NullIfWhiteSpace(request.ReceiptUrl);
        await context.SaveChangesAsync(cancellationToken);

        var detail = await LoadDetailAsync(context, transactionId, cancellationToken);
        return new TransactionUpdateResult(TransactionUpdateOutcome.Updated, detail);
    }

    private static async Task<TransactionDetailResponse?> LoadDetailAsync(
        ExpenseTrackerDbContext context,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var transaction = await context.Transactions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(candidate => candidate.Lines).ThenInclude(line => line.TagAssignments).ThenInclude(assignment => assignment.Tag)
            .Include(candidate => candidate.TagAssignments).ThenInclude(assignment => assignment.Tag)
            .SingleOrDefaultAsync(candidate => candidate.Id == transactionId, cancellationToken);

        return transaction is null ? null : new TransactionDetailResponse(
            transaction.Id,
            transaction.Origin,
            transaction.TransactionDate,
            transaction.Description,
            transaction.Note,
            transaction.AccountLabel,
            transaction.ExternalReference,
            transaction.Direction,
            transaction.Amount,
            transaction.Currency,
            transaction.CategoryId,
            transaction.ReceiptUrl,
            transaction.LineExtractionStatus,
            transaction.DocumentImportId,
            transaction.ImportPosition,
            transaction.SourceFormat,
            transaction.BalanceAfter,
            [.. transaction.Lines
                .OrderBy(line => line.Position)
                .Select(line => new TransactionLineResponse(
                    line.Id,
                    line.Position,
                    line.LineType,
                    line.Description,
                    line.Quantity,
                    line.UnitAmount,
                    line.Amount,
                    ToTagResponses(line.TagAssignments)))],
            ToTagResponses(transaction.TagAssignments));
    }

    private static IReadOnlyList<TransactionTagResponse> ToTagResponses<TAssignment>(List<TAssignment> assignments)
        where TAssignment : ITagAssignment =>
        [.. assignments
            .Select(assignment => new TransactionTagResponse(
                assignment.TagId,
                assignment.Tag.Slug,
                assignment.Tag.Name,
                assignment.State,
                assignment.Source,
                assignment.DecidedAt))
            .OrderBy(tag => tag.Name)];

    private static DuplicateFlagResponse ToDuplicateFlagResponse(TransactionDuplicateFlag flag) =>
        new(flag.Id, flag.TransactionId, flag.MatchedTransactionId, flag.Reason, flag.State, flag.SuggestedAt, flag.DecidedAt);

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
