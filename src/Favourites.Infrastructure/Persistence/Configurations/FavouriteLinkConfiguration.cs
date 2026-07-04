using Favourites.Domain.Entities;
using Favourites.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Favourites.Infrastructure.Persistence.Configurations;

internal sealed class FavouriteLinkConfiguration : IEntityTypeConfiguration<FavouriteLink>
{
    public void Configure(EntityTypeBuilder<FavouriteLink> builder)
    {
        builder.ToTable("Links");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.UserId)
            .IsRequired();

        builder.Property(link => link.Url)
            .HasMaxLength(FavouriteLink.MaxUrlLength)
            .IsRequired();

        builder.Property(link => link.Title)
            .HasMaxLength(FavouriteLink.MaxTitleLength)
            .IsRequired();

        builder.Property(link => link.Description)
            .HasMaxLength(FavouriteLink.MaxDescriptionLength);

        builder.Property(link => link.CategoryId);

        builder.Property(link => link.IsArchived)
            .IsRequired();

        builder.Property(link => link.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(link => link.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // DeleteCategoryHandler nulls CategoryId before removing a category.
        // Restrict avoids SQL Server multi-cascade paths via ApplicationUser.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(link => link.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => link.UserId)
            .HasDatabaseName("IX_Links_UserId");

        builder.HasIndex(link => link.CreatedAtUtc)
            .HasDatabaseName("IX_Links_CreatedAtUtc")
            .IsDescending();

        builder.HasIndex(link => link.CategoryId)
            .HasDatabaseName("IX_Links_CategoryId");
    }
}
