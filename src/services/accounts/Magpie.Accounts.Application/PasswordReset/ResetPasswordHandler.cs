using Magpie.Accounts.Application.Abstractions;

namespace Magpie.Accounts.Application.PasswordReset;

/// <summary>Use case: the reader opened the reset link and typed a new password.</summary>
public sealed class ResetPasswordHandler(IReaderAccounts accounts)
{
    public Task<ResetPasswordResult> HandleAsync(
        string? readerId, string? token, string? newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(readerId) || string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(ResetPasswordResult.InvalidLink);
        }

        if (string.IsNullOrEmpty(newPassword))
        {
            return Task.FromResult(ResetPasswordResult.Rejected(["Enter a new password."]));
        }

        return accounts.ResetPasswordAsync(readerId, token, newPassword, cancellationToken);
    }
}
