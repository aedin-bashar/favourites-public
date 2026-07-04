using Favourites.Api.Contracts.Links;
using Favourites.Application.Links.ArchiveFavouriteLink;
using Favourites.Application.Links.CreateLink;
using Favourites.Application.Links.DeleteArchivedLinks;
using Favourites.Application.Links.DeleteFavouriteLink;
using Favourites.Application.Links.GetCleanupSuggestions;
using Favourites.Application.Links.GetFavouriteLinkById;
using Favourites.Application.Links.GetUserLinks;
using Favourites.Application.Links.ImportLinks;
using Favourites.Application.Links.RestoreArchivedLink;
using Favourites.Application.Links.RestoreManyLinks;
using Favourites.Application.Links.UpdateFavouriteLink;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Favourites.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/links")]
public sealed class LinksController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLinkRequest request,
        [FromServices] IValidator<CreateFavouriteLinkCommand> validator,
        [FromServices] CreateFavouriteLinkHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateFavouriteLinkCommand(
            Url: request.Url,
            Title: request.Title,
            Description: request.Description,
            TagIds: request.TagIds,
            CategoryId: request.CategoryId);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                ApiResponseMapping.ToFluentValidationErrors(validationResult)));
        }

        var dto = await handler.HandleAsync(command, cancellationToken);
        var response = ApiResponseMapping.ToLinkResponse(dto);

        return Created($"/api/links/{dto.Id}", response);
    }

    // Accepts optional `page` (default 1) and `pageSize` (default 25, max 100).
    // When both are omitted the response wraps the full result set with page=1, pageSize=total
    // so existing clients that read `items` still work without changes.
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] GetUserLinksHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] Guid? tagId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? archived = null,
        [FromQuery] DateTimeOffset? archivedFrom = null,
        [FromQuery] DateTimeOffset? archivedTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var sortOrder = ParseSort(sort);
        var archivedFilter = ParseArchivedFilter(archived);

        var clampedPageSize = Math.Clamp(pageSize, 1, 100);
        var clampedPage = Math.Max(1, page);

        var paged = await handler.HandleAsync(
            new GetUserLinksQuery(
                Search: search,
                TagId: tagId,
                CategoryId: categoryId,
                Sort: sortOrder,
                Archived: archivedFilter,
                ArchivedFrom: archivedFrom,
                ArchivedTo: archivedTo,
                Page: clampedPage,
                PageSize: clampedPageSize),
            cancellationToken);

        var items = paged.Items.Select(ApiResponseMapping.ToLinkResponse).ToList();

        return Ok(new PagedLinksResponse(
            Items: items,
            Total: paged.Total,
            Page: paged.Page,
            PageSize: paged.PageSize));
    }

    private static LinkSortOrder ParseSort(string? sort) => sort?.Trim().ToLowerInvariant() switch
    {
        "oldest" => LinkSortOrder.Oldest,
        "title" => LinkSortOrder.Title,
        "recently-updated" or "recentlyupdated" or "updated" => LinkSortOrder.RecentlyUpdated,
        "oldest-archived" or "oldestarchived" => LinkSortOrder.OldestArchived,
        _ => LinkSortOrder.Newest,
    };

    private static ArchivedFilter ParseArchivedFilter(string? archived) =>
        archived?.Trim().ToLowerInvariant() switch
        {
            "archived" => ArchivedFilter.Archived,
            "all" => ArchivedFilter.All,
            _ => ArchivedFilter.Active,
        };

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] GetFavouriteLinkByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var dto = await handler.HandleAsync(new GetFavouriteLinkByIdQuery(id), cancellationToken);

        if (dto is null)
        {
            return NotFound();
        }

        return Ok(ApiResponseMapping.ToLinkResponse(dto));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateLinkRequest request,
        [FromServices] IValidator<UpdateFavouriteLinkCommand> validator,
        [FromServices] UpdateFavouriteLinkHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateFavouriteLinkCommand(
            Id: id,
            Url: request.Url,
            Title: request.Title,
            Description: request.Description,
            TagIds: request.TagIds,
            CategoryId: request.CategoryId);

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

        return Ok(ApiResponseMapping.ToLinkResponse(dto));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] DeleteFavouriteLinkHandler handler,
        CancellationToken cancellationToken)
    {
        var deleted = await handler.HandleAsync(new DeleteFavouriteLinkCommand(id), cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(
        Guid id,
        [FromServices] ArchiveFavouriteLinkHandler handler,
        CancellationToken cancellationToken)
    {
        var archived = await handler.HandleAsync(new ArchiveFavouriteLinkCommand(id), cancellationToken);

        return archived ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(
        Guid id,
        [FromServices] RestoreArchivedLinkHandler handler,
        CancellationToken cancellationToken)
    {
        var restored = await handler.HandleAsync(new RestoreArchivedLinkCommand(id), cancellationToken);

        return restored ? NoContent() : NotFound();
    }

    // Restore a batch of archived links by their IDs.
    [HttpPost("restore-many")]
    public async Task<IActionResult> RestoreMany(
        [FromBody] RestoreManyLinksRequest request,
        [FromServices] RestoreManyLinksHandler handler,
        CancellationToken cancellationToken)
    {
        if (request.LinkIds is null || request.LinkIds.Count == 0)
        {
            return BadRequest(new { error = "At least one link ID is required." });
        }

        var count = await handler.HandleAsync(
            new RestoreManyLinksCommand(request.LinkIds),
            cancellationToken);

        return Ok(new { restored = count });
    }

    // Delete all archived links for the current user (empty archive).
    [HttpDelete("archived")]
    public async Task<IActionResult> DeleteArchived(
        [FromServices] DeleteArchivedLinksHandler handler,
        CancellationToken cancellationToken)
    {
        var count = await handler.HandleAsync(new DeleteArchivedLinksCommand(), cancellationToken);

        return Ok(new { deleted = count });
    }

    // Import bookmarks from a Netscape HTML bookmark file or a
    // Favourites JSON export (multipart upload; file type decides the parser).
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB cap
    public async Task<IActionResult> Import(
        IFormFile file,
        [FromServices] ImportLinksHandler htmlHandler,
        [FromServices] ImportJsonLinksHandler jsonHandler,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A non-empty HTML or JSON file is required." });

        var isHtml = file.ContentType.Contains("html", StringComparison.OrdinalIgnoreCase)
            || file.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            || file.FileName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);

        var isJson = !isHtml
            && (file.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                || file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        if (!isHtml && !isJson)
            return BadRequest(new { error = "Only HTML bookmark files and Favourites JSON exports are supported." });

        string content;
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { error = "The uploaded file is empty." });

        ImportLinksResult result;
        if (isJson)
        {
            try
            {
                result = await jsonHandler.HandleAsync(new ImportJsonLinksCommand(content), cancellationToken);
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest(new { error = "The uploaded file is not a valid Favourites JSON export." });
            }
        }
        else
        {
            result = await htmlHandler.HandleAsync(new ImportLinksCommand(content), cancellationToken);
        }

        return Ok(new ImportLinksResponse(result.Created, result.Skipped));
    }

    // Export the authenticated user's links in JSON or Netscape HTML format.
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromServices] GetUserLinksHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] string format = "json")
    {
        var paged = await handler.HandleAsync(
            new GetUserLinksQuery(
                Search: null,
                TagId: null,
                CategoryId: null,
                Sort: LinkSortOrder.Newest,
                Archived: ArchivedFilter.All,
                ArchivedFrom: null,
                ArchivedTo: null,
                Page: 1,
                PageSize: int.MaxValue),
            cancellationToken);

        var items = paged.Items.Select(ApiResponseMapping.ToLinkResponse).ToList();

        // An empty library has nothing to export — refuse instead of producing
        // an empty backup file the user might mistake for a real one.
        if (items.Count == 0)
            return BadRequest(new { error = "There are no links to export. Save at least one link first." });

        if (format.Equals("html", StringComparison.OrdinalIgnoreCase))
        {
            var html = BuildNetscapeHtml(items);
            return File(
                System.Text.Encoding.UTF8.GetBytes(html),
                "text/html",
                "favourites-bookmarks.html");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(items, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });

        return File(
            System.Text.Encoding.UTF8.GetBytes(json),
            "application/json",
            "favourites-export.json");
    }

    // Return archived links older than 90 days, oldest-first.
    [HttpGet("cleanup-suggestions")]
    public async Task<IActionResult> CleanupSuggestions(
        [FromServices] GetCleanupSuggestionsHandler handler,
        CancellationToken cancellationToken)
    {
        var items = await handler.HandleAsync(new GetCleanupSuggestionsQuery(), cancellationToken);
        return Ok(items.Select(ApiResponseMapping.ToLinkResponse).ToList());
    }

    // Netscape bookmark file format — the de-facto import format for
    // Chrome/Firefox/Safari/Edge. Categories become folders; uncategorized
    // links sit at the root. Round-trips through our own HTML import, where
    // folder names become tags.
    private static string BuildNetscapeHtml(IReadOnlyList<LinkResponse> links)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        sb.AppendLine("<!-- This is an automatically generated file. It will be read and overwritten. DO NOT EDIT! -->");
        // Marker that lets our own HTML import restore folders as categories
        // and TAGS attributes as tags. Browsers ignore comments.
        sb.AppendLine($"<!-- {ImportLinksHandler.FavouritesExportMarker} -->");
        sb.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        sb.AppendLine("<TITLE>Bookmarks</TITLE>");
        sb.AppendLine("<H1>Bookmarks</H1>");
        sb.AppendLine("<DL><p>");

        foreach (var link in links.Where(l => l.Category is null))
            AppendBookmark(sb, link, indent: "    ");

        foreach (var group in links
            .Where(l => l.Category is not null)
            .GroupBy(l => l.Category!.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"    <DT><H3>{HtmlEncode(group.Key)}</H3>");
            sb.AppendLine("    <DL><p>");
            foreach (var link in group)
                AppendBookmark(sb, link, indent: "        ");
            sb.AppendLine("    </DL><p>");
        }

        sb.AppendLine("</DL><p>");
        return sb.ToString();
    }

    private static void AppendBookmark(System.Text.StringBuilder sb, LinkResponse link, string indent)
    {
        var addDate = link.CreatedAtUtc.ToUnixTimeSeconds();
        var tagsAttr = link.Tags.Count > 0
            ? $" TAGS=\"{HtmlEncode(string.Join(",", link.Tags.Select(t => t.Name)))}\""
            : string.Empty;
        sb.AppendLine($"{indent}<DT><A HREF=\"{HtmlEncode(link.Url)}\" ADD_DATE=\"{addDate}\"{tagsAttr}>{HtmlEncode(link.Title)}</A>");
    }

    private static string HtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);
}
