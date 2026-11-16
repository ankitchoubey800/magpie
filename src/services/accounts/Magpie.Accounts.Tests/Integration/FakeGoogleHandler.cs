using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Magpie.Accounts.Tests.Integration;

/// <summary>
/// Pretends to be Google. The real handler sends the browser to Google and
/// waits for it to come back. This one skips the trip: it reads "who Google
/// says you are" from the test's own query string and parks it in the same
/// temporary cookie the real handler would use. Everything AFTER that point
/// (our linking rules) is the real code.
/// </summary>
public sealed class FakeGoogleHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Google";

    /// <summary>Builds the address a test "clicks" to sign in as a given Google user.</summary>
    public static string StartUrl(string googleId, string email, bool emailVerified = true, string returnUrl = "/welcome") =>
        $"/api/accounts/external/Google/start?returnUrl={Uri.EscapeDataString(returnUrl)}" +
        $"&fake_sub={Uri.EscapeDataString(googleId)}&fake_email={Uri.EscapeDataString(email)}&fake_verified={emailVerified}";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Request.Query["fake_sub"].ToString()),
            new Claim(ClaimTypes.Email, Request.Query["fake_email"].ToString()),
            new Claim("email_verified", Request.Query["fake_verified"].ToString()),
        ], Scheme.Name);

        await Context.SignInAsync(IdentityConstants.ExternalScheme, new ClaimsPrincipal(identity), properties);
        Response.Redirect(properties.RedirectUri!);
    }
}
