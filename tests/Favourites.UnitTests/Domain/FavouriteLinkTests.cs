using Favourites.Domain.Entities;

namespace Favourites.UnitTests.Domain;

public sealed class FavouriteLinkTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Task 2.19 — creating a valid favourite link
    [Fact]
    public void Create_WithValidArguments_ReturnsFavouriteLink()
    {
        var createdAt = new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);

        var link = FavouriteLink.Create(
            userId: UserId,
            url: "https://example.com/page",
            title: "Example",
            description: "An example link",
            createdAtUtc: createdAt);

        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.Equal(UserId, link.UserId);
        Assert.Equal("https://example.com/page", link.Url);
        Assert.Equal("Example", link.Title);
        Assert.Equal("An example link", link.Description);
        Assert.False(link.IsArchived);
        Assert.Equal(createdAt, link.CreatedAtUtc);
        Assert.Null(link.UpdatedAtUtc);
    }

    [Fact]
    public void Create_WithNullDescription_StoresNull()
    {
        var link = FavouriteLink.Create(UserId, "https://example.com", "Example", description: null);

        Assert.Null(link.Description);
    }

    [Fact]
    public void Create_WithWhitespaceDescription_StoresNull()
    {
        var link = FavouriteLink.Create(UserId, "https://example.com", "Example", description: "   ");

        Assert.Null(link.Description);
    }

    [Fact]
    public void Create_TrimsTitleAndDescription()
    {
        var link = FavouriteLink.Create(UserId, "https://example.com", "  Trimmed  ", "  Notes  ");

        Assert.Equal("Trimmed", link.Title);
        Assert.Equal("Notes", link.Description);
    }

    [Fact]
    public void Create_WithoutCreatedAt_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var link = FavouriteLink.Create(UserId, "https://example.com", "Example", description: null);

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(link.CreatedAtUtc, before, after);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var first = FavouriteLink.Create(UserId, "https://example.com/a", "A", null);
        var second = FavouriteLink.Create(UserId, "https://example.com/b", "B", null);

        Assert.NotEqual(first.Id, second.Id);
    }

    // Task 2.20 — rejecting invalid URLs
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingUrl_Throws(string? url)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => FavouriteLink.Create(UserId, url!, "Title", null));

        Assert.Equal("url", ex.ParamName);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("example.com")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("mailto:user@example.com")]
    public void Create_WithNonHttpUrl_Throws(string url)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => FavouriteLink.Create(UserId, url, "Title", null));

        Assert.Equal("url", ex.ParamName);
    }

    [Fact]
    public void Create_WithUrlExceedingMaxLength_Throws()
    {
        var longUrl = "https://example.com/" + new string('a', FavouriteLink.MaxUrlLength);

        var ex = Assert.Throws<ArgumentException>(
            () => FavouriteLink.Create(UserId, longUrl, "Title", null));

        Assert.Equal("url", ex.ParamName);
    }

    [Fact]
    public void Create_WithHttpUrl_IsAccepted()
    {
        var link = FavouriteLink.Create(UserId, "http://example.com/", "Example", null);

        Assert.StartsWith("http://", link.Url);
    }

    [Fact]
    public void Create_TrimsUrlBeforeValidating()
    {
        var link = FavouriteLink.Create(UserId, "  https://example.com/  ", "Example", null);

        Assert.Equal("https://example.com/", link.Url);
    }

    [Fact]
    public void Create_WithMissingTitle_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => FavouriteLink.Create(UserId, "https://example.com", "   ", null));

        Assert.Equal("title", ex.ParamName);
    }

    [Fact]
    public void Create_WithTitleExceedingMaxLength_Throws()
    {
        var longTitle = new string('t', FavouriteLink.MaxTitleLength + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => FavouriteLink.Create(UserId, "https://example.com", longTitle, null));

        Assert.Equal("title", ex.ParamName);
    }

    [Fact]
    public void Create_WithDescriptionExceedingMaxLength_Throws()
    {
        var longDescription = new string('d', FavouriteLink.MaxDescriptionLength + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => FavouriteLink.Create(UserId, "https://example.com", "Title", longDescription));

        Assert.Equal("description", ex.ParamName);
    }

    // Task 2.23 — user ownership rules
    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => FavouriteLink.Create(Guid.Empty, "https://example.com", "Title", null));

        Assert.Equal("userId", ex.ParamName);
    }

    [Fact]
    public void Create_AssignsOwnerExactlyAsProvided()
    {
        var owner = Guid.NewGuid();

        var link = FavouriteLink.Create(owner, "https://example.com", "Title", null);

        Assert.Equal(owner, link.UserId);
    }

    [Fact]
    public void Create_WithDifferentOwners_ProducesIndependentLinks()
    {
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        var linkA = FavouriteLink.Create(ownerA, "https://example.com/a", "A", null);
        var linkB = FavouriteLink.Create(ownerB, "https://example.com/b", "B", null);

        Assert.NotEqual(linkA.UserId, linkB.UserId);
        Assert.Equal(ownerA, linkA.UserId);
        Assert.Equal(ownerB, linkB.UserId);
    }

    [Fact]
    public void ClearCategory_WithAssignedCategory_RemovesCategoryAndUpdatesTimestamp()
    {
        var categoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var updatedAt = new DateTimeOffset(2026, 5, 19, 10, 0, 0, TimeSpan.Zero);
        var link = FavouriteLink.Create(
            UserId,
            "https://example.com/category",
            "Categorised",
            null,
            categoryId);

        link.ClearCategory(categoryId, updatedAt);

        Assert.Null(link.CategoryId);
        Assert.Equal(updatedAt, link.UpdatedAtUtc);
    }

    [Fact]
    public void ClearCategory_WithDifferentCategory_LeavesLinkUnchanged()
    {
        var categoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var otherCategoryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var link = FavouriteLink.Create(
            UserId,
            "https://example.com/category",
            "Categorised",
            null,
            categoryId);

        link.ClearCategory(otherCategoryId);

        Assert.Equal(categoryId, link.CategoryId);
        Assert.Null(link.UpdatedAtUtc);
    }
}
