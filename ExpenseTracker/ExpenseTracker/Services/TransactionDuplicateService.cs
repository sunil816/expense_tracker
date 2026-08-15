using System.Text;
using ExpenseTracker.Data;
using ExpenseTracker.Models.Extraction;
using ExpenseTracker.Models.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services;

public sealed record DuplicateMatch(Guid MatchedTransactionId, DuplicateMatchReason Reason);

public interface ITransactionDuplicateService
{
    Task<IReadOnlyList<DuplicateMatch?>> ClassifyAsync(
        SourceProvider provider,
        IReadOnlyList<ExtractedTransaction> incoming,
        DateTimeOffset importStartedAt,
        CancellationToken cancellationToken);
}

public sealed class TransactionDuplicateService(IDbContextFactory<ExpenseTrackerDbContext> contextFactory)
    : ITransactionDuplicateService
{
    public async Task<IReadOnlyList<DuplicateMatch?>> ClassifyAsync(
        SourceProvider provider,
        IReadOnlyList<ExtractedTransaction> incoming,
        DateTimeOffset importStartedAt,
        CancellationToken cancellationToken)
    {
        _ = provider;
        _ = importStartedAt;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var matches = new DuplicateMatch?[incoming.Count];

        for (var index = 0; index < incoming.Count; index++)
        {
            var transaction = incoming[index];
            var candidates = await context.Transactions
                .AsNoTracking()
                .Where(candidate =>
                    candidate.TransactionDate == transaction.TransactionDate
                    && candidate.Amount == transaction.Amount
                    && candidate.Direction == transaction.Direction
                    && candidate.Currency == "INR")
                .ToListAsync(cancellationToken);

            var reference = Normalize(transaction.ExternalReference);
            var referenceMatch = !string.IsNullOrEmpty(reference)
                ? candidates.Where(candidate => string.Equals(
                    Normalize(candidate.ExternalReference), reference, StringComparison.OrdinalIgnoreCase))
                : [];

            var matched = SelectCandidate(referenceMatch, transaction.TransactionDate);
            if (matched is not null)
            {
                matches[index] = new(matched.Id, DuplicateMatchReason.ExternalReference);
                continue;
            }

            var description = Normalize(transaction.Description);
            var account = Normalize(transaction.AccountLabel);
            var compositeMatches = candidates.Where(candidate =>
                string.Equals(Normalize(candidate.Description), description, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Normalize(candidate.AccountLabel), account, StringComparison.OrdinalIgnoreCase));
            matched = SelectCandidate(compositeMatches, transaction.TransactionDate);
            if (matched is not null)
            {
                matches[index] = new(matched.Id, DuplicateMatchReason.Composite);
            }
        }

        return matches;
    }

    private static ExpenseTransaction? SelectCandidate(
        IEnumerable<ExpenseTransaction> candidates,
        DateOnly incomingDate) =>
        candidates
            .OrderBy(candidate => Math.Abs(candidate.TransactionDate.DayNumber - incomingDate.DayNumber))
            .ThenBy(candidate => candidate.Id)
            .FirstOrDefault();

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToUpperInvariant(character));
                pendingWhitespace = false;
            }
            else if (char.IsWhiteSpace(character))
            {
                pendingWhitespace = true;
            }
        }

        return builder.ToString();
    }
}