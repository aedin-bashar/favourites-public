namespace Favourites.Domain.Entities;

public sealed class FavouriteLinkTag
{
    private FavouriteLinkTag()
    {
    }

    private FavouriteLinkTag(Guid favouriteLinkId, Guid tagId)
    {
        FavouriteLinkId = favouriteLinkId;
        TagId = tagId;
    }

    public Guid FavouriteLinkId { get; private set; }

    public Guid TagId { get; private set; }

    public static FavouriteLinkTag Create(Guid favouriteLinkId, Guid tagId)
    {
        if (favouriteLinkId == Guid.Empty)
        {
            throw new ArgumentException(
                "FavouriteLinkId is required.",
                nameof(favouriteLinkId));
        }

        if (tagId == Guid.Empty)
        {
            throw new ArgumentException("TagId is required.", nameof(tagId));
        }

        return new FavouriteLinkTag(favouriteLinkId, tagId);
    }
}
