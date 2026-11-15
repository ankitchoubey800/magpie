using Magpie.Accounts.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Magpie.Accounts.Api.ErrorHandling;

/// <summary>
/// "Our database is down" is not a bug in this service, so it must not look
/// like one. It becomes 503 Service Unavailable (try again later) instead of
/// 500 Internal Server Error (we broke something).
/// </summary>
internal sealed class DependencyUnavailableExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DependencyUnavailableExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DependencyUnavailableException unavailable)
        {
            // Not ours: let the next handler (the catch-everything one) deal with it.
            return false;
        }

        logger.LogWarning(exception, "Dependency {Dependency} is unavailable", unavailable.Dependency);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "A dependency is unavailable",
                Detail = $"The accounts service cannot reach {unavailable.Dependency} right now. Please try again shortly.",
                Extensions = { ["dependency"] = unavailable.Dependency },
            },
        });
    }
}
