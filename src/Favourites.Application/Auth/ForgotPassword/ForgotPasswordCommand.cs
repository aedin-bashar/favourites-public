namespace Favourites.Application.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email, string ResetBaseUrl);
