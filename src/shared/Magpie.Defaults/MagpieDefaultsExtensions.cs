using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Magpie.Defaults;

/// <summary>
/// The things EVERY Magpie service must do the same way: logging, error format
/// and health endpoints. A service calls three methods and gets all of it.
/// </summary>
public static class MagpieDefaultsExtensions
{
    /// <summary>Tag for checks that answer "is this process alive?".</summary>
    public const string LiveTag = "live";

    /// <summary>Tag for checks that answer "can this service do useful work right now?".</summary>
    public const string ReadyTag = "ready";

    public static WebApplicationBuilder AddMagpieDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        // 1. LOGGING
        // On your laptop: short, human-friendly lines.
        // Everywhere else: one JSON object per line, which log tools can search and filter.
        builder.Logging.ClearProviders();
        if (builder.Environment.IsDevelopment())
        {
            builder.Logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        }
        else
        {
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            });
        }

        // 2. ONE ERROR FORMAT (Problem Details, RFC 9457)
        // Every error response from every service gets the same two extra fields.
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["service"] = serviceName;
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            };
        });

        // The safety net for any exception nobody else handled.
        // Services may register more specific handlers BEFORE calling AddMagpieDefaults.
        builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

        // 3. HEALTH CHECKS
        // "self" always passes: if the process can answer at all, it is alive.
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag]);

        return builder;
    }

    /// <summary>
    /// For services that sit BEHIND the gateway. The gateway is the one that talks
    /// to the browser, so only it knows the reader's real network address and the
    /// address they typed. It passes these on in "X-Forwarded-..." headers. This
    /// tells the service to believe them.
    ///
    /// NEVER call this in the gateway itself: there the caller is the open
    /// internet, and anyone could type those headers by hand.
    /// </summary>
    public static WebApplicationBuilder TrustGatewayForwardedHeaders(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

            // By default only a proxy on the same machine is believed. In Docker
            // the gateway has its own address, so we accept any sender. That is
            // safe only while the service can be reached through the gateway alone.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return builder;
    }

    /// <summary>Adds the error-handling middleware. Call this first, right after Build().</summary>
    public static WebApplication UseMagpieDefaults(this WebApplication app)
    {
        // Catches exceptions and hands them to the registered IExceptionHandler classes.
        app.UseExceptionHandler();

        // Gives a Problem Details body to error responses that have none (for example a bare 404).
        app.UseStatusCodePages();

        return app;
    }

    public static WebApplication MapMagpieHealthEndpoints(this WebApplication app)
    {
        // Liveness: only the "self" check. It must NOT depend on the database,
        // otherwise a database outage would make the platform restart healthy processes.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
            ResponseWriter = WriteHealthResponse,
        });

        // Readiness: every registered check, including dependencies such as PostgreSQL.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = WriteHealthResponse,
        });

        return app;
    }

    private static readonly JsonSerializerOptions HealthJsonOptions = new(JsonSerializerDefaults.Web);

    // The built-in writer only prints the word "Healthy" or "Unhealthy".
    // This one also lists each check, so you can see WHICH dependency failed.
    // It deliberately leaves out exception messages: health endpoints are easy to reach.
    private static Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
            }),
        };

        return context.Response.WriteAsJsonAsync(body, HealthJsonOptions);
    }
}
