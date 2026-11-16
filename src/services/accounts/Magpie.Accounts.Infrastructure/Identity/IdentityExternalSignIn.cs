using System.Security.Claims;
using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Magpie.Accounts.Infrastructure.Identity;

/// <summary>
/// The adapter behind IExternalSignIn. The most important rule in this file:
/// an outside identity is NEVER attached to an existing account just because
/// the email addresses match.
/// </summary>
internal sealed class IdentityExternalSignIn(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider) : IExternalSignIn
{
    public async Task<ExternalSignInOutcome> CompleteAsync(CancellationToken cancellationToken)
    {
        // Reads the temporary cookie that the provider's sign-in left behind.
        var info = await signIn.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return ExternalSignInOutcome.Failed;
        }

        // Case 1: we have seen this exact outside identity before.
        var known = await signIn.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
        if (known.Succeeded)
        {
            await ForgetPendingAsync();
            return ExternalSignInOutcome.SignedIn;
        }

        // We only ever act on an email the provider says it has verified.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var verified = info.Principal.FindFirstValue(IExternalSignIn.EmailVerifiedClaim);
        if (known.IsLockedOut
            || !EmailAddress.TryCreate(email, out var address)
            || !string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase))
        {
            await ForgetPendingAsync();
            return ExternalSignInOutcome.Failed;
        }

        // Case 2: an account with this email exists but is not linked.
        // Keep the temporary cookie: the linking step needs it.
        if (await users.FindByEmailAsync(address!.Value) is not null)
        {
            return ExternalSignInOutcome.NeedsPasswordToLink;
        }

        // Case 3: a brand-new reader. No password, and the email counts as
        // confirmed because the provider has verified it.
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = address.Value,
            Email = address.Value,
            EmailConfirmed = true,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

        if (!(await users.CreateAsync(user)).Succeeded || !(await users.AddLoginAsync(user, info)).Succeeded)
        {
            await ForgetPendingAsync();
            return ExternalSignInOutcome.Failed;
        }

        await signIn.SignInAsync(user, isPersistent: true, info.LoginProvider);
        await ForgetPendingAsync();
        return ExternalSignInOutcome.AccountCreated;
    }

    public async Task<bool> LinkPendingToReaderAsync(string readerId, CancellationToken cancellationToken)
    {
        var info = await signIn.GetExternalLoginInfoAsync();
        var user = await users.FindByIdAsync(readerId);
        if (info is null || user is null)
        {
            return false;
        }

        // The waiting identity must be for THIS account's email, not just any.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            await ForgetPendingAsync();
            return false;
        }

        var result = await users.AddLoginAsync(user, info);
        await ForgetPendingAsync();
        return result.Succeeded;
    }

    private Task ForgetPendingAsync() =>
        httpContextAccessor.HttpContext!.SignOutAsync(IdentityConstants.ExternalScheme);
}
