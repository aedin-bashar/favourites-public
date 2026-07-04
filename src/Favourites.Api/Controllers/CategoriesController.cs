using Favourites.Api.Contracts.Categories;
using Favourites.Application.Categories.CreateCategory;
using Favourites.Application.Categories.DeleteCategory;
using Favourites.Application.Categories.GetCategoriesDuplicates;
using Favourites.Application.Categories.GetCategoriesSummary;
using Favourites.Application.Categories.GetUserCategories;
using Favourites.Application.Categories.MergeCategories;
using Favourites.Application.Categories.UpdateCategory;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Favourites.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromServices] GetCategoriesSummaryHandler handler,
        CancellationToken cancellationToken)
    {
        var dto = await handler.HandleAsync(new GetCategoriesSummaryQuery(), cancellationToken);

        var response = new CategoriesSummaryResponse(
            TotalCategories: dto.TotalCategories,
            EmptyCategories: dto.EmptyCategories,
            LargestCategory: dto.LargestCategory is null
                ? null
                : new CategoryLargestResponse(dto.LargestCategory.Id, dto.LargestCategory.Name, dto.LargestCategory.Count),
            RecentlyAdded: dto.RecentlyAdded is null
                ? null
                : new CategorySummaryItemResponse(dto.RecentlyAdded.Id, dto.RecentlyAdded.Name),
            UncategorizedLinks: dto.UncategorizedLinks);

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromServices] GetUserCategoriesHandler handler = null!,
        CancellationToken cancellationToken = default)
    {
        var categories = await handler.HandleAsync(new GetUserCategoriesQuery(), cancellationToken);

        IEnumerable<Favourites.Application.Categories.Dtos.CategoryDto> filtered = categories;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            filtered = filtered.Where(category =>
                category.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        filtered = status?.Trim().ToLowerInvariant() switch
        {
            "used" => filtered.Where(category => category.LinkCount > 0),
            "empty" => filtered.Where(category => category.LinkCount == 0),
            _ => filtered,
        };

        filtered = sort?.Trim().ToLowerInvariant() switch
        {
            "largest" => filtered.OrderByDescending(category => category.LinkCount).ThenBy(category => category.Name),
            "newest" => filtered.OrderByDescending(category => category.CreatedAtUtc).ThenBy(category => category.Name),
            "recently-active" or "activity" => filtered
                .OrderByDescending(category => category.LastActivityAtUtc ?? category.CreatedAtUtc)
                .ThenBy(category => category.Name),
            _ => filtered.OrderBy(category => category.Name),
        };

        var clampedPageSize = Math.Clamp(pageSize, 1, 100);
        var clampedPage = Math.Max(1, page);

        var materialized = filtered.ToList();
        var total = materialized.Count;
        var items = materialized
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .Select(ApiResponseMapping.ToCategoryResponse)
            .ToList();

        return Ok(new PagedCategoriesResponse(items, total, clampedPage, clampedPageSize));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryRequest request,
        [FromServices] IValidator<CreateCategoryCommand> validator,
        [FromServices] CreateCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateCategoryCommand(request.Name);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                ApiResponseMapping.ToFluentValidationErrors(validationResult)));
        }

        var dto = await handler.HandleAsync(command, cancellationToken);
        var response = ApiResponseMapping.ToCategoryResponse(dto);

        return Created($"/api/categories/{dto.Id}", response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        [FromServices] IValidator<UpdateCategoryCommand> validator,
        [FromServices] UpdateCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCategoryCommand(id, request.Name, request.Color);

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

        return Ok(ApiResponseMapping.ToCategoryResponse(dto));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] DeleteCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var deleted = await handler.HandleAsync(new DeleteCategoryCommand(id), cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    // Return groups of categories with similar names (case-insensitive equal or Levenshtein ≤ 2).
    [HttpGet("duplicates")]
    public async Task<IActionResult> Duplicates(
        [FromServices] GetCategoriesDuplicatesHandler handler,
        CancellationToken cancellationToken)
    {
        var groups = await handler.HandleAsync(new GetCategoriesDuplicatesQuery(), cancellationToken);

        var response = groups
            .Select(g => new CategoryDuplicateGroupResponse(
                g.Categories.Select(ApiResponseMapping.ToCategoryResponse).ToList()))
            .ToList();

        return Ok(response);
    }

    // Merge one or more categories into a single target category.
    [HttpPost("merge")]
    public async Task<IActionResult> Merge(
        [FromBody] MergeCategoriesRequest request,
        [FromServices] MergeCategoriesHandler handler,
        CancellationToken cancellationToken)
    {
        if (request.KeepCategoryId == Guid.Empty)
            return BadRequest(new { error = "KeepCategoryId is required." });

        if (request.MergeCategoryIds is null || request.MergeCategoryIds.Count == 0)
            return BadRequest(new { error = "At least one category ID to merge is required." });

        try
        {
            var merged = await handler.HandleAsync(
                new MergeCategoriesCommand(request.KeepCategoryId, request.MergeCategoryIds),
                cancellationToken);

            return Ok(new { merged });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
