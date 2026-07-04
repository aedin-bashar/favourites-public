namespace Favourites.Application.Links.RestoreManyLinks;

public sealed record RestoreManyLinksCommand(IReadOnlyList<Guid> LinkIds);
