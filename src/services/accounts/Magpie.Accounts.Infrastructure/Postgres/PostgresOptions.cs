using System.ComponentModel.DataAnnotations;

namespace Magpie.Accounts.Infrastructure.Postgres;

/// <summary>Typed settings for the database, filled from "ConnectionStrings" in configuration.</summary>
public sealed class PostgresOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(ErrorMessage =
        "Connection string 'AccountsDb' is missing. Set it with user-secrets (local run) " +
        "or the ConnectionStrings__AccountsDb environment variable (containers).")]
    public string? AccountsDb { get; set; }
}
