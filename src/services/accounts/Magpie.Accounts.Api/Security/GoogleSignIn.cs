using Magpie.Accounts.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Magpie.Accounts.Api.Security;

public static class GoogleSignIn
{
    public const string ProviderName = "Google";

    /// <summary>
    /// The address Google sends the browser back to. It lives under
    /// /api/accounts so that the gateway's existing route already covers it.
    /// You register exactly this address (with your site in front) at Google.
    /// </summary>
    public const string CallbackPath = "/api/accounts/external/google/signin";

    /// <summary>
    /// Switches on "Continue with Google" only when a client id is configured.
    /// This is the ONE place where we read configuration while starting up
    /// instead of through an options class, because the question is not "what
    /// value?" but "does this feature exist at all?".
    /// </summary>
    public static AuthenticationBuilder AddGoogleIfConfigured(
        this AuthenticationBuilder authentication, IConfiguration configuration)
    {
        var section = configuration.GetSection("Authentication:Google");
        var clientId = section["ClientId"];
        var clientSecret = section["ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return authentication;
        }

        return authentication.AddGoogle(ProviderName, options =>
        {
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.CallbackPath = CallbackPath;

            // Google's answer is parked in Identity's short-lived "external"
            // cookie. It is NOT a session: our own code decides what happens next.
            options.SignInScheme = IdentityConstants.ExternalScheme;

            // Keep Google's statement that it has verified the email address.
            options.ClaimActions.MapJsonKey(
                IExternalSignIn.EmailVerifiedClaim, "email_verified", "boolean");

            // The little cookie that ties "we sent you to Google" to "Google sent
            // you back". Coming back is a normal page navigation, so Lax is
            // enough, and (unlike the default) it also works on http://localhost.
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        });
    }
}
