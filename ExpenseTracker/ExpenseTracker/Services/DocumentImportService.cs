using ExpenseTracker.Data;
using ExpenseTracker.Models.Extraction;
using ExpenseTracker.Models.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ExpenseTracker.Services;

public sealed class DocumentImportService(
    IDbContextFactory<ExpenseTrackerDbContext> contextFactory,
    ITransactionDuplicateService duplicateService)
{
    private const string ContentHashIndex = "ix_document_imports_content_hash";

    public async Task<TransactionImportResult?> FindAsync(string contentHash, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var documentImport = await context.DocumentImports
            .AsNoTracking()
            .Include(import => import.Transactions)
            .ThenInclude(transaction => transaction.DuplicateFlags)
            .SingleOrDefaultAsync(import => import.ContentHash == contentHash, cancellationToken);

        return documentImport is null ? null : ToResult(documentImport, isDuplicate: true);
    }

    public async Task<TransactionImportResult> SaveAsync(
        string contentHash,
        IReadOnlyList<ExtractedTransaction> extractedTransactions,
        SourceProvider provider,
        CancellationToken cancellationToken)
    {
        var existing = await FindAsync(contentHash, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var importedAt = DateTimeOffset.UtcNow;

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var matches = await duplicateService.ClassifyAsync(provider, extractedTransactions, importedAt, cancellationToken);
            var transactions = ImportedTransactionFactory.Create(extractedTransactions);
            var documentImport = new DocumentImport
            {
                Id = Guid.NewGuid(),
                ContentHash = contentHash,
                ImportedAt = importedAt,
                Provider = provider,
                Transactions = transactions
            };

            for (var index = 0; index < transactions.Count; index++)
            {
                var match = matches[index];
                if (match is null)
                {
                    continue;
                }

                transactions[index].DuplicateFlags.Add(new TransactionDuplicateFlag
                {
                    Id = Guid.NewGuid(),
                    MatchedTransactionId = match.MatchedTransactionId,
                    Reason = match.Reason,
                    State = DuplicateFlagState.Suggested,
                    SuggestedAt = importedAt
                });
            }

            context.DocumentImports.Add(documentImport);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResult(documentImport, isDuplicate: false);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ContentHashIndex
            })
        {
            return await FindAsync(contentHash, cancellationToken)
                ?? throw new InvalidOperationException("The concurrent document import could not be loaded.", exception);
        }
    }

    public Task<TransactionImportResult> SaveAsync(
        string contentHash,
        IReadOnlyList<ExtractedTransaction> extractedTransactions,
        CancellationToken cancellationToken) =>
        SaveAsync(contentHash, extractedTransactions, SourceProvider.SuperMoney, cancellationToken);

    private static TransactionImportResult ToResult(DocumentImport documentImport, bool isDuplicate) =>
        new(
            documentImport.Id,
            isDuplicate,
            documentImport.ImportedAt,
            documentImport.Provider,
            documentImport.Transactions.Count,
            documentImport.Transactions.Count(transaction => transaction.DuplicateFlags.Count > 0),
            documentImport.Transactions
                .OrderBy(transaction => transaction.ImportPosition)
                .Select(transaction => new SavedTransaction(
                    transaction.Id,
                    transaction.ImportPosition!.Value,
                    transaction.SourceSequence,
                    transaction.SourceFormat!.Value,
                    transaction.TransactionDate,
                    transaction.Description,
                    transaction.AccountLabel,
                    transaction.ExternalReference,
                    transaction.Direction,
                    transaction.Kind,
                    transaction.Amount,
                    transaction.Currency,
                    transaction.BalanceAfter,
                    transaction.CategoryId,
                    transaction.ReceiptUrl,
                    transaction.LineExtractionStatus,
                    transaction.DuplicateFlags
                        .Select(flag => new DuplicateFlagDetails(
                            flag.Id,
                            flag.MatchedTransactionId,
                            flag.Reason,
                            flag.State,
                            flag.SuggestedAt,
                            flag.DecidedAt))
                        .SingleOrDefault()))
                .ToArray());

}
