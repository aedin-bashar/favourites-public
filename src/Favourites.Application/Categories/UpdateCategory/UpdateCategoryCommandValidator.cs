using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Categories.UpdateCategory;

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    private readonly IFavouritesDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public UpdateCategoryCommandValidator(
        IFavouritesDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;

        RuleFor(command => command.Id)
            .NotEqual(Guid.Empty).WithMessage("Category id is required.");

        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Category name is required.")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Category name is required.")
            .Must(value => value!.Trim().Length <= Category.MaxNameLength)
                .WithMessage($"Category name must be {Category.MaxNameLength} characters or fewer.")
            .MustAsync(BeUniqueForCurrentUserAsync)
                .WithMessage("A category with this name already exists.");
    }

    private async Task<bool> BeUniqueForCurrentUserAsync(
        UpdateCategoryCommand command,
        string? name,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id;

        if (userId is null || string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        var normalizedName = name.Trim().ToLower();

        return !await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(
                category => category.UserId == userId
                    && category.Id != command.Id
                    && category.Name.ToLower() == normalizedName,
                cancellationToken);
    }
}
