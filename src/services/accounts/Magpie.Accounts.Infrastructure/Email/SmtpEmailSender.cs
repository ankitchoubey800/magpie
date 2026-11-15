using System.Net.Mail;
using System.Net.Sockets;
using Magpie.Accounts.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Magpie.Accounts.Infrastructure.Email;

/// <summary>
/// The laptop adapter for IEmailSender: hands the message to an SMTP server
/// (Mailpit). No sign-in and no encryption, which is right for a mail catcher
/// on your own machine and wrong for anything else. The cloud adapter
/// (Amazon SES) replaces this class in week 15.
/// </summary>
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false,
        };
        mail.To.Add(message.To);

        try
        {
            using var client = new SmtpClient(settings.Host, settings.Port) { Timeout = 5000 };
            await client.SendMailAsync(mail, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or SocketException or TimeoutException)
        {
            throw new DependencyUnavailableException("email", exception);
        }

        // Log THAT an email went out and its subject. Never the address or the
        // body: the body contains a link that signs someone up.
        logger.LogInformation("Sent email with subject {Subject}", message.Subject);
    }
}
