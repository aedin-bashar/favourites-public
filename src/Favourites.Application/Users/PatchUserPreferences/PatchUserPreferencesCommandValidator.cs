using Favourites.Domain.Entities;
using FluentValidation;

namespace Favourites.Application.Users.PatchUserPreferences;

public sealed class PatchUserPreferencesCommandValidator : AbstractValidator<PatchUserPreferencesCommand>
{
    private static readonly string[] Themes =
    [
        UserPreferences.ThemeLight,
        UserPreferences.ThemeDark,
        UserPreferences.ThemeSystem
    ];

    private static readonly string[] Densities =
    [
        UserPreferences.DensityComfortable,
        UserPreferences.DensityCompact
    ];

    private static readonly string[] TagSorts =
    [
        UserPreferences.TagsSortName,
        UserPreferences.TagsSortMostUsed,
        UserPreferences.TagsSortNewest
    ];

    private static readonly string[] CategorySorts =
    [
        UserPreferences.CategoriesSortName,
        UserPreferences.CategoriesSortLargest,
        UserPreferences.CategoriesSortNewest,
        UserPreferences.CategoriesSortRecentlyActive
    ];

    public PatchUserPreferencesCommandValidator()
    {
        RuleFor(command => command.Theme)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => Themes.Contains(value.Trim().ToLowerInvariant()))
            .WithMessage("Theme must be light, dark, or system.");

        RuleFor(command => command.Density)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => Densities.Contains(value.Trim().ToLowerInvariant()))
            .WithMessage("Density must be comfortable or compact.");

        RuleFor(command => command.DefaultCategoryId)
            .NotEqual(Guid.Empty)
            .When(command => command.DefaultCategoryId is not null);

        RuleFor(command => command.TagsDefaultSort)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => TagSorts.Contains(value.Trim().ToLowerInvariant()))
            .WithMessage("Tags default sort is not supported.");

        RuleFor(command => command.CategoriesDefaultSort)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => CategorySorts.Contains(value.Trim().ToLowerInvariant()))
            .WithMessage("Categories default sort is not supported.");
    }
}
