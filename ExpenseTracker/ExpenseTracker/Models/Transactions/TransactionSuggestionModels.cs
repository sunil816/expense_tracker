using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Models.Transactions;

public sealed class TransactionSuggestionBulkDecisionRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyList<Guid> SuggestionIds { get; set; } = [];

    [Required]
    public SuggestionState? State { get; set; }
}

public sealed record TransactionSuggestionBulkDecisionResponse(int UpdatedCount);