using Favourites.Api.Contracts.Tags;
using Favourites.Application.Tags.CreateTag;
using Favourites.Application.Tags.DeleteTag;
using Favourites.Application.Tags.GetTagsDuplicates;
using Favourites.Application.Tags.GetTagsSummary;
using Favourites.Application.Tags.GetUserTags;
using Favourites.Application.Tags.MergeTags;
using Favourites.Application.Tags.UpdateTag;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Favourites.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tags")]
public sealed class TagsController : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromServices] GetTagsSummaryHandler handler,
        CancellationToken cancellationToken)
    {
        var dto = await handler.HandleAsync(new GetTagsSummaryQuery(), cancellationToken);

        var response = new TagsSummaryResponse(
            TotalTags: dto.TotalTags,
            UnusedTags: dto.UnusedTags,
            MostUsed: dto.MostUsed is null
                ? null
                : new TagMostUsedResponse(dto.MostUsed.Id, dto.MostUsed.Name, dto.MostUsed.Count),
            RecentlyAdded: dto.RecentlyAdded is null
                ? null
                : new TagSummaryItemResponse(dto.RecentlyAdded.Id, dto.RecentlyAdded.Name),
            PossibleDuplicates: dto.PossibleDuplicates);

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromServices] GetUserTagsHandler handler = null!,
        CancellationToken cancellationToken = default)
    {
        var tags = await handler.HandleAsync(new GetUserTagsQuery(), cancellationToken);

        IEnumerable<Favourites.Application.Tags.Dtos.TagDto> filtered = tags;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            filtered = filtered.Where(tag =>
                tag.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        filtered = status?.Trim().ToLowerInvariant() switch
        {
            "used" => filtered.Where(tag => tag.LinkCount > 0),
            "unused" => filtered.Where(tag => tag.LinkCount == 0),
            _ => filtered,
        };

        filtered = sort?.Trim().ToLowerInvariant() switch
        {
            "most-used" => filtered.OrderByDescending(tag => tag.LinkCount).ThenBy(tag => tag.Name),
            "least-used" => filtered.OrderBy(tag => tag.LinkCount).ThenBy(tag => tag.Name),
            "newest" => filtered.OrderByDescending(tag => tag.CreatedAtUtc).ThenBy(tag => tag.Name),
            _ => filtered.OrderBy(tag => tag.Name),
        };

        var clampedPageSize = Math.Clamp(pageSize, 1, 100);
        var clampedPage = Math.Max(1, page);

        var materialized = filtered.ToList();
        var total = materialized.Count;
        var items = materialized
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .Select(ApiResponseMapping.ToTagResponse)
            .ToList();

        return Ok(new PagedTagsResponse(items, total, clampedPage, clampedPageSize));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTagRequest request,
        [FromServices] IValidator<CreateTagCommand> validator,
        [FromServices] CreateTagHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateTagCommand(request.Name);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                ApiResponseMapping.ToFluentValidationErrors(validationResult)));
        }

        var dto = await handler.HandleAsync(command, cancellationToken);
        var response = ApiResponseMapping.ToTagResponse(dto);

        return Created($"/api/tags/{dto.Id}", response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTagRequest request,
        [FromServices] IValidator<UpdateTagCommand> validator,
        [FromServices] UpdateTagHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTagCommand(id, request.Name);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                ApiResponseMapping.ToFluentValidationErrors(validationResult)));
        }

        var dto = await handler.HandleAsync(command, cancellationToken);

        if (dto is null)
        {
            return NotFound();
        }

        return Ok(ApiResponseMapping.ToTagResponse(dto));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] DeleteTagHandler handler,
        CancellationToken cancellationToken)
    {
        var deleted = await handler.HandleAsync(new DeleteTagCommand(id), cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    // Return groups of tags with similar names (case-insensitive equal or Levenshtein ≤ 2).
    [HttpGet("duplicates")]
    public async Task<IActionResult> Duplicates(
        [FromServices] GetTagsDuplicatesHandler handler,
        CancellationToken cancellationToken)
    {
        var groups = await handler.HandleAsync(new GetTagsDuplicatesQuery(), cancellationToken);

        var response = groups
            .Select(g => new TagDuplicateGroupResponse(
                g.Tags.Select(ApiResponseMapping.ToTagResponse).ToList()))
            .ToList();

        return Ok(response);
    }

    // Merge one or more tags into a single target tag.
    [HttpPost("merge")]
    public async Task<IActionResult> Merge(
        [FromBody] MergeTagsRequest request,
        [FromServices] MergeTagsHandler handler,
        CancellationToken cancellationToken)
    {
        if (request.KeepTagId == Guid.Empty)
            return BadRequest(new { error = "KeepTagId is required." });

        if (request.MergeTagIds is null || request.MergeTagIds.Count == 0)
            return BadRequest(new { error = "At least one tag ID to merge is required." });

        try
        {
            var merged = await handler.HandleAsync(
                new MergeTagsCommand(request.KeepTagId, request.MergeTagIds),
                cancellationToken);

            return Ok(new { merged });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
