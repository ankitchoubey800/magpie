using System.ComponentModel.DataAnnotations;

namespace Magpie.Accounts.Api.Options;

/// <summary>
/// Typed settings for the Accounts service, filled from the "Accounts" section
/// of configuration and checked when the service starts. A wrong or missing
/// value stops the service immediately with a clear message, instead of
/// causing a strange failure on some request an hour later.
/// </summary>
public sealed class AccountsOptions
{
    public const string SectionName = "Accounts";

    /// <summary>The address readers type into their browser. Used for links in emails.</summary>
    [Required, Url]
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>How long a reader stays signed in without visiting.</summary>
    [Range(1, 90)]
    public int SessionLifetimeDays { get; set; } = 14;

    /// <summary>
    /// Folder where the keys that protect cookies and tokens are kept.
    /// Empty = keep them in the default per-user location (fine for "dotnet run").
    /// In a container this MUST point at a volume, or every restart signs everyone out.
    /// </summary>
    public string? DataProtectionKeysPath { get; set; }

    /// <summary>
    /// How many sign-up, sign-in and reset requests one network address may
    /// make per minute before it is told to slow down (429).
    /// </summary>
    [Range(1, 1_000_000)]
    public int AuthRequestsPerMinute { get; set; } = 10;

    /// <summary>
    /// How often a session cookie is re-checked against the account (minutes).
    /// 0 = on every request. This is how long a stolen or stale cookie can
    /// keep working after a password reset.
    /// </summary>
    [Range(0, 60)]
    public int SessionRecheckMinutes { get; set; } = 5;
}
