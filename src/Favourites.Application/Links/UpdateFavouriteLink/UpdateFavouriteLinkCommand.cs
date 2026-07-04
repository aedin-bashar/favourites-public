namespace Favourites.Application.Links.UpdateFavouriteLink;

public sealed record UpdateFavouriteLinkCommand(
    Guid Id,
    string Url,
    string Title,
    string? Description,
    IReadOnlyCollection<Guid>? TagIds = null,
    Guid? CategoryId = null);
