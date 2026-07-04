using Favourites.Api.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Favourites.IntegrationTests.ExceptionHandling;

public sealed class ClientAbortExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenRequestWasAborted_HandlesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var context = new DefaultHttpContext
        {
            RequestAborted = cts.Token,
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/links";

        var middleware = new ClientAbortExceptionMiddleware(
            _ => throw new TaskCanceledException("The request was aborted."),
            NullLogger<ClientAbortExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(ClientAbortExceptionMiddleware.ClientClosedRequestStatusCode, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestWasNotAborted_RethrowsOperationCanceledException()
    {
        var context = new DefaultHttpContext();
        var middleware = new ClientAbortExceptionMiddleware(
            _ => throw new TaskCanceledException("The operation was canceled."),
            NullLogger<ClientAbortExceptionMiddleware>.Instance);

        await Assert.ThrowsAsync<TaskCanceledException>(() => middleware.InvokeAsync(context));
    }
}
