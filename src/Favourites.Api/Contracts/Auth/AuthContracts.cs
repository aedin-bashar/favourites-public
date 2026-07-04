namespace Favourites.Api.Contracts.Auth;

public sealed record RegisterRequest(
    string DisplayName,
    string Email,
    string Password,
    string ConfirmPassword);

public sealed record RegisterResponse(
    Guid Id,
    string DisplayName,
    string Email);

public sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false);

public sealed record LoginResponse(
    Guid Id,
    string DisplayName,
    string Email);

public sealed record LogoutRequest;

public sealed record LogoutResponse(bool Succeeded);

public sealed record CurrentUserRequest;

public sealed record CurrentUserResponse(
    Guid Id,
    string DisplayName,
    string Email);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword, string ConfirmNewPassword);
