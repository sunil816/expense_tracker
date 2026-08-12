using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.Data;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Tags;
using ExpenseTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class TagAssignmentTests
{
    [Fact]
    public async Task RejectedAssignmentSurvivesAndBlocksDeletionOfTagStillInUse()
    {
        await using var database = await TestDatabase.CreateAsync();
        var tagId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        await using (var context = await database.CreateContextAsync())
        {
            context.Tags.Add(new Tag { Id = tagId, Slug = "shopping", Name = "Shopping" });
            context.Transactions.Add(CreateManualTransaction(transactionId, "UPI/AMAZON PAY/9982211", 680m));
            context.TransactionTags.Add(new TransactionTag
            {
                TransactionId = transactionId,
                TagId = tagId,
                State = TagAssignmentState.Suggested,
                Source = TagAssignmentSource.Rule,
                DecidedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await using (var context = await database.CreateContextAsync())
        {
            var assignment = await context.TransactionTags.SingleAsync();
            assignment.State = TagAssignmentState.Rejected;
            await context.SaveChangesAsync();
        }

        await using (var context = await database.CreateContextAsync())
        {
            context.Tags.Remove(await context.Tags.SingleAsync());
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using (var context = await database.CreateContextAsync())
        {
            Assert.Equal(TagAssignmentState.Rejected, (await context.TransactionTags.SingleAsync()).State);
            Assert.Equal(1, await context.Tags.CountAsync());
        }
    }

    [Fact]
    public async Task DuplicateAssignmentIsRejectedAndTransactionDeleteCascades()
    {
        await using var database = await TestDatabase.CreateAsync();
        var tagId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        await using (var context = await database.CreateContextAsync())
        {
            context.Tags.Add(new Tag { Id = tagId, Slug = "coffee", Name = "Coffee" });
            context.Transactions.Add(CreateManualTransaction(transactionId, "UPI/BLUETOKAI COFFEE/1123", 40m));
            context.TransactionTags.Add(CreateAssignment(transactionId, tagId, TagAssignmentState.Confirmed));
            await context.SaveChangesAsync();
        }

        await using (var context = await database.CreateContextAsync())
        {
            context.TransactionTags.Add(CreateAssignment(transactionId, tagId, TagAssignmentState.Suggested));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using (var context = await database.CreateContextAsync())
        {
            context.Transactions.Remove(await context.Transactions.SingleAsync());
            await context.SaveChangesAsync();
            Assert.Empty(await context.TransactionTags.ToListAsync());
            Assert.Equal(1, await context.Tags.CountAsync());
        }
    }

    [Fact]
    public async Task MergeRepointsAssignmentsAndKeepsTheMoreDecidedStateOnCollision()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sourceTagId = Guid.NewGuid();
        var targetTagId = Guid.NewGuid();
        var collidingTransactionId = Guid.NewGuid();
        var movedTransactionId = Guid.NewGuid();

        await using (var context = await database.CreateContextAsync())
        {
            context.Tags.Add(new Tag { Id = sourceTagId, Slug = "cofee", Name = "Cofee" });
            context.Tags.Add(new Tag { Id = targetTagId, Slug = "coffee", Name = "Coffee" });
            context.Transactions.Add(CreateManualTransaction(collidingTransactionId, "BLUETOKAI", 40m));
            context.Transactions.Add(CreateManualTransaction(movedTransactionId, "THIRD WAVE", 220m));
            context.TransactionTags.Add(CreateAssignment(collidingTransactionId, sourceTagId, TagAssignmentState.Confirmed));
            context.TransactionTags.Add(CreateAssignment(collidingTransactionId, targetTagId, TagAssignmentState.Suggested));
            context.TransactionTags.Add(CreateAssignment(movedTransactionId, sourceTagId, TagAssignmentState.Rejected));
            await context.SaveChangesAsync();
        }

        var result = await database.Services.GetRequiredService<TagService>()
            .MergeAsync(sourceTagId, targetTagId, CancellationToken.None);

        Assert.Equal(TagMergeOutcome.Merged, result.Outcome);
        Assert.Equal(1, result.MovedTransactionAssignments);

        await using (var context = await database.CreateContextAsync())
        {
            var assignments = await context.TransactionTags.ToListAsync();
            Assert.All(assignments, assignment => Assert.Equal(targetTagId, assignment.TagId));
            Assert.Equal(
                TagAssignmentState.Confirmed,
                assignments.Single(assignment => assignment.TransactionId == collidingTransactionId).State);
            Assert.Equal(
                TagAssignmentState.Rejected,
                assignments.Single(assignment => assignment.TransactionId == movedTransactionId).State);
            Assert.Equal(targetTagId, (await context.Tags.SingleAsync()).Id);
        }
    }

    [Fact]
    public async Task CreateDerivesSlugAndRefusesCollidingOrUnusableNames()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<TagService>();

        var created = await service.CreateAsync(new TagRequest { Name = "  Cold Brew Coffee " }, CancellationToken.None);
        var colliding = await service.CreateAsync(new TagRequest { Name = "cold brew (coffee)!" }, CancellationToken.None);
        var unusable = await service.CreateAsync(new TagRequest { Name = "!!!" }, CancellationToken.None);

        Assert.Equal(TagCreateOutcome.Created, created.Outcome);
        Assert.Equal("cold-brew-coffee", created.Tag!.Slug);
        Assert.Equal("Cold Brew Coffee", created.Tag.Name);
        Assert.Equal(TagCreateOutcome.SlugConflict, colliding.Outcome);
        Assert.Equal(TagCreateOutcome.UnusableName, unusable.Outcome);
        Assert.Single(await service.ListAsync(CancellationToken.None));
    }

    private static ExpenseTransaction CreateManualTransaction(Guid id, string description, decimal amount) =>
        new()
        {
            Id = id,
            Origin = TransactionOrigin.Manual,
            TransactionDate = new DateOnly(2026, 8, 9),
            Description = description,
            Direction = TransactionDirection.Debit,
            Amount = amount,
            LineExtractionStatus = LineExtractionStatus.NotApplicable
        };

    private static TransactionTag CreateAssignment(Guid transactionId, Guid tagId, TagAssignmentState state) =>
        new()
        {
            TransactionId = transactionId,
            TagId = tagId,
            State = state,
            Source = TagAssignmentSource.Manual,
            DecidedAt = DateTimeOffset.UtcNow
        };
}
