namespace Favourites.Application.Links.Dtos;

public sealed record PagedLinksDto(
    IReadOnlyList<FavouriteLinkDto> Items,
    int Total,
    int Page,
    int PageSize);
