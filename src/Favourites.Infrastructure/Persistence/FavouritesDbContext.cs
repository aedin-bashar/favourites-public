using Favourites.Application.Abstractions.Persistence;
using Favourites.Domain.Entities;
using Favourites.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Infrastructure.Persistence;

public sealed class FavouritesDbContext(DbContextOptions<FavouritesDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IFavouritesDbContext
{
    public DbSet<FavouriteLink> FavouriteLinks => Set<FavouriteLink>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<FavouriteLinkTag> FavouriteLinkTags => Set<FavouriteLinkTag>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>().ToTable("Users");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        modelBuilder.Entity<ApplicationUser>(user =>
        {
            user.Property(x => x.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            user.Property(x => x.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FavouritesDbContext).Assembly);
    }
}
