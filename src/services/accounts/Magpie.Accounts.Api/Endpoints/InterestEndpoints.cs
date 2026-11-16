using System.Security.Claims;
using Magpie.Accounts.Api.Security;
using Magpie.Accounts.Application.Interests;
using Magpie.Mediator;

namespace Magpie.Accounts.Api.Endpoints;

public sealed record SetInterestsRequest(IReadOnlyList<int>? CategoryIds);

public static class InterestEndpoints
{
    public static IEndpointRouteBuilder MapInterestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounts").AddEndpointFilter<AntiforgeryFilter>();

        // The list of topics is the same for everyone and is not a secret.
        group.MapGet("/categories", async (IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.SendAsync(new GetCategoriesQuery(), cancellationToken)));

        // "MY interests". There is deliberately no reader id in the address.
        // The id comes from the session cookie, so one reader cannot even ASK
        // for another reader's interests. The safest permission check is the
        // one you never need to write.
        group.MapGet("/me/interests", async (ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.SendAsync(new GetMyInterestsQuery(user.ReaderId()), cancellationToken)))
            .RequireAuthorization();

        // PUT = "replace the whole set with this one". Sending the same request
        // twice leaves the same result, which makes retries harmless.
        group.MapPut("/me/interests", async (
                SetInterestsRequest request, ClaimsPrincipal user, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.SendAsync(
                    new SetMyInterestsCommand(user.ReaderId(), request.CategoryIds), cancellationToken);

                return result.Saved
                    ? Results.NoContent()
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["categoryIds"] = [result.Error!] });
            })
            .RequireAuthorization();

        return app;
    }
}
