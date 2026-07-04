using Favourites.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Favourites.Infrastructure.Persistence.Configurations;

internal sealed class FavouriteLinkTagConfiguration : IEntityTypeConfiguration<FavouriteLinkTag>
{
    public void Configure(EntityTypeBuilder<FavouriteLinkTag> builder)
    {
        builder.ToTable("LinkTags");

        builder.HasKey(link => new { link.FavouriteLinkId, link.TagId });

        builder.HasOne<FavouriteLink>()
            .WithMany()
            .HasForeignKey(link => link.FavouriteLinkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tag FK uses Restrict (not Cascade) to break SQL Server's multi-cascade-path
        // constraint: both FavouriteLink and Tag cascade from ApplicationUser, so a join
        // row would be reachable by two cascade paths during user deletion. When a user
        // is deleted, FavouriteLink rows cascade first (which clears their join rows),
        // then Tag rows delete cleanly. The DeleteTag use case must remove
        // FavouriteLinkTag rows for a tag before deleting the tag itself.
        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(link => link.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite PK (FavouriteLinkId, TagId) already covers "given a link, list its tags".
        // This index supports the reverse lookup "given a tag, list its links".
        builder.HasIndex(link => link.TagId)
            .HasDatabaseName("IX_LinkTags_TagId");
    }
}
