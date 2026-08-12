using ExpenseTracker.Data;
using ExpenseTracker.Models.Extraction;
using ExpenseTracker.Models.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ExpenseTracker.Services;

public sealed class DocumentImportService(IDbContextFactory<ExpenseTrackerDbContext> contextFactory)
{
    private const string ContentHashIndex = "ix_document_imports_content_hash";

    public async Task<TransactionImportResult?> FindAsync(string contentHash, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var documentImport = await context.DocumentImports
            .AsNoTracking()
            .Include(import => import.Transactions)
            .SingleOrDefaultAsync(import => import.ContentHash == contentHash, cancellationToken);

        return documentImport is null ? null : ToResult(documentImport, isDuplicate: true);
    }

    public async Task<TransactionImportResult> SaveAsync(
        string contentHash,
        IReadOnlyList<ExtractedTransaction> extractedTransactions,
        CancellationToken cancellationToken)
    {
        var existing = await FindAsync(contentHash, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var documentImport = new DocumentImport
        {
            Id = Guid.NewGuid(),
            ContentHash = contentHash,
            ImportedAt = DateTimeOffset.UtcNow,
            Transactions = ImportedTransactionFactory.Create(extractedTransactions)
        };

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            context.DocumentImports.Add(documentImport);
            await context.SaveChangesAsync(cancellationToken);
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

    private static TransactionImportResult ToResult(DocumentImport documentImport, bool isDuplicate) =>
        new(
            documentImport.Id,
            isDuplicate,
            documentImport.ImportedAt,
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
                    transaction.Amount,
                    transaction.Currency,
                    transaction.BalanceAfter,
                    transaction.CategoryId,
                    transaction.ReceiptUrl,
                    transaction.LineExtractionStatus))
                .ToArray());
}
