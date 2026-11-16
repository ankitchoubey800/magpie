using System.Security.Claims;

namespace Magpie.Accounts.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in reader's id, read from the session cookie. This is the ONLY
    /// place a reader id may come from when an endpoint acts "on my own data".
    /// </summary>
    public static Guid ReaderId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("This endpoint requires a signed-in reader."));
}
