namespace Magpie.Accounts.Application.Abstractions;

/// <summary>
/// Builds the links we put in emails. The site address comes from OUR
/// configuration, never from the incoming request: an attacker can put any
/// host name they like in a request, and we must not email it to our readers.
/// </summary>
public sealed class AccountLinks(Uri publicBaseUrl)
{
    public Uri ConfirmEmail(PendingConfirmation confirmation)
    {
        var query =
            $"readerId={Uri.EscapeDataString(confirmation.ReaderId)}&token={Uri.EscapeDataString(confirmation.Token)}";

        return new Uri(publicBaseUrl, $"/confirm-email?{query}");
    }

    public Uri ResetPassword(PasswordResetTicket ticket)
    {
        var query =
            $"readerId={Uri.EscapeDataString(ticket.ReaderId)}&token={Uri.EscapeDataString(ticket.Token)}";

        return new Uri(publicBaseUrl, $"/reset-password?{query}");
    }

    public Uri SignIn() => new(publicBaseUrl, "/sign-in");
}
