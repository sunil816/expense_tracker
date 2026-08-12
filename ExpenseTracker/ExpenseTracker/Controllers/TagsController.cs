using ExpenseTracker.Models.Tags;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers;

[ApiController]
[Route("api/tags")]
public sealed class TagsController(TagService tagService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken) =>
        Ok(await tagService.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] TagRequest request, CancellationToken cancellationToken)
    {
        if (RequireHttps() is { } insecure)
        {
            return insecure;
        }

        var result = await tagService.CreateAsync(request, cancellationToken);
        return result.Outcome switch
        {
            TagCreateOutcome.Created => StatusCode(StatusCodes.Status201Created, result.Tag),
            TagCreateOutcome.UnusableName => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The tag name is unusable.",
                detail: "name must contain at least one letter or digit."),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The tag already exists.",
                detail: "A tag with the same slug is already defined.")
        };
    }

    [HttpPut("{tagId:guid}")]
    public async Task<IActionResult> RenameAsync(Guid tagId, [FromBody] TagRequest request, CancellationToken cancellationToken)
    {
        if (RequireHttps() is { } insecure)
        {
            return insecure;
        }

        var renamed = await tagService.RenameAsync(tagId, request, cancellationToken);
        return renamed is null ? TagNotFound() : Ok(renamed);
    }

    [HttpPost("{tagId:guid}/merge")]
    public async Task<IActionResult> MergeAsync(Guid tagId, [FromBody] TagMergeRequest request, CancellationToken cancellationToken)
    {
        if (RequireHttps() is { } insecure)
        {
            return insecure;
        }

        var result = await tagService.MergeAsync(tagId, request.TargetTagId!.Value, cancellationToken);
        return result.Outcome switch
        {
            TagMergeOutcome.Merged => Ok(result),
            TagMergeOutcome.SameTag => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A tag cannot be merged into itself.",
                detail: "targetTagId must differ from the tag being merged."),
            _ => TagNotFound()
        };
    }

    private IActionResult TagNotFound() =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "The tag does not exist.",
            detail: "The requested tag could not be found.");

    private IActionResult? RequireHttps() =>
        Request.IsHttps
            ? null
            : Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "HTTPS is required.",
                detail: "This endpoint only accepts encrypted transport.");
}
