using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Tests.Fakes;

/// <summary>
/// An in-memory stand-in for the IReaderAccounts port. It keeps accounts in a
/// dictionary so use-case tests need no database and no Identity.
/// </summary>
public sealed class FakeReaderAccounts : IReaderAccounts
{
    private sealed record Account(string Id, string Password, bool Confirmed);

    private readonly Dictionary<string, Account> _byEmail = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Passwords shorter than this are rejected, like a real password rule.</summary>
    public int MinimumPasswordLength { get; set; } = 10;

    public bool Exists(string email) => _byEmail.ContainsKey(email);

    public void MarkConfirmed(string email) => _byEmail[email] = _byEmail[email] with { Confirmed = true };

    public Task<CreateAccountResult> CreateAsync(EmailAddress email, string password, CancellationToken cancellationToken)
    {
        if (password.Length < MinimumPasswordLength)
        {
            return Task.FromResult(CreateAccountResult.Rejected(["Password is too short."]));
        }

        if (_byEmail.ContainsKey(email.Value))
        {
            return Task.FromResult(CreateAccountResult.AlreadyExists());
        }

        var account = new Account(Guid.NewGuid().ToString(), password, Confirmed: false);
        _byEmail[email.Value] = account;
        return Task.FromResult(CreateAccountResult.Created(new PendingConfirmation(account.Id, "token-for-" + account.Id)));
    }

    public Task<PendingConfirmation?> GetPendingConfirmationAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        PendingConfirmation? pending =
            _byEmail.TryGetValue(email.Value, out var account) && !account.Confirmed
                ? new PendingConfirmation(account.Id, "token-for-" + account.Id)
                : null;

        return Task.FromResult(pending);
    }

    public Task<bool> ConfirmEmailAsync(string readerId, string token, CancellationToken cancellationToken)
    {
        var entry = _byEmail.FirstOrDefault(pair => pair.Value.Id == readerId);
        if (entry.Value is null || token != "token-for-" + readerId)
        {
            return Task.FromResult(false);
        }

        _byEmail[entry.Key] = entry.Value with { Confirmed = true };
        return Task.FromResult(true);
    }

    public Task<bool> SignInAsync(EmailAddress email, string password, CancellationToken cancellationToken) =>
        Task.FromResult(_byEmail.TryGetValue(email.Value, out var account) && account.Confirmed && account.Password == password);

    public Task SignOutAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<PasswordResetTicket?> CreatePasswordResetAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        PasswordResetTicket? ticket =
            _byEmail.TryGetValue(email.Value, out var account) && account.Confirmed
                ? new PasswordResetTicket(account.Id, "reset-for-" + account.Id)
                : null;

        return Task.FromResult(ticket);
    }

    public Task<ResetPasswordResult> ResetPasswordAsync(
        string readerId, string token, string newPassword, CancellationToken cancellationToken)
    {
        var entry = _byEmail.FirstOrDefault(pair => pair.Value.Id == readerId);
        if (entry.Value is null || token != "reset-for-" + readerId)
        {
            return Task.FromResult(ResetPasswordResult.InvalidLink);
        }

        if (newPassword.Length < MinimumPasswordLength)
        {
            return Task.FromResult(ResetPasswordResult.Rejected(["Password is too short."]));
        }

        _byEmail[entry.Key] = entry.Value with { Password = newPassword };
        return Task.FromResult(ResetPasswordResult.Done);
    }
}
