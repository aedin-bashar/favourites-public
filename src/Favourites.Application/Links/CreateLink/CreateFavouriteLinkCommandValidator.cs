using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Favourites.Application.Links.CreateLink;

public sealed class CreateFavouriteLinkCommandValidator : AbstractValidator<CreateFavouriteLinkCommand>
{
    private readonly IFavouritesDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CreateFavouriteLinkCommandValidator(
        IFavouritesDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;

        RuleFor(command => command.Url)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("URL is required.")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("URL is required.")
            .Must(value => value!.Trim().Length <= FavouriteLink.MaxUrlLength)
                .WithMessage($"URL must be {FavouriteLink.MaxUrlLength} characters or fewer.")
            .Must(BeAbsoluteHttpUrl)
                .WithMessage("URL must be a valid absolute http or https URL.");

        RuleFor(command => command.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithMessage("Title is required.")
            .Must(value => value!.Trim().Length <= FavouriteLink.MaxTitleLength)
                .WithMessage($"Title must be {FavouriteLink.MaxTitleLength} characters or fewer.");

        RuleFor(command => command.Description!)
            .Must(value => value.Trim().Length <= FavouriteLink.MaxDescriptionLength)
                .WithMessage($"Description must be {FavouriteLink.MaxDescriptionLength} characters or fewer.")
            .When(command => !string.IsNullOrWhiteSpace(command.Description));

        RuleFor(command => command.TagIds)
            .Cascade(CascadeMode.Stop)
            .Must(tagIds => tagIds is null || tagIds.All(id => id != Guid.Empty))
                .WithMessage("Tag ids must not be empty.")
            .Must(tagIds => tagIds is null || tagIds.Distinct().Count() == tagIds.Count)
                .WithMessage("Tag ids must be unique.")
            .MustAsync(BeOwnedByCurrentUserAsync)
                .WithMessage("One or more selected tags are not available.");

        RuleFor(command => command.CategoryId)
            .Cascade(CascadeMode.Stop)
            .Must(id => id is null || id != Guid.Empty)
                .WithMessage("Category id must not be empty.")
            .MustAsync(CategoryBeOwnedByCurrentUserAsync)
                .WithMessage("The selected category is not available.");
    }

    private static bool BeAbsoluteHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }

    private async Task<bool> BeOwnedByCurrentUserAsync(
        IReadOnlyCollection<Guid>? tagIds,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id;

        if (userId is null || tagIds is null || tagIds.Count == 0)
        {
            return true;
        }

        var uniqueTagIds = tagIds.Distinct().ToArray();

        var ownedTagCount = await _dbContext.Tags
            .AsNoTracking()
            .CountAsync(
                tag => tag.UserId == userId && uniqueTagIds.Contains(tag.Id),
                cancellationToken);

        return ownedTagCount == uniqueTagIds.Length;
    }

    private async Task<bool> CategoryBeOwnedByCurrentUserAsync(
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id;

        if (userId is null || categoryId is null || categoryId == Guid.Empty)
        {
            return true;
        }

        return await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(
                category => category.Id == categoryId && category.UserId == userId,
                cancellationToken);
    }
}
