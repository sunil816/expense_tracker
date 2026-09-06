using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Models.Transactions;

public sealed class ManualTransactionRequest
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

public sealed record ManualTransactionResponse(
    Guid Id,
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
    string? ReceiptUrl);
