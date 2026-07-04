using Favourites.Api.Contracts.Auth;
using Favourites.Api.Contracts.Categories;
using Favourites.Api.Contracts.Links;
using Favourites.Api.Contracts.Tags;
using Favourites.Api.Contracts.User;
using Favourites.Application.Categories.Dtos;
using Favourites.Application.Links.Dtos;
using Favourites.Application.Tags.Dtos;
using Favourites.Application.Users.Dtos;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;

namespace Favourites.Api.Controllers;

internal static class ApiResponseMapping
{
    public static LinkResponse ToLinkResponse(FavouriteLinkDto dto) =>
        new(
            Id: dto.Id,
            Url: dto.Url,
            Title: dto.Title,
            Description: dto.Description,
            IsArchived: dto.IsArchived,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc,
            Tags: dto.Tags
                .Select(ToTagResponse)
                .ToList(),
            Category: dto.Category is null
                ? null
                : ToCategoryResponse(dto.Category));

    public static TagResponse ToTagResponse(TagDto dto) =>
        new(
            Id: dto.Id,
            Name: dto.Name,
            LinkCount: dto.LinkCount,
            CreatedAtUtc: dto.CreatedAtUtc,
            LastUsedAtUtc: dto.LastUsedAtUtc);

    public static CategoryResponse ToCategoryResponse(CategoryDto dto) =>
        new(
            Id: dto.Id,
            Name: dto.Name,
            Color: dto.Color,
            LinkCount: dto.LinkCount,
            CreatedAtUtc: dto.CreatedAtUtc,
            LastActivityAtUtc: dto.LastActivityAtUtc);

    public static UserPreferencesResponse ToUserPreferencesResponse(UserPreferencesDto dto) =>
        new(
            dto.Theme,
            dto.Density,
            dto.DefaultCategoryId,
            dto.AutoExtractTitle,
            dto.ShowFavicon,
            dto.OpenInNewTab,
            dto.ConfirmBeforeDelete,
            dto.SuggestTagsAutomatically,
            dto.ShowColorsOnTagChips,
            dto.TagsDefaultSort,
            dto.CategoriesDefaultSort,
            dto.WeeklySummaryEmail,
            dto.SecurityAlerts,
            dto.ProductUpdates,
            dto.UpdatedAtUtc);

    public static Dictionary<string, string[]> ToFluentValidationErrors(ValidationResult result) =>
        result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

    public static Dictionary<string, string[]> ToIdentityValidationErrors(IEnumerable<IdentityError> errors) =>
        errors
            .GroupBy(error => error.Code switch
            {
                "DuplicateEmail" => nameof(RegisterRequest.Email),
                "DuplicateUserName" => nameof(RegisterRequest.Email),
                "InvalidEmail" => nameof(RegisterRequest.Email),
                "PasswordTooShort" => nameof(RegisterRequest.Password),
                "PasswordRequiresDigit" => nameof(RegisterRequest.Password),
                "PasswordRequiresLower" => nameof(RegisterRequest.Password),
                "PasswordRequiresUpper" => nameof(RegisterRequest.Password),
                "PasswordRequiresNonAlphanumeric" => nameof(RegisterRequest.Password),
                "PasswordRequiresUniqueChars" => nameof(RegisterRequest.Password),
                _ => string.Empty
            })
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).Distinct().ToArray());
}
