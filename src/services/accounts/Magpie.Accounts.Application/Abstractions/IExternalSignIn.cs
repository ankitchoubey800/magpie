namespace Magpie.Accounts.Application.Abstractions;

/// <summary>
/// The port for "Continue with Google" (and any later outside sign-in provider).
/// By the time these methods run, the provider has already told us who the
/// visitor is; that answer is waiting in a short-lived temporary cookie.
/// </summary>
public interface IExternalSignIn
{
    /// <summary>The claim in which the provider says "we checked that this email belongs to this person".</summary>
    public const string EmailVerifiedClaim = "email_verified";

    /// <summary>Finishes the sign-in that the provider just vouched for.</summary>
    Task<ExternalSignInOutcome> CompleteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Connects the waiting outside identity to the reader who has just proven,
    /// with their password, that the existing account is theirs.
    /// </summary>
    Task<bool> LinkPendingToReaderAsync(string readerId, CancellationToken cancellationToken);
}

public enum ExternalSignInOutcome
{
    /// <summary>This outside identity was already linked to an account. Signed in.</summary>
    SignedIn,

    /// <summary>First visit, and nobody has this email yet. Account created and signed in.</summary>
    AccountCreated,

    /// <summary>
    /// An account with this email already exists and is not linked. We do NOT
    /// sign in or link. The reader must first prove they own the existing account.
    /// </summary>
    NeedsPasswordToLink,

    /// <summary>The provider gave us nothing usable (no verified email, cancelled, expired).</summary>
    Failed,
}
