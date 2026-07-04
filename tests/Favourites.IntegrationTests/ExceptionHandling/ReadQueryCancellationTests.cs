using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Links.GetUserLinks;
using Favourites.Domain.Entities;
using Favourites.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Favourites.IntegrationTests.ExceptionHandling;

public sealed class ReadQueryCancellationTests
{
    [Fact]
    public async Task GetUserLinksHandler_WhenClientAbortTokenIsAlreadyCanceled_CompletesReadQuery()
    {
        var userId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();

        var category = Category.Create(userId, "Docs");
        var tag = Tag.Create(userId, "dotnet");
        var link = FavouriteLink.Create(
            userId,
            "https://example.com/docs",
            "Docs",
            description: null,
            category.Id);

        dbContext.Categories.Add(category);
        dbContext.Tags.Add(tag);
        dbContext.FavouriteLinks.Add(link);
        dbContext.FavouriteLinkTags.Add(FavouriteLinkTag.Create(link.Id, tag.Id));
        await dbContext.SaveChangesAsync();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var handler = new GetUserLinksHandler(new TestCurrentUser(userId), dbContext);

        var result = await handler.HandleAsync(
            new GetUserLinksQuery(Page: 1, PageSize: 25),
            cts.Token);

        Assert.Equal(1, result.Total);
        var item = Assert.Single(result.Items);
        Assert.Equal(link.Id, item.Id);
        Assert.Single(item.Tags);
        Assert.NotNull(item.Category);
    }

    private static FavouritesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FavouritesDbContext>()
            .UseInMemoryDatabase($"read-query-cancellation-{Guid.NewGuid()}")
            .Options;

        return new FavouritesDbContext(options);
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid? Id => id;
        public bool IsAuthenticated => true;
    }
}
