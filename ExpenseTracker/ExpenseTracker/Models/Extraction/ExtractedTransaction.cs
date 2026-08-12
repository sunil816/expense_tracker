using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Models.Extraction;

public sealed record ExtractedTransaction(
    int? SourceSequence,
    SourceDocumentFormat SourceFormat,
    DateOnly TransactionDate,
    string Description,
    string? AccountLabel,
    string? ExternalReference,
    TransactionDirection Direction,
    decimal Amount,
    decimal? BalanceAfter,
    string? ReceiptUrl);

public sealed record TransactionImportResult(
    Guid ImportId,
    bool IsDuplicate,
    DateTimeOffset ImportedAt,
    IReadOnlyList<SavedTransaction> Transactions);

public sealed record SavedTransaction(
    Guid Id,
    int ImportPosition,
    int? SourceSequence,
    SourceDocumentFormat SourceFormat,
    DateOnly TransactionDate,
    string Description,
    string? AccountLabel,
    string? ExternalReference,
    TransactionDirection Direction,
    decimal Amount,
    string Currency,
    decimal? BalanceAfter,
    Guid? CategoryId,
    string? ReceiptUrl,
    LineExtractionStatus LineExtractionStatus);