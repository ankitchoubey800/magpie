using Magpie.Accounts.Application.Abstractions;

namespace Magpie.Accounts.Application.Registration;

/// <summary>Use case: the reader clicked the link in the confirmation email.</summary>
public sealed class ConfirmEmailHandler(IReaderAccounts accounts)
{
    public Task<bool> HandleAsync(string? readerId, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(readerId) || string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(false);
        }

        return accounts.ConfirmEmailAsync(readerId, token, cancellationToken);
    }
}
