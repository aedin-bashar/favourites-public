using Favourites.Api.Contracts.Auth;
using Favourites.Application.Auth.ForgotPassword;
using Favourites.Application.Auth.ResetPassword;
using Favourites.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Favourites.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var validationErrors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            validationErrors[nameof(RegisterRequest.DisplayName)] = ["Display name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            validationErrors[nameof(RegisterRequest.Email)] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            validationErrors[nameof(RegisterRequest.Password)] = ["Password is required."];
        }

        if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            validationErrors[nameof(RegisterRequest.ConfirmPassword)] = ["Confirm password is required."];
        }
        else if (request.Password != request.ConfirmPassword)
        {
            validationErrors[nameof(RegisterRequest.ConfirmPassword)] = ["Password and confirm password must match."];
        }

        if (validationErrors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                ApiResponseMapping.ToIdentityValidationErrors(result.Errors)));
        }

        var principal = await claimsPrincipalFactory.CreateAsync(user);
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);

        return Ok(new RegisterResponse(user.Id, user.DisplayName, user.Email ?? string.Empty));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var validationErrors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            validationErrors[nameof(LoginRequest.Email)] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            validationErrors[nameof(LoginRequest.Password)] = ["Password is required."];
        }

        if (validationErrors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return Unauthorized();
        }

        var passwordIsValid = await userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordIsValid)
        {
            return Unauthorized();
        }

        var principal = await claimsPrincipalFactory.CreateAsync(user);
        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = request.RememberMe
            });

        return Ok(new LoginResponse(user.Id, user.DisplayName, user.Email ?? string.Empty));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        return Ok(new LogoutResponse(true));
    }

    [HttpGet("current-user")]
    [Authorize]
    public async Task<IActionResult> CurrentUser()
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserResponse(user.Id, user.DisplayName, user.Email ?? string.Empty));
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("ForgotPassword")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        [FromServices] ForgotPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            // Still 200 — never reveal whether the email exists.
            return Ok();
        }

        var resetBaseUrl = BuildResetBaseUrl(Request);

        await handler.HandleAsync(
            new ForgotPasswordCommand(request.Email.Trim(), resetBaseUrl),
            cancellationToken);

        return Ok();
    }

    private static string BuildResetBaseUrl(HttpRequest request)
    {
        var origin = $"{request.Scheme}://{request.Host.Value}";

        if (request.PathBase.HasValue)
        {
            return $"{origin}{request.PathBase.Value}";
        }

        return TryGetBasePathFromReferer(request, out var refererBasePath)
            ? $"{origin}{refererBasePath}"
            : origin;
    }

    private static bool TryGetBasePathFromReferer(HttpRequest request, out string basePath)
    {
        basePath = string.Empty;

        var referer = request.Headers.Referer.ToString();
        if (!Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            return false;
        }

        var refererHost = refererUri.IsDefaultPort
            ? refererUri.Host
            : $"{refererUri.Host}:{refererUri.Port}";

        if (!string.Equals(refererHost, request.Host.Value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string forgotPasswordRoute = "/forgot-password";
        var refererPath = refererUri.AbsolutePath.TrimEnd('/');

        if (!refererPath.EndsWith(forgotPasswordRoute, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        basePath = refererPath[..^forgotPasswordRoute.Length].TrimEnd('/');
        return !string.IsNullOrWhiteSpace(basePath);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        [FromServices] ResetPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        var validationErrors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            validationErrors[nameof(ResetPasswordRequest.Token)] = ["Reset token is required."];
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            validationErrors[nameof(ResetPasswordRequest.NewPassword)] = ["New password is required."];
        }

        if (string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
        {
            validationErrors[nameof(ResetPasswordRequest.ConfirmNewPassword)] = ["Confirm password is required."];
        }
        else if (request.NewPassword != request.ConfirmNewPassword)
        {
            validationErrors[nameof(ResetPasswordRequest.ConfirmNewPassword)] = ["Passwords must match."];
        }

        if (validationErrors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var result = await handler.HandleAsync(
            new ResetPasswordCommand(request.Token, request.NewPassword),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok();
    }
}
