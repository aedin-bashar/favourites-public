using System.Reflection;
using Favourites.Application.Dashboard.GetDashboardSummary;
using Favourites.Application.Categories.CreateCategory;
using Favourites.Application.Categories.DeleteCategory;
using Favourites.Application.Categories.GetCategoriesDuplicates;
using Favourites.Application.Categories.GetCategoriesSummary;
using Favourites.Application.Categories.GetUserCategories;
using Favourites.Application.Categories.MergeCategories;
using Favourites.Application.Categories.UpdateCategory;
using Favourites.Application.Links.ArchiveFavouriteLink;
using Favourites.Application.Links.CreateLink;
using Favourites.Application.Links.DeleteArchivedLinks;
using Favourites.Application.Links.DeleteFavouriteLink;
using Favourites.Application.Links.GetArchivedSummary;
using Favourites.Application.Links.GetCleanupSuggestions;
using Favourites.Application.Links.GetFavouriteLinkById;
using Favourites.Application.Links.GetUserLinks;
using Favourites.Application.Links.ImportLinks;
using Favourites.Application.Links.RestoreArchivedLink;
using Favourites.Application.Links.RestoreManyLinks;
using Favourites.Application.Links.UpdateFavouriteLink;
using Favourites.Application.Tags.CreateTag;
using Favourites.Application.Tags.DeleteTag;
using Favourites.Application.Tags.GetTagsDuplicates;
using Favourites.Application.Tags.GetTagsSummary;
using Favourites.Application.Tags.GetUserTags;
using Favourites.Application.Tags.MergeTags;
using Favourites.Application.Tags.UpdateTag;
using Favourites.Application.Users.GetUserPreferences;
using Favourites.Application.Users.PatchUserPreferences;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<GetDashboardSummaryHandler>();
        services.AddScoped<CreateFavouriteLinkHandler>();
        services.AddScoped<GetUserLinksHandler>();
        services.AddScoped<GetFavouriteLinkByIdHandler>();
        services.AddScoped<UpdateFavouriteLinkHandler>();
        services.AddScoped<DeleteFavouriteLinkHandler>();
        services.AddScoped<ArchiveFavouriteLinkHandler>();
        services.AddScoped<RestoreArchivedLinkHandler>();
        services.AddScoped<GetArchivedSummaryHandler>();
        services.AddScoped<RestoreManyLinksHandler>();
        services.AddScoped<DeleteArchivedLinksHandler>();
        services.AddScoped<GetUserTagsHandler>();
        services.AddScoped<GetTagsSummaryHandler>();
        services.AddScoped<CreateTagHandler>();
        services.AddScoped<UpdateTagHandler>();
        services.AddScoped<DeleteTagHandler>();
        services.AddScoped<GetUserCategoriesHandler>();
        services.AddScoped<GetCategoriesSummaryHandler>();
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<UpdateCategoryHandler>();
        services.AddScoped<DeleteCategoryHandler>();
        services.AddScoped<GetUserPreferencesHandler>();
        services.AddScoped<PatchUserPreferencesHandler>();
        services.AddScoped<ImportLinksHandler>();
        services.AddScoped<ImportJsonLinksHandler>();
        services.AddScoped<GetCleanupSuggestionsHandler>();
        services.AddScoped<GetTagsDuplicatesHandler>();
        services.AddScoped<MergeTagsHandler>();
        services.AddScoped<GetCategoriesDuplicatesHandler>();
        services.AddScoped<MergeCategoriesHandler>();

        return services;
    }
}
