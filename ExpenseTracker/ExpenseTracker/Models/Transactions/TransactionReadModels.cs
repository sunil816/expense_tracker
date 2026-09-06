using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Models.Transactions;

public sealed class TransactionListQuery
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public TransactionDirection? Direction { get; set; }

    public TransactionKind? Kind { get; set; }

    public Guid? CategoryId { get; set; }

    public bool? Uncategorized { get; set; }

    public TransactionOrigin? Origin { get; set; }

    public SourceDocumentFormat? SourceFormat { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 50;
}

public sealed record TransactionSummaryResponse(
    Guid Id,
    TransactionOrigin Origin,
    DateOnly TransactionDate,
    string Description,
    string? Note,
    string? AccountLabel,
    string? ExternalReference,
    TransactionDirection Direction,
    TransactionKind Kind,
    decimal Amount,
    string Currency,
    Guid? CategoryId,
    string? ReceiptUrl,
    LineExtractionStatus LineExtractionStatus,
    bool HasLines);

public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record TransactionTagResponse(
    Guid TagId,
    string Slug,
    string Name,
    TagAssignmentState State,
    TagAssignmentSource Source,
    DateTimeOffset DecidedAt);

public sealed record DuplicateFlagResponse(
    Guid FlagId,
    Guid TransactionId,
    Guid MatchedTransactionId,
    DuplicateMatchReason Reason,
    DuplicateFlagState State,
    DateTimeOffset SuggestedAt,
    DateTimeOffset? DecidedAt);

public sealed class DuplicateReviewQuery
{
    public DuplicateFlagState? State { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 50;
}

public sealed record DuplicateReviewTransactionResponse(
    Guid Id,
    TransactionOrigin Origin,
    DateOnly TransactionDate,
    string Description,
    string? AccountLabel,
    string? ExternalReference,
    TransactionDirection Direction,
    TransactionKind Kind,
    decimal Amount,
    string Currency,
    SourceDocumentFormat? SourceFormat);

public sealed record DuplicateReviewItemResponse(
    DuplicateFlagResponse Flag,
    DuplicateReviewTransactionResponse FlaggedTransaction,
    DuplicateReviewTransactionResponse MatchedTransaction);

public sealed record DuplicateReviewPageResponse(
    IReadOnlyList<DuplicateReviewItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record TransactionLineResponse(
    Guid Id,
    int Position,
    TransactionLineType LineType,
    string Description,
    decimal? Quantity,
    decimal? UnitAmount,
    decimal Amount,
    IReadOnlyList<TransactionTagResponse> Tags);

public sealed record TransactionDetailResponse(
    Guid Id,
    TransactionOrigin Origin,
    DateOnly TransactionDate,
    string Description,
    string? Note,
    string? AccountLabel,
    string? ExternalReference,
    TransactionDirection Direction,
    TransactionKind Kind,
    decimal Amount,
    string Currency,
    Guid? CategoryId,
    string? ReceiptUrl,
    LineExtractionStatus LineExtractionStatus,
    Guid? DocumentImportId,
    int? ImportPosition,
    SourceDocumentFormat? SourceFormat,
    decimal? BalanceAfter,
    IReadOnlyList<TransactionLineResponse> Lines,
    IReadOnlyList<TransactionTagResponse> Tags);
