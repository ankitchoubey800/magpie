using Magpie.Accounts.Application.Ping;

namespace Magpie.Accounts.Api.Endpoints;

public static class PingEndpoints
{
    public static IEndpointRouteBuilder MapPingEndpoints(this IEndpointRouteBuilder app)
    {
        // The endpoint is deliberately thin: receive the request, call ONE use
        // case, return its result. ASP.NET Core supplies both parameters:
        // the handler from the container, the token from the request.
        app.MapGet("/api/accounts/ping", async (GetPingHandler handler, CancellationToken cancellationToken) =>
            TypedResults.Ok(await handler.HandleAsync(cancellationToken)));

        return app;
    }
}
