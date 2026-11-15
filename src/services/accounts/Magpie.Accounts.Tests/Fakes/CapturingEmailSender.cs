using System.Collections.Concurrent;
using Magpie.Accounts.Application.Abstractions;

namespace Magpie.Accounts.Tests.Fakes;

/// <summary>
/// Stands in for the mail server. Instead of sending, it keeps every message
/// so a test can read it, exactly as you would read your inbox.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    public IReadOnlyList<EmailMessage> SentTo(string email) =>
        [.. _sent.Where(message => string.Equals(message.To, email, StringComparison.OrdinalIgnoreCase))];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
