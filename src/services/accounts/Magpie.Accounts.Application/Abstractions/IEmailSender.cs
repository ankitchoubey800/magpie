namespace Magpie.Accounts.Application.Abstractions;

/// <summary>
/// The port for sending email. On the laptop the adapter talks to Mailpit;
/// in the cloud (week 15) it will talk to Amazon SES. Use cases never know.
/// </summary>
public interface IEmailSender
{
    /// <exception cref="DependencyUnavailableException">The mail server could not be reached.</exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string TextBody);
