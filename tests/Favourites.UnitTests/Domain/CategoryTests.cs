using Favourites.Domain.Entities;

namespace Favourites.UnitTests.Domain;

public sealed class CategoryTests
{
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // Task 2.22 — valid category names
    [Fact]
    public void Create_WithValidName_ReturnsCategory()
    {
        var category = Category.Create(UserId, "Work");

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal(UserId, category.UserId);
        Assert.Equal("Work", category.Name);
    }

    [Fact]
    public void Create_TrimsName()
    {
        var category = Category.Create(UserId, "   Work   ");

        Assert.Equal("Work", category.Name);
    }

    [Fact]
    public void Create_WithNameAtMaxLength_IsAccepted()
    {
        var name = new string('c', Category.MaxNameLength);

        var category = Category.Create(UserId, name);

        Assert.Equal(name, category.Name);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var a = Category.Create(UserId, "Work");
        var b = Category.Create(UserId, "Personal");

        Assert.NotEqual(a.Id, b.Id);
    }

    // Task 2.22 — invalid category names
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingName_Throws(string? name)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Category.Create(UserId, name!));

        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_Throws()
    {
        var name = new string('c', Category.MaxNameLength + 1);

        var ex = Assert.Throws<ArgumentException>(
            () => Category.Create(UserId, name));

        Assert.Equal("name", ex.ParamName);
    }

    // Task 2.23 — user ownership rules
    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Category.Create(Guid.Empty, "Work"));

        Assert.Equal("userId", ex.ParamName);
    }

    [Fact]
    public void Create_AssignsOwnerExactlyAsProvided()
    {
        var owner = Guid.NewGuid();

        var category = Category.Create(owner, "Work");

        Assert.Equal(owner, category.UserId);
    }

    [Fact]
    public void Create_SameNameForDifferentOwners_ProducesIndependentCategories()
    {
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();

        var categoryA = Category.Create(ownerA, "Work");
        var categoryB = Category.Create(ownerB, "Work");

        Assert.NotEqual(categoryA.Id, categoryB.Id);
        Assert.NotEqual(categoryA.UserId, categoryB.UserId);
        Assert.Equal(categoryA.Name, categoryB.Name);
    }
}
