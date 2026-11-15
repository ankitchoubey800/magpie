using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Application.Registration;

/// <summary>What the caller is allowed to learn about a registration attempt.</summary>
public sealed record RegisterOutcome(bool Accepted, IReadOnlyList<string> Errors)
{
    public static readonly RegisterOutcome CheckYourEmail = new(true, []);

    public static RegisterOutcome Invalid(params IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>
/// Use case: a visitor asks for an account.
///
/// The important design rule: the answer NEVER reveals whether the email was
/// already registered. In both cases the caller hears "check your email", and
/// the difference is explained privately, in the inbox of the address's owner.
/// </summary>
public sealed class RegisterReaderHandler(IReaderAccounts accounts, IEmailSender emailSender, AccountLinks links)
{
    public async Task<RegisterOutcome> HandleAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(email, out var address))
        {
            return RegisterOutcome.Invalid("Enter a valid email address.");
        }

        if (string.IsNullOrEmpty(password))
        {
            return RegisterOutcome.Invalid("Enter a password.");
        }

        var result = await accounts.CreateAsync(address!, password, cancellationToken);

        switch (result.Status)
        {
            case CreateAccountStatus.Rejected:
                // Password rules failed. Safe to say so: it reveals nothing about other people.
                return RegisterOutcome.Invalid(result.Errors);

            case CreateAccountStatus.Created:
                await SendConfirmationAsync(address!, result.Confirmation!, cancellationToken);
                break;

            case CreateAccountStatus.AlreadyExists:
                var pending = await accounts.GetPendingConfirmationAsync(address!, cancellationToken);
                if (pending is not null)
                {
                    // Registered earlier but never confirmed: simply send the link again.
                    await SendConfirmationAsync(address!, pending, cancellationToken);
                }
                else
                {
                    await emailSender.SendAsync(
                        AccountEmails.AlreadyRegistered(address!, links.SignIn()), cancellationToken);
                }

                break;
        }

        return RegisterOutcome.CheckYourEmail;
    }

    private Task SendConfirmationAsync(EmailAddress to, PendingConfirmation confirmation, CancellationToken cancellationToken) =>
        emailSender.SendAsync(AccountEmails.ConfirmYourEmail(to, links.ConfirmEmail(confirmation)), cancellationToken);
}
