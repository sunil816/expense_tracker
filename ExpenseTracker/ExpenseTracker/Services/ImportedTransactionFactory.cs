using ExpenseTracker.Models.Extraction;
using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Services;

public static class ImportedTransactionFactory
{
    public static List<ExpenseTransaction> Create(IReadOnlyList<ExtractedTransaction> extractedTransactions) =>
        [.. extractedTransactions.Select((transaction, position) => new ExpenseTransaction
        {
            Id = Guid.NewGuid(),
            Origin = TransactionOrigin.Imported,
            ImportPosition = position,
            SourceSequence = transaction.SourceSequence,
            SourceFormat = transaction.SourceFormat,
            TransactionDate = transaction.TransactionDate,
            Description = transaction.Description,
            AccountLabel = transaction.AccountLabel,
            ExternalReference = transaction.ExternalReference,
            Direction = transaction.Direction,
            Amount = transaction.Amount,
            BalanceAfter = transaction.BalanceAfter,
            ReceiptUrl = transaction.ReceiptUrl,
            LineExtractionStatus = transaction.SourceFormat == SourceDocumentFormat.OrderHistory
                ? transaction.ReceiptUrl is null ? LineExtractionStatus.Unavailable : LineExtractionStatus.Available
                : LineExtractionStatus.NotApplicable
        })];
}
