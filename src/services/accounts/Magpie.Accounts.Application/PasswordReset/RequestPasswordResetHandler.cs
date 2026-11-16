using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Application.Registration;
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Application.PasswordReset;

/// <summary>
/// Use case: "I forgot my password." Like registration, it never reveals
/// whether the email has an account: it returns nothing either way.
/// </summary>
public sealed class RequestPasswordResetHandler(IReaderAccounts accounts, IEmailSender emailSender, AccountLinks links)
{
    public async Task HandleAsync(string? email, CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(email, out var address))
        {
            return;
        }

        var ticket = await accounts.CreatePasswordResetAsync(address!, cancellationToken);
        if (ticket is null)
        {
            // No confirmed account with this email. Say nothing, send nothing.
            return;
        }

        await emailSender.SendAsync(
            AccountEmails.ResetYourPassword(address!, links.ResetPassword(ticket)), cancellationToken);
    }
}
