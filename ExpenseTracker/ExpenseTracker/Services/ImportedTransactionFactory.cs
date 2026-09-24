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
            Kind = TransactionKindClassifier.ClassifyWithReason(transaction.Description, transaction.Direction).Kind,
            Amount = transaction.Amount,
            BalanceAfter = transaction.BalanceAfter,
            ReceiptUrl = transaction.ReceiptUrl,
            LineExtractionStatus = transaction.SourceFormat == SourceDocumentFormat.OrderHistory
                ? transaction.ReceiptUrl is null ? LineExtractionStatus.Unavailable : LineExtractionStatus.Available
                : LineExtractionStatus.NotApplicable
        })];

    public static List<TransactionSuggestion> CreateSuggestions(
        IReadOnlyList<ExpenseTransaction> transactions,
        DateTimeOffset suggestedAt) =>
        [..
        transactions.Select(transaction =>
        {
            var classification = TransactionKindClassifier.ClassifyWithReason(transaction.Description, transaction.Direction);
            var baseline = transaction.Direction == TransactionDirection.Credit ? TransactionKind.Income : TransactionKind.Expense;
            return classification.Kind == baseline
                ? null
                : new TransactionSuggestion
                {
                    Id = Guid.NewGuid(),
                    TransactionId = transaction.Id,
                    Field = SuggestionField.Kind,
                    PreviousKind = baseline,
                    SuggestedKind = classification.Kind,
                    State = SuggestionState.Suggested,
                    Source = classification.Source,
                    Reason = classification.Reason,
                    SuggestedAt = suggestedAt
                };
        }).Where(suggestion => suggestion is not null).Select(suggestion => suggestion!)];
}
