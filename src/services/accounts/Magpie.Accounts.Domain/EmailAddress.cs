namespace Magpie.Accounts.Domain;

/// <summary>
/// An email address that has passed our basic checks. Code that receives an
/// EmailAddress never has to wonder "has anyone validated this string?".
///
/// The checks are deliberately light. The only real proof that an address
/// works is that its owner clicks the link we send to it.
/// </summary>
public sealed record EmailAddress
{
    /// <summary>The longest address the email standards allow.</summary>
    public const int MaxLength = 254;

    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static bool TryCreate(string? input, out EmailAddress? email)
    {
        email = null;

        var value = input?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        // Exactly one "@", with something on both sides, a dot in the part
        // after it, and no spaces anywhere.
        var at = value.IndexOf('@');
        var hasOneAt = at > 0 && at == value.LastIndexOf('@') && at < value.Length - 1;
        if (!hasOneAt || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var domain = value[(at + 1)..];
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.'))
        {
            return false;
        }

        email = new EmailAddress(value);
        return true;
    }

    public override string ToString() => Value;
}
