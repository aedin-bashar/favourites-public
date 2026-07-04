namespace Favourites.Application.Links.ImportLinks;

/// <summary>
/// Command to import links from a Netscape HTML bookmark file.
/// </summary>
public sealed record ImportLinksCommand(string HtmlContent);
