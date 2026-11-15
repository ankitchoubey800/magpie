using Magpie.Accounts.Api.Options;
using Magpie.Accounts.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Magpie.Accounts.Api.Security;

public static class SecuritySetup
{
    public const string SessionCookieName = "magpie.session";
    public const string AntiforgeryCookieName = "magpie.xsrf";
    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

    public static WebApplicationBuilder AddAccountsSecurity(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        // --- Settings (Options pattern) ---
        services.AddOptions<AccountsOptions>()
            .BindConfiguration(AccountsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(serviceProvider => new AccountLinks(
            new Uri(serviceProvider.GetRequiredService<IOptions<AccountsOptions>>().Value.PublicBaseUrl)));

        // --- Who are you? (authentication with a session cookie) ---
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<AccountsOptions>>((cookie, accounts) =>
            {
                cookie.Cookie.Name = SessionCookieName;

                // The three safety flags:
                cookie.Cookie.HttpOnly = true;                           // scripts cannot read it
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // only sent over HTTPS (browsers also allow localhost)
                cookie.Cookie.SameSite = SameSiteMode.Lax;               // not sent on requests started by other sites, except plain link clicks

                cookie.ExpireTimeSpan = TimeSpan.FromDays(accounts.Value.SessionLifetimeDays);
                cookie.SlidingExpiration = true; // visiting regularly keeps you signed in

                // By default cookie sign-in REDIRECTS to a login page. That suits
                // server-rendered sites. An API must answer with a status code
                // and let the React app decide what to show.
                cookie.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        // --- What may you do? (authorization) ---
        services.AddAuthorization();

        // --- Was this request really started by OUR website? (anti-forgery) ---
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;

            // "Secure when the request is HTTPS". Unlike the session cookie this
            // cannot be Always: the anti-forgery system refuses to work at all
            // on a plain-HTTP request when it is, and on the laptop the gateway
            // talks to this service over HTTP. (Revisited when HTTPS arrives in
            // the cloud weeks; see the chapter's "carried forward" list.)
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        // --- The keys that encrypt cookies and sign tokens (data protection) ---
        // The application name must be identical in every copy of this service,
        // so that a cookie written by one copy can be read by another.
        services.AddDataProtection().SetApplicationName("magpie-accounts");

        services.AddOptions<KeyManagementOptions>()
            .Configure<IOptions<AccountsOptions>>((keys, accounts) =>
            {
                var folder = accounts.Value.DataProtectionKeysPath;
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    keys.XmlRepository = new FileSystemXmlRepository(
                        new DirectoryInfo(folder), NullLoggerFactory.Instance);
                }
            });

        return builder;
    }
}
