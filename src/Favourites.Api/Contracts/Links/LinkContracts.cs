using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.Tags;

namespace Favourites.Api.Contracts.Links;

public sealed record CreateLinkRequest(
    string Url,
    string Title,
    string? Description,
    IReadOnlyCollection<Guid>? TagIds = null,
    Guid? CategoryId = null);

public sealed record UpdateLinkRequest(
    string Url,
    string Title,
    string? Description,
    IReadOnlyCollection<Guid>? TagIds = null,
    Guid? CategoryId = null);

public sealed record LinkResponse(
    Guid Id,
    string Url,
    string Title,
    string? Description,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<TagResponse> Tags,
    CategoryResponse? Category);

public sealed record PagedLinksResponse(
    IReadOnlyList<LinkResponse> Items,
    int Total,
    int Page,
    int PageSize);
