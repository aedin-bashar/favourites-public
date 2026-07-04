namespace Favourites.Application.Auth.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword);
