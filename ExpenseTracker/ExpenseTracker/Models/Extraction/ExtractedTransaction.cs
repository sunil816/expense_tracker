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
    SourceProvider Provider,
    int ImportedCount,
    int FlaggedDuplicateCount,
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
    TransactionKind Kind,
    decimal Amount,
    string Currency,
    decimal? BalanceAfter,
    Guid? CategoryId,
    string? ReceiptUrl,
    LineExtractionStatus LineExtractionStatus,
    DuplicateFlagDetails? DuplicateFlag);

public sealed record DuplicateFlagDetails(
    Guid FlagId,
    Guid MatchedTransactionId,
    DuplicateMatchReason Reason,
    DuplicateFlagState State,
    DateTimeOffset SuggestedAt,
    DateTimeOffset? DecidedAt);