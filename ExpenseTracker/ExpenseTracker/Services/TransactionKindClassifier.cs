using System.Text.RegularExpressions;
using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Services;

public static partial class TransactionKindClassifier
{
    public static TransactionKind Classify(string description, TransactionDirection direction) =>
        TransferPattern().IsMatch(description)
            ? TransactionKind.Transfer
            : direction == TransactionDirection.Credit ? TransactionKind.Income : TransactionKind.Expense;

    [GeneratedRegex("\\b(atm|cash withdrawal|cash wd|fund transfer|bank transfer|neft|imps|rtgs|upi transfer|card payment)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TransferPattern();
}