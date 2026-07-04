namespace Favourites.Application.Auth.ResetPassword;

public sealed record ResetPasswordResult(bool Succeeded, string? Error = null);
