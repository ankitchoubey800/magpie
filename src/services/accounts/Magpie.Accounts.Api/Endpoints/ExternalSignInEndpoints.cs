using System.Security.Claims;
using Magpie.Accounts.Api.Security;
using Magpie.Accounts.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Magpie.Accounts.Api.Endpoints;

public static class ExternalSignInEndpoints
{
    private const string CallbackPath = "/api/accounts/external/callback";

    public static IEndpointRouteBuilder MapExternalSignInEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounts/external").AddEndpointFilter<AntiforgeryFilter>();

        // Which outside providers are switched on? The sign-in page shows a
        // button for each. An empty list means "password only".
        group.MapGet("/providers", async (IAuthenticationSchemeProvider schemes) =>
        {
            var all = await schemes.GetAllSchemesAsync();
            return TypedResults.Ok(all
                .Where(scheme => scheme.Name == GoogleSignIn.ProviderName)
                .Select(scheme => scheme.Name)
                .ToArray());
        });

        // Step 1. The browser NAVIGATES here (a normal link, not fetch). We answer
        // with a redirect to the provider's own sign-in page.
        group.MapGet("/{provider}/start", async (
            string provider, string? returnUrl, IAuthenticationSchemeProvider schemes) =>
        {
            if (provider != GoogleSignIn.ProviderName || await schemes.GetSchemeAsync(provider) is null)
            {
                return Results.NotFound();
            }

            var properties = new AuthenticationProperties
            {
                // Where the provider's handler sends the browser when it is done.
                RedirectUri = $"{CallbackPath}?returnUrl={Uri.EscapeDataString(SafeLocalPath(returnUrl))}",
                // Identity looks for this entry to learn which provider answered.
                Items = { ["LoginProvider"] = provider },
            };

            return Results.Challenge(properties, [provider]);
        });

        // Step 2. The provider has vouched for the visitor. Decide what that means
        // for us, then send the browser to a page of the React app.
        group.MapGet("/callback", async (string? returnUrl, IExternalSignIn externalSignIn, CancellationToken cancellationToken) =>
        {
            var outcome = await externalSignIn.CompleteAsync(cancellationToken);

            return outcome switch
            {
                ExternalSignInOutcome.SignedIn or ExternalSignInOutcome.AccountCreated =>
                    Results.LocalRedirect(SafeLocalPath(returnUrl)),
                ExternalSignInOutcome.NeedsPasswordToLink =>
                    Results.LocalRedirect("/sign-in?link=pending"),
                _ => Results.LocalRedirect("/sign-in?error=external"),
            };
        });

        // Step 3 (only for the "needs password" case). The reader has now signed
        // in with their password, which proves the existing account is theirs.
        group.MapPost("/link", async (ClaimsPrincipal user, IExternalSignIn externalSignIn, CancellationToken cancellationToken) =>
        {
            var linked = await externalSignIn.LinkPendingToReaderAsync(
                user.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);

            return linked
                ? Results.NoContent()
                : Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Could not link the account",
                    detail: "The link request expired or does not match this account. Start again from the sign-in page.");
        }).RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Only ever redirect to a path on OUR site. Without this check, an attacker
    /// could send people a real Magpie link that ends up on a fake look-alike
    /// site after sign-in (an "open redirect").
    /// </summary>
    private static string SafeLocalPath(string? path) =>
        !string.IsNullOrEmpty(path) && path.StartsWith('/') && !path.StartsWith("//") && !path.StartsWith("/\\")
            ? path
            : "/";
}
