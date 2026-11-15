using Magpie.Accounts.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Magpie.Accounts.Infrastructure.Persistence;

/// <summary>
/// EF Core's view of the Accounts database. A DbContext is one short "work
/// session" with the database: it tracks the objects you load and writes your
/// changes back in one go.
///
/// IdentityUserContext brings the table definitions for users (and their
/// tokens, claims and outside logins) without the roles tables, which we do
/// not use.
/// </summary>
public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Friendlier table names than the "AspNetUsers" defaults.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
