using System.Threading.RateLimiting;
using Magpie.Accounts.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Magpie.Accounts.Api.Security;

public static class AuthRateLimiting
{
    /// <summary>The policy name endpoints refer to with RequireRateLimiting(...).</summary>
    public const string PolicyName = "auth";

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // One counter per network address ("partition"). Each address gets
            // N requests per one-minute window; the counter then starts again.
            options.AddPolicy(PolicyName, httpContext =>
            {
                var limit = httpContext.RequestServices
                    .GetRequiredService<IOptions<AccountsOptions>>().Value.AuthRequestsPerMinute;

                // Thanks to the forwarded headers, this is the reader's address,
                // not the gateway's.
                var address = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0, // do not make callers wait in line: refuse at once
                });
            });

            // Refusals use the same error shape as everything else, plus the
            // standard Retry-After header saying how many seconds to wait.
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .TryWriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many attempts",
                            Detail = "Please wait a minute before trying again.",
                            Extensions = { ["code"] = "rate_limited" },
                        },
                    });
            };
        });

        return services;
    }
}
