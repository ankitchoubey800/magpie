using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Magpie.Defaults;

/// <summary>
/// Last line of defence: turns any unexpected exception into a 500 Problem Details
/// response. The full exception goes to the log; the caller only sees a safe message.
/// </summary>
internal sealed class UnhandledExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Returning true means "handled, stop looking for other handlers".
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
                Detail = "The error has been logged. Quote the traceId if you report it.",
            },
        });
    }
}
