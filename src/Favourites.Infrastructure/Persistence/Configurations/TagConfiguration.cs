using Favourites.Domain.Entities;
using Favourites.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Favourites.Infrastructure.Persistence.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");

        builder.HasKey(tag => tag.Id);

        builder.Property(tag => tag.UserId)
            .IsRequired();

        builder.Property(tag => tag.Name)
            .HasMaxLength(Tag.MaxNameLength)
            .IsRequired();

        builder.Property(tag => tag.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(tag => tag.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tag => tag.UserId)
            .HasDatabaseName("IX_Tags_UserId");

        builder.HasIndex(tag => new { tag.UserId, tag.Name })
            .HasDatabaseName("IX_Tags_UserId_Name")
            .IsUnique();
    }
}
