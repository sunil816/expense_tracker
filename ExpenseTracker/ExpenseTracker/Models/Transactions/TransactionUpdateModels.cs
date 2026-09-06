using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Models.Transactions;

public sealed class TransactionUpdateRequest
{
    [Required]
    public DateOnly? TransactionDate { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Note { get; set; }

    [Required]
    public TransactionDirection? Direction { get; set; }

    public TransactionKind? Kind { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal Amount { get; set; }

    [StringLength(200)]
    public string? AccountLabel { get; set; }

    [StringLength(200)]
    public string? ExternalReference { get; set; }

    public Guid? CategoryId { get; set; }

    [Url]
    [StringLength(2000)]
    public string? ReceiptUrl { get; set; }
}

public sealed class TransactionTagRequest
{
    [Required]
    public Guid? TagId { get; set; }
}

public sealed class DuplicateFlagDecisionRequest
{
    [Required]
    public DuplicateFlagState? State { get; set; }
}

public sealed class MatchingTransactionQuery
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    public Guid? ExcludeId { get; set; }
}

public sealed class BulkCategoryRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }
}

public sealed record BulkCategoryResponse(int UpdatedCount);

public enum TransactionTagUpdateOutcome
{
    Updated,
    NotFound,
    TagNotFound,
    AlreadyAssigned
}

public sealed record TransactionTagUpdateResult(
    TransactionTagUpdateOutcome Outcome,
    TransactionDetailResponse? Transaction);

public enum DuplicateFlagDecisionOutcome
{
    Updated,
    AlreadyDecided,
    NotFound,
    InvalidState,
    Conflict
}

public sealed record DuplicateFlagDecisionResult(
    DuplicateFlagDecisionOutcome Outcome,
    DuplicateFlagResponse? Flag);

public enum TransactionUpdateOutcome
{
    Updated,
    NotFound,
    UnknownCategory
}

public sealed record TransactionUpdateResult(
    TransactionUpdateOutcome Outcome,
    TransactionDetailResponse? Transaction);
