using System.Text;
using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Magpie.Accounts.Infrastructure.Identity;

/// <summary>
/// The adapter behind the IReaderAccounts port, built on ASP.NET Core Identity.
/// UserManager stores and checks accounts; SignInManager writes the session cookie.
/// </summary>
internal sealed class IdentityReaderAccounts(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    TimeProvider timeProvider) : IReaderAccounts
{
    public async Task<CreateAccountResult> CreateAsync(
        EmailAddress email, string password, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email.Value,
            Email = email.Value,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

        // CreateAsync hashes the password. The plain password is never stored and never logged.
        var result = await users.CreateAsync(user, password);
        if (result.Succeeded)
        {
            return CreateAccountResult.Created(await CreateConfirmationAsync(user));
        }

        var isDuplicate = result.Errors.Any(error =>
            error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName));

        return isDuplicate
            ? CreateAccountResult.AlreadyExists()
            : CreateAccountResult.Rejected(result.Errors.Select(error => error.Description).ToList());
    }

    public async Task<PendingConfirmation?> GetPendingConfirmationAsync(
        EmailAddress email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Value);
        if (user is null || user.EmailConfirmed)
        {
            return null;
        }

        return await CreateConfirmationAsync(user);
    }

    public async Task<bool> ConfirmEmailAsync(string readerId, string token, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(readerId);
        if (user is null)
        {
            return false;
        }

        if (!TryFromLinkSafe(token, out var rawToken))
        {
            // Someone edited the link. Treat it like any other invalid token.
            return false;
        }

        var result = await users.ConfirmEmailAsync(user, rawToken);
        return result.Succeeded;
    }

    public async Task<bool> SignInAsync(EmailAddress email, string password, CancellationToken cancellationToken)
    {
        // isPersistent: the cookie survives closing the browser, until it expires.
        // lockoutOnFailure: a wrong password counts towards a temporary lockout.
        var result = await signIn.PasswordSignInAsync(
            email.Value, password, isPersistent: true, lockoutOnFailure: true);

        return result.Succeeded;
    }

    public Task SignOutAsync(CancellationToken cancellationToken) => signIn.SignOutAsync();

    public async Task<PasswordResetTicket?> CreatePasswordResetAsync(
        EmailAddress email, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Value);
        if (user is null || !user.EmailConfirmed)
        {
            return null;
        }

        var rawToken = await users.GeneratePasswordResetTokenAsync(user);
        return new PasswordResetTicket(user.Id.ToString(), ToLinkSafe(rawToken));
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(
        string readerId, string token, string newPassword, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(readerId);
        if (user is null || !TryFromLinkSafe(token, out var rawToken))
        {
            return ResetPasswordResult.InvalidLink;
        }

        // On success Identity also changes the account's "security stamp". That one
        // change does two jobs: this token can never be used again, and every
        // session cookie issued before now stops being accepted.
        var result = await users.ResetPasswordAsync(user, rawToken, newPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? ResetPasswordResult.InvalidLink
                : ResetPasswordResult.Rejected(result.Errors.Select(error => error.Description).ToList());
        }

        // The reader has proven they own the mailbox, so lift any lockout caused
        // by earlier wrong guesses (their own or an attacker's).
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);

        return ResetPasswordResult.Done;
    }

    private async Task<PendingConfirmation> CreateConfirmationAsync(ApplicationUser user)
    {
        var rawToken = await users.GenerateEmailConfirmationTokenAsync(user);
        return new PendingConfirmation(user.Id.ToString(), ToLinkSafe(rawToken));
    }

    // A raw token contains characters such as "+" and "/" that get mangled
    // inside a web address, so we re-encode it in a link-safe alphabet.
    private static string ToLinkSafe(string rawToken) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

    private static bool TryFromLinkSafe(string linkSafeToken, out string rawToken)
    {
        try
        {
            rawToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(linkSafeToken));
            return true;
        }
        catch (FormatException)
        {
            rawToken = "";
            return false;
        }
    }
}
