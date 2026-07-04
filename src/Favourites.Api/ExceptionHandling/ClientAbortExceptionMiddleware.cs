using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Favourites.Api.ExceptionHandling;

public sealed class ClientAbortExceptionMiddleware(
    RequestDelegate next,
    ILogger<ClientAbortExceptionMiddleware> logger)
{
    public const int ClientClosedRequestStatusCode = 499;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request was aborted by the client while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = ClientClosedRequestStatusCode;
            }
        }
    }
}

public static class ClientAbortExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseClientAbortExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ClientAbortExceptionMiddleware>();
}
