using Favourites.Api.Contracts.Links;
using Favourites.Application.Links.GetArchivedSummary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Favourites.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/archived")]
public sealed class ArchivedController : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromServices] GetArchivedSummaryHandler handler,
        CancellationToken cancellationToken)
    {
        var dto = await handler.HandleAsync(new GetArchivedSummaryQuery(), cancellationToken);

        var response = new ArchivedSummaryResponse(
            ArchivedLinks: dto.ArchivedLinks,
            ArchivedThisMonth: dto.ArchivedThisMonth,
            OldestArchived: dto.OldestArchived is null
                ? null
                : ApiResponseMapping.ToLinkResponse(dto.OldestArchived),
            RestoredRecently: dto.RestoredRecently,
            CleanupSuggestions: dto.CleanupSuggestions
                .Select(ApiResponseMapping.ToLinkResponse)
                .ToList());

        return Ok(response);
    }
}
