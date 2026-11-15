using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Application.SignIn;

/// <summary>
/// Use case: sign in with email and password.
/// One answer for every kind of failure, so an attacker learns nothing from it.
/// </summary>
public sealed class SignInHandler(IReaderAccounts accounts)
{
    public async Task<bool> HandleAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        if (!EmailAddress.TryCreate(email, out var address) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        return await accounts.SignInAsync(address!, password, cancellationToken);
    }
}
