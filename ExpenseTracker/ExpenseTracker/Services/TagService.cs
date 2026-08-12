using System.Text;
using ExpenseTracker.Data;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Tags;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services;

public sealed class TagService(IDbContextFactory<ExpenseTrackerDbContext> contextFactory)
{
    public async Task<IReadOnlyList<TagResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Tags
            .AsNoTracking()
            .OrderBy(tag => tag.Name)
            .Select(tag => new TagResponse(tag.Id, tag.Slug, tag.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<TagCreateResult> CreateAsync(TagRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var slug = ToSlug(name);
        if (slug.Length == 0)
        {
            return new TagCreateResult(TagCreateOutcome.UnusableName, null);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await context.Tags.AnyAsync(tag => tag.Slug == slug, cancellationToken))
        {
            return new TagCreateResult(TagCreateOutcome.SlugConflict, null);
        }

        var tag = new Tag { Id = Guid.NewGuid(), Slug = slug, Name = name };
        context.Tags.Add(tag);
        await context.SaveChangesAsync(cancellationToken);
        return new TagCreateResult(TagCreateOutcome.Created, new TagResponse(tag.Id, tag.Slug, tag.Name));
    }

    /// <summary>Renames the display name only; the slug stays stable so existing references keep resolving.</summary>
    public async Task<TagResponse?> RenameAsync(Guid tagId, TagRequest request, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tag = await context.Tags.SingleOrDefaultAsync(candidate => candidate.Id == tagId, cancellationToken);
        if (tag is null)
        {
            return null;
        }

        tag.Name = request.Name.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return new TagResponse(tag.Id, tag.Slug, tag.Name);
    }

    public async Task<TagMergeResult> MergeAsync(Guid sourceTagId, Guid targetTagId, CancellationToken cancellationToken)
    {
        if (sourceTagId == targetTagId)
        {
            return new TagMergeResult(TagMergeOutcome.SameTag, 0, 0);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await context.Tags.AnyAsync(tag => tag.Id == sourceTagId, cancellationToken))
        {
            return new TagMergeResult(TagMergeOutcome.SourceNotFound, 0, 0);
        }

        if (!await context.Tags.AnyAsync(tag => tag.Id == targetTagId, cancellationToken))
        {
            return new TagMergeResult(TagMergeOutcome.TargetNotFound, 0, 0);
        }

        await using var databaseTransaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var transactionAssignments = await context.TransactionTags
            .Where(assignment => assignment.TagId == sourceTagId || assignment.TagId == targetTagId)
            .ToListAsync(cancellationToken);
        var movedTransactionAssignments = Repoint(
            context.TransactionTags,
            transactionAssignments,
            sourceTagId,
            targetTagId,
            assignment => assignment.TransactionId,
            transactionId => new TransactionTag { TransactionId = transactionId, TagId = targetTagId });

        var lineAssignments = await context.TransactionLineTags
            .Where(assignment => assignment.TagId == sourceTagId || assignment.TagId == targetTagId)
            .ToListAsync(cancellationToken);
        var movedLineAssignments = Repoint(
            context.TransactionLineTags,
            lineAssignments,
            sourceTagId,
            targetTagId,
            assignment => assignment.TransactionLineId,
            lineId => new TransactionLineTag { TransactionLineId = lineId, TagId = targetTagId });

        await context.SaveChangesAsync(cancellationToken);
        context.Tags.Remove(await context.Tags.SingleAsync(tag => tag.Id == sourceTagId, cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);

        return new TagMergeResult(TagMergeOutcome.Merged, movedTransactionAssignments, movedLineAssignments);
    }

    /// <summary>Moves source-tag rows onto the target tag, collapsing collisions onto whichever row is more decided.</summary>
    private static int Repoint<TAssignment>(
        DbSet<TAssignment> assignments,
        List<TAssignment> loaded,
        Guid sourceTagId,
        Guid targetTagId,
        Func<TAssignment, Guid> subjectSelector,
        Func<Guid, TAssignment> createForTarget)
        where TAssignment : class, ITagAssignment
    {
        var existingTargets = loaded
            .Where(assignment => assignment.TagId == targetTagId)
            .ToDictionary(subjectSelector);
        var moved = 0;

        foreach (var assignment in loaded.Where(assignment => assignment.TagId == sourceTagId))
        {
            var subjectId = subjectSelector(assignment);
            assignments.Remove(assignment);

            if (existingTargets.TryGetValue(subjectId, out var target))
            {
                if (Rank(assignment.State) > Rank(target.State))
                {
                    target.State = assignment.State;
                    target.Source = assignment.Source;
                    target.DecidedAt = assignment.DecidedAt;
                }

                continue;
            }

            var replacement = createForTarget(subjectId);
            replacement.State = assignment.State;
            replacement.Source = assignment.Source;
            replacement.DecidedAt = assignment.DecidedAt;
            assignments.Add(replacement);
            moved++;
        }

        return moved;
    }

    // A recorded decision outranks an unreviewed suggestion.
    private static int Rank(TagAssignmentState state) => state switch
    {
        TagAssignmentState.Confirmed => 3,
        TagAssignmentState.Rejected => 2,
        _ => 1
    };

    private static string ToSlug(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
