using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Application.Abstractions;

/// <summary>
/// The port for everything that touches stored accounts and sign-in sessions.
/// The use cases say WHAT they need; Infrastructure implements it with
/// ASP.NET Core Identity. No use case ever sees a password hash.
/// </summary>
public interface IReaderAccounts
{
    /// <summary>Creates an unconfirmed account, or explains why it could not.</summary>
    Task<CreateAccountResult> CreateAsync(EmailAddress email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a fresh confirmation token if an account with this email exists
    /// and is still unconfirmed; otherwise null.
    /// </summary>
    Task<PendingConfirmation?> GetPendingConfirmationAsync(EmailAddress email, CancellationToken cancellationToken);

    /// <summary>True when the token was valid and the email is now confirmed.</summary>
    Task<bool> ConfirmEmailAsync(string readerId, string token, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the password and, if everything is in order, starts a session.
    /// Returns only true or false ON PURPOSE: the caller must not be able to
    /// tell "wrong password" from "no such account" from "locked out".
    /// </summary>
    Task<bool> SignInAsync(EmailAddress email, string password, CancellationToken cancellationToken);

    Task SignOutAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns a fresh reset token if a CONFIRMED account with this email exists;
    /// otherwise null.
    /// </summary>
    Task<PasswordResetTicket?> CreatePasswordResetAsync(EmailAddress email, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a new password if the token is valid. A successful reset also makes
    /// the token unusable and ends every existing session of that reader.
    /// </summary>
    Task<ResetPasswordResult> ResetPasswordAsync(
        string readerId, string token, string newPassword, CancellationToken cancellationToken);
}

/// <summary>What a reader needs in order to choose a new password.</summary>
public sealed record PasswordResetTicket(string ReaderId, string Token);

public enum ResetPasswordStatus
{
    Done,
    InvalidLink,
    RejectedPassword,
}

public sealed record ResetPasswordResult(ResetPasswordStatus Status, IReadOnlyList<string> Errors)
{
    public static readonly ResetPasswordResult Done = new(ResetPasswordStatus.Done, []);

    public static readonly ResetPasswordResult InvalidLink = new(ResetPasswordStatus.InvalidLink, []);

    public static ResetPasswordResult Rejected(IReadOnlyList<string> errors) =>
        new(ResetPasswordStatus.RejectedPassword, errors);
}

/// <summary>What a reader needs in order to confirm their email address.</summary>
public sealed record PendingConfirmation(string ReaderId, string Token);

public enum CreateAccountStatus
{
    Created,
    AlreadyExists,
    Rejected,
}

public sealed record CreateAccountResult(
    CreateAccountStatus Status,
    PendingConfirmation? Confirmation,
    IReadOnlyList<string> Errors)
{
    public static CreateAccountResult Created(PendingConfirmation confirmation) =>
        new(CreateAccountStatus.Created, confirmation, []);

    public static CreateAccountResult AlreadyExists() =>
        new(CreateAccountStatus.AlreadyExists, null, []);

    public static CreateAccountResult Rejected(IReadOnlyList<string> errors) =>
        new(CreateAccountStatus.Rejected, null, errors);
}
