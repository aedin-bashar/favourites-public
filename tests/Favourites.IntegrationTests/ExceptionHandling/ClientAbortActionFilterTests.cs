using Favourites.Api.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Favourites.IntegrationTests.ExceptionHandling;

public sealed class ClientAbortActionFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WhenExecutedContextHasClientAbortException_HandlesException()
    {
        var httpContext = await CreateAbortedHttpContextAsync();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/api/categories";

        var actionContext = CreateActionContext(httpContext);
        var executingContext = CreateExecutingContext(actionContext);
        var executedContext = CreateExecutedContext(actionContext);
        executedContext.Exception = new TaskCanceledException("The request was aborted.");

        var filter = new ClientAbortActionFilter(NullLogger<ClientAbortActionFilter>.Instance);

        await filter.OnActionExecutionAsync(
            executingContext,
            () => Task.FromResult(executedContext));

        Assert.True(executedContext.ExceptionHandled);
        var result = Assert.IsType<StatusCodeResult>(executedContext.Result);
        Assert.Equal(ClientAbortExceptionMiddleware.ClientClosedRequestStatusCode, result.StatusCode);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenClientAbortExceptionIsThrown_HandlesException()
    {
        var httpContext = await CreateAbortedHttpContextAsync();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/api/tags";

        var executingContext = CreateExecutingContext(CreateActionContext(httpContext));
        var filter = new ClientAbortActionFilter(NullLogger<ClientAbortActionFilter>.Instance);

        await filter.OnActionExecutionAsync(
            executingContext,
            () => throw new TaskCanceledException("The request was aborted."));

        var result = Assert.IsType<StatusCodeResult>(executingContext.Result);
        Assert.Equal(ClientAbortExceptionMiddleware.ClientClosedRequestStatusCode, result.StatusCode);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenRequestWasNotAborted_DoesNotHandleException()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = CreateActionContext(httpContext);
        var executingContext = CreateExecutingContext(actionContext);
        var executedContext = CreateExecutedContext(actionContext);
        executedContext.Exception = new TaskCanceledException("The operation was canceled.");

        var filter = new ClientAbortActionFilter(NullLogger<ClientAbortActionFilter>.Instance);

        await filter.OnActionExecutionAsync(
            executingContext,
            () => Task.FromResult(executedContext));

        Assert.False(executedContext.ExceptionHandled);
        Assert.Null(executedContext.Result);
    }

    private static async Task<DefaultHttpContext> CreateAbortedHttpContextAsync()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        return new DefaultHttpContext
        {
            RequestAborted = cts.Token
        };
    }

    private static ActionContext CreateActionContext(HttpContext httpContext) =>
        new(httpContext, new RouteData(), new ActionDescriptor());

    private static ActionExecutingContext CreateExecutingContext(ActionContext actionContext) =>
        new(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

    private static ActionExecutedContext CreateExecutedContext(ActionContext actionContext) =>
        new(
            actionContext,
            new List<IFilterMetadata>(),
            new object());
}
