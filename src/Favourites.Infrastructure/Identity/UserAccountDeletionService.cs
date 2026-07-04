using Favourites.Application.Abstractions.Identity;
using Favourites.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Infrastructure.Identity;

public sealed class UserAccountDeletionService(
    FavouritesDbContext dbContext,
    UserManager<ApplicationUser> userManager) : IUserAccountDeletionService
{
    public async Task<bool> DeleteCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return false;
        }

        if (dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            await DeleteOwnedDataAsync(userId, cancellationToken);
            await DeleteIdentityUserAsync(user, cancellationToken);
            return true;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await DeleteOwnedDataAsync(userId, cancellationToken);
        await DeleteIdentityUserAsync(user, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task DeleteOwnedDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var linkIds = await dbContext.FavouriteLinks
            .Where(link => link.UserId == userId)
            .Select(link => link.Id)
            .ToListAsync(cancellationToken);

        var tagIds = await dbContext.Tags
            .Where(tag => tag.UserId == userId)
            .Select(tag => tag.Id)
            .ToListAsync(cancellationToken);

        var linkTags = await dbContext.FavouriteLinkTags
            .Where(linkTag =>
                linkIds.Contains(linkTag.FavouriteLinkId) ||
                tagIds.Contains(linkTag.TagId))
            .ToListAsync(cancellationToken);

        var links = await dbContext.FavouriteLinks
            .Where(link => link.UserId == userId)
            .ToListAsync(cancellationToken);

        var tags = await dbContext.Tags
            .Where(tag => tag.UserId == userId)
            .ToListAsync(cancellationToken);

        var categories = await dbContext.Categories
            .Where(category => category.UserId == userId)
            .ToListAsync(cancellationToken);

        var passwordResetTokens = await dbContext.PasswordResetTokens
            .Where(token => token.UserId == userId)
            .ToListAsync(cancellationToken);

        var preferences = await dbContext.UserPreferences
            .Where(preferences => preferences.UserId == userId)
            .ToListAsync(cancellationToken);

        dbContext.FavouriteLinkTags.RemoveRange(linkTags);
        dbContext.FavouriteLinks.RemoveRange(links);
        dbContext.UserPreferences.RemoveRange(preferences);
        dbContext.Tags.RemoveRange(tags);
        dbContext.Categories.RemoveRange(categories);
        dbContext.PasswordResetTokens.RemoveRange(passwordResetTokens);
    }

    private async Task DeleteIdentityUserAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var result = await userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not delete account: {errors}");
        }
    }
}
