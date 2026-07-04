using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Tags.UpdateTag;

public sealed class UpdateTagCommandValidator : AbstractValidator<UpdateTagCommand>
{
    private readonly IFavouritesDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public UpdateTagCommandValidator(
        IFavouritesDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;

        RuleFor(command => command.Id)
            .NotEqual(Guid.Empty).WithMessage("Tag id is required.");

        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Tag name is required.")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Tag name is required.")
            .Must(value => value!.Trim().Length <= Tag.MaxNameLength)
                .WithMessage($"Tag name must be {Tag.MaxNameLength} characters or fewer.")
            .MustAsync(BeUniqueForCurrentUserAsync)
                .WithMessage("A tag with this name already exists.");
    }

    private async Task<bool> BeUniqueForCurrentUserAsync(
        UpdateTagCommand command,
        string? name,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id;

        if (userId is null || string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        var normalizedName = name.Trim().ToLower();

        return !await _dbContext.Tags
            .AsNoTracking()
            .AnyAsync(
                tag => tag.UserId == userId
                    && tag.Id != command.Id
                    && tag.Name.ToLower() == normalizedName,
                cancellationToken);
    }
}
