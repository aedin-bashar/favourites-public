namespace Favourites.Application.Links.ImportLinks;

/// <summary>
/// Command to import links from a Favourites JSON export file
/// (the array produced by GET /api/links/export?format=json).
/// </summary>
public sealed record ImportJsonLinksCommand(string JsonContent);
