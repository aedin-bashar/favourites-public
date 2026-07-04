namespace Favourites.Application.Tags.UpdateTag;

public sealed record UpdateTagCommand(
    Guid Id,
    string Name);
