using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Services;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class TransactionKindClassifierTests
{
    [Theory]
    [InlineData("ATM CASH WITHDRAWAL", TransactionDirection.Debit, TransactionKind.Transfer)]
    [InlineData("IMPS TO SAVINGS", TransactionDirection.Debit, TransactionKind.Transfer)]
    [InlineData("CARD PAYMENT", TransactionDirection.Debit, TransactionKind.Transfer)]
    [InlineData("Salary August", TransactionDirection.Credit, TransactionKind.Income)]
    [InlineData("Grocery store", TransactionDirection.Debit, TransactionKind.Expense)]
    [InlineData("Refund from store", TransactionDirection.Credit, TransactionKind.Income)]
    public void ClassifiesDescription(string description, TransactionDirection direction, TransactionKind expected) =>
        Assert.Equal(expected, TransactionKindClassifier.Classify(description, direction));
}