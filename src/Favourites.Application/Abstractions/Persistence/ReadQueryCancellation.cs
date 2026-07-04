namespace Favourites.Application.Abstractions.Persistence;

internal static class ReadQueryCancellation
{
    // UI navigation often aborts in-flight GET requests. These read-only queries
    // are cheap enough to finish without surfacing TaskCanceledException in EF.
    public static CancellationToken IgnoreClientAbort(CancellationToken _) => CancellationToken.None;
}
