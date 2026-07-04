using Favourites.Api.Contracts.User;
using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Users.GetUserPreferences;
using Favourites.Application.Users.PatchUserPreferences;
using Favourites.Infrastructure.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Favourites.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/user")]
public sealed class UserController(
    UserManager<ApplicationUser> userManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory,
    IUserAccountDeletionService accountDeletionService) : ControllerBase
{
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(
        [FromServices] GetUserPreferencesHandler handler,
        CancellationToken cancellationToken)
    {
        var dto = await handler.HandleAsync(new GetUserPreferencesQuery(), cancellationToken);

        return Ok(ApiResponseMapping.ToUserPreferencesResponse(dto));
    }

    [HttpPatch("preferences")]
    public async Task<IActionResult> PatchPreferences(
        [FromBody] PatchUserPreferencesRequest request,
        [FromServices] IValidator<PatchUserPreferencesCommand> validator,
        [FromServices] PatchUserPreferencesHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new PatchUserPreferencesCommand(
            request.Theme,
            request.Density,
            request.DefaultCategoryId,
            request.AutoExtractTitle,
            request.ShowFavicon,
            request.OpenInNewTab,
            request.ConfirmBeforeDelete,
            request.SuggestTagsAutomatically,
            request.ShowColorsOnTagChips,
            request.TagsDefaultSort,
            request.CategoriesDefaultSort,
            request.WeeklySummaryEmail,
            request.SecurityAlerts,
            request.ProductUpdates);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(
                ApiResponseMapping.ToFluentValidationErrors(validationResult)));
        }

        var dto = await handler.HandleAsync(command, cancellationToken);

        if (dto is null)
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(PatchUserPreferencesRequest.DefaultCategoryId)] =
                        ["Default category was not found."]
                }));
        }

        return Ok(ApiResponseMapping.ToUserPreferencesResponse(dto));
    }

    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var validationErrors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            validationErrors[nameof(UpdateProfileRequest.DisplayName)] =
                ["Display name is required."];
        }
        else if (request.DisplayName.Trim().Length > 200)
        {
            validationErrors[nameof(UpdateProfileRequest.DisplayName)] =
                ["Display name must be 200 characters or fewer."];
        }

        if (validationErrors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        user.DisplayName = request.DisplayName.Trim();
        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                ApiResponseMapping.ToIdentityValidationErrors(result.Errors)));
        }

        var principal = await claimsPrincipalFactory.CreateAsync(user);
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);

        return Ok(new UserProfileResponse(user.Id, user.DisplayName, user.Email ?? string.Empty));
    }

    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var deleted = await accountDeletionService.DeleteCurrentUserAsync(user.Id, cancellationToken);

        if (!deleted)
        {
            return Unauthorized();
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        return NoContent();
    }
}
