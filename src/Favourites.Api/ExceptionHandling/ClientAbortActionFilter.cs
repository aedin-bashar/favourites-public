using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Favourites.Api.ExceptionHandling;

public sealed class ClientAbortActionFilter(
    ILogger<ClientAbortActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        try
        {
            var executedContext = await next();

            if (executedContext.Exception is OperationCanceledException &&
                context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                LogClientAbort(context.HttpContext);
                MarkExceptionHandled(executedContext);
            }
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            LogClientAbort(context.HttpContext);
            MarkActionHandled(context);
        }
    }

    private void MarkExceptionHandled(ActionExecutedContext context)
    {
        context.ExceptionHandled = true;

        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.Clear();
            context.Result = new StatusCodeResult(ClientAbortExceptionMiddleware.ClientClosedRequestStatusCode);
        }
    }

    private static void MarkActionHandled(ActionExecutingContext context)
    {
        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.Clear();
            context.Result = new StatusCodeResult(ClientAbortExceptionMiddleware.ClientClosedRequestStatusCode);
        }
    }

    private void LogClientAbort(HttpContext context)
    {
        logger.LogDebug(
            "Request was aborted by the client while processing {Method} {Path}",
            context.Request.Method,
            context.Request.Path);
    }
}
