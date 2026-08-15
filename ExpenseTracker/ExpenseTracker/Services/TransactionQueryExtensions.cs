using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Services;

public static class TransactionQueryExtensions
{
    public static IQueryable<ExpenseTransaction> ExcludingConfirmedDuplicates(
        this IQueryable<ExpenseTransaction> transactions) =>
        transactions.Where(transaction => !transaction.DuplicateFlags.Any(flag => flag.State == DuplicateFlagState.Confirmed));
}