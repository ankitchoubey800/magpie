using System.ComponentModel.DataAnnotations;

namespace Magpie.Accounts.Infrastructure.Email;

/// <summary>Typed settings for outgoing email, filled from the "Email" section.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string Host { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; } = 1025;

    [Required, EmailAddress]
    public string FromAddress { get; set; } = "";

    [Required]
    public string FromName { get; set; } = "Magpie";
}
