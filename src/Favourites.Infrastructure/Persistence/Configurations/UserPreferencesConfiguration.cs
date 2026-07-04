using Favourites.Domain.Entities;
using Favourites.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Favourites.Infrastructure.Persistence.Configurations;

internal sealed class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("UserPreferences");

        builder.HasKey(preferences => preferences.UserId);

        builder.Property(preferences => preferences.Theme)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(preferences => preferences.Density)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(preferences => preferences.DefaultCategoryId);

        builder.Property(preferences => preferences.AutoExtractTitle)
            .IsRequired();

        builder.Property(preferences => preferences.ShowFavicon)
            .IsRequired();

        builder.Property(preferences => preferences.OpenInNewTab)
            .IsRequired();

        builder.Property(preferences => preferences.ConfirmBeforeDelete)
            .IsRequired();

        builder.Property(preferences => preferences.SuggestTagsAutomatically)
            .IsRequired();

        builder.Property(preferences => preferences.ShowColorsOnTagChips)
            .IsRequired();

        builder.Property(preferences => preferences.TagsDefaultSort)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(preferences => preferences.CategoriesDefaultSort)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(preferences => preferences.WeeklySummaryEmail)
            .IsRequired();

        builder.Property(preferences => preferences.SecurityAlerts)
            .IsRequired();

        builder.Property(preferences => preferences.ProductUpdates)
            .IsRequired();

        builder.Property(preferences => preferences.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<UserPreferences>(preferences => preferences.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(preferences => preferences.DefaultCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
