using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Models.Tags;

public sealed class TagRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}

public sealed class TagMergeRequest
{
    [Required]
    public Guid? TargetTagId { get; set; }
}

public sealed record TagResponse(Guid Id, string Slug, string Name);

public enum TagCreateOutcome
{
    Created,
    UnusableName,
    SlugConflict
}

public sealed record TagCreateResult(TagCreateOutcome Outcome, TagResponse? Tag);

public enum TagMergeOutcome
{
    Merged,
    SourceNotFound,
    TargetNotFound,
    SameTag
}

public sealed record TagMergeResult(
    TagMergeOutcome Outcome,
    int MovedTransactionAssignments,
    int MovedLineAssignments);
