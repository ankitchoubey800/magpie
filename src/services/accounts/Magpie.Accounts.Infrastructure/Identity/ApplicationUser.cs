using Microsoft.AspNetCore.Identity;

namespace Magpie.Accounts.Infrastructure.Identity;

/// <summary>
/// One row in the users table. IdentityUser already defines the columns that
/// sign-in needs (email, password hash, lockout counters and so on); we add
/// only what is ours. Guid means the id is a random 128-bit value, so ids
/// cannot be guessed or counted the way 1, 2, 3 can.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAtUtc { get; set; }
}
