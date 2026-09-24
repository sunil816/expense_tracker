using System.Text.RegularExpressions;
using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Services;

public static partial class TransactionKindClassifier
{
    public static TransactionKindClassification ClassifyWithReason(string description, TransactionDirection direction)
    {
        var match = TransferPattern().Match(description);
        return match.Success
            ? new(TransactionKind.Transfer, "transfer-keyword", $"Matched keyword: {match.Value}")
            : new(direction == TransactionDirection.Credit ? TransactionKind.Income : TransactionKind.Expense, "direction", "Derived from transaction direction");
    }

    public static TransactionKind Classify(string description, TransactionDirection direction) =>
        ClassifyWithReason(description, direction).Kind;

    [GeneratedRegex("\\b(atm|cash withdrawal|cash wd|fund transfer|bank transfer|neft|imps|rtgs|upi transfer|card payment)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TransferPattern();
}

public sealed record TransactionKindClassification(TransactionKind Kind, string Source, string Reason);