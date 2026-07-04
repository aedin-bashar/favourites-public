using Favourites.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Abstractions.Persistence;

public interface IFavouritesDbContext
{
    DbSet<FavouriteLink> FavouriteLinks { get; }

    DbSet<Tag> Tags { get; }

    DbSet<Category> Categories { get; }

    DbSet<FavouriteLinkTag> FavouriteLinkTags { get; }

    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    DbSet<UserPreferences> UserPreferences { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
