namespace Favourites.Application.Abstractions.Identity;

public interface ICurrentUser
{
    Guid? Id { get; }

    bool IsAuthenticated { get; }
}
