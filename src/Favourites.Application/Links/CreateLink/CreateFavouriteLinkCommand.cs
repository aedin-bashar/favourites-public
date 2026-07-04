namespace Favourites.Application.Links.CreateLink;

public sealed record CreateFavouriteLinkCommand(
    string Url,
    string Title,
    string? Description,
    IReadOnlyCollection<Guid>? TagIds = null,
    Guid? CategoryId = null);
