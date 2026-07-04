using Favourites.Api.Contracts.Dashboard;
using Favourites.Application.Dashboard.GetDashboardSummary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Favourites.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromServices] GetDashboardSummaryHandler handler,
        CancellationToken cancellationToken)
    {
        var dto = await handler.HandleAsync(new GetDashboardSummaryQuery(), cancellationToken);

        var response = new DashboardSummaryResponse(
            TotalLinks: dto.TotalLinks,
            TotalTags: dto.TotalTags,
            TotalCategories: dto.TotalCategories,
            TotalArchived: dto.TotalArchived,
            ThisWeek: new DashboardThisWeekResponse(
                LinksAdded: dto.ThisWeek.LinksAdded,
                CategoriesCreated: dto.ThisWeek.CategoriesCreated,
                TagsCreated: dto.ThisWeek.TagsCreated,
                LinksArchived: dto.ThisWeek.LinksArchived));

        return Ok(response);
    }
}
