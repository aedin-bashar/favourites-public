namespace Favourites.Infrastructure.Email;

public sealed class EmailSettings
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string UserName { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string SubjectPrefix { get; init; } = string.Empty;
}
