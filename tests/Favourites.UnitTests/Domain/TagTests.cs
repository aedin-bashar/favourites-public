using Favourites.Domain.Entities;

namespace Favourites.UnitTests.Domain;

public sealed class TagTests
{
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Task 2.21 — valid tag names
    [Fact]
    public void Create_WithValidName_ReturnsTag()
    {
        var tag = Tag.Create(UserId, "reading");

        Assert.NotEqual(Guid.Empty, tag.Id);
        Assert.Equal(UserId, tag.UserId);
        Assert.Equal("reading", tag.Name);
    }

    [Fact]
    public void Create_TrimsName()
    {
        var tag = Tag.Create(UserId, "   reading   ");

        Assert.Equal("reading", tag.Name);
    }

    [Fact]
    public void Create_WithNameAtMaxLength_IsAccepted()
    {
        var name = new string('a', Tag.MaxNameLength);

        var tag = Tag.Create(UserId, name);

        Assert.Equal(name, tag.Name);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var a = Tag.Create(UserId, "alpha");
        var b = Tag.Create(UserId, "beta");

        Assert.NotEqual(a.Id, b.Id);
    }

    // Task 2.21 — invalid tag names
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingName_Throws(string? name)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Tag.Create(UserId, name!));

        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_Throws()
    {
        var name = new string('a', Tag.MaxNameLength + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => Tag.Create(UserId, name));

        Assert.Equal("name", ex.ParamName);
    }

    // Task 2.23 — user ownership rules
    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Tag.Create(Guid.Empty, "reading"));

        Assert.Equal("userId", ex.ParamName);
    }

    [Fact]
    public void Create_AssignsOwnerExactlyAsProvided()
    {
        var owner = Guid.NewGuid();

        var tag = Tag.Create(owner, "reading");

        Assert.Equal(owner, tag.UserId);
    }

    [Fact]
    public void Create_SameNameForDifferentOwners_ProducesIndependentTags()
    {
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        var tagA = Tag.Create(ownerA, "reading");
        var tagB = Tag.Create(ownerB, "reading");

        Assert.NotEqual(tagA.Id, tagB.Id);
        Assert.NotEqual(tagA.UserId, tagB.UserId);
        Assert.Equal(tagA.Name, tagB.Name);
    }
}
