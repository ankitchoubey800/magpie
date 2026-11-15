using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Application.Registration;

/// <summary>
/// Use case: "I did not get the email, send it again."
/// It returns nothing, on purpose: whether or not an email was sent would
/// reveal whether the address has an account.
/// </summary>
public sealed class ResendConfirmationHandler(IReaderAccounts accounts, IEmailSender emailSender, AccountLinks links)
{
    public async Task HandleAsync(string? email, CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(email, out var address))
        {
            return;
        }

        var pending = await accounts.GetPendingConfirmationAsync(address!, cancellationToken);
        if (pending is null)
        {
            // No such account, or already confirmed. Say nothing, send nothing.
            return;
        }

        await emailSender.SendAsync(
            AccountEmails.ConfirmYourEmail(address!, links.ConfirmEmail(pending)), cancellationToken);
    }
}
