using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Users.Dtos;
using Favourites.Application.Users.Mapping;
using Favourites.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Users.GetUserPreferences;

public sealed class GetUserPreferencesHandler(
    ICurrentUser currentUser,
    IFavouritesDbContext dbContext)
{
    public async Task<UserPreferencesDto> HandleAsync(
        GetUserPreferencesQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id
            ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
        cancellationToken = ReadQueryCancellation.IgnoreClientAbort(cancellationToken);

        var preferences = await dbContext.UserPreferences
            .SingleOrDefaultAsync(preferences => preferences.UserId == userId, cancellationToken);

        return (preferences ?? UserPreferences.CreateDefault(userId)).ToDto();
    }
}
