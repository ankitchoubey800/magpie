using Magpie.Accounts.Domain;
using Magpie.Accounts.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
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
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<ReaderInterest> ReaderInterests => Set<ReaderInterest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Friendlier table names than the "AspNetUsers" defaults.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<Category>(category =>
        {
            category.ToTable("categories");
            category.HasKey(c => c.Id);
            category.Property(c => c.Id).ValueGeneratedNever(); // we choose the ids ourselves
            category.Property(c => c.Slug).HasMaxLength(64);
            category.Property(c => c.Name).HasMaxLength(64);
            category.HasIndex(c => c.Slug).IsUnique();

            // A topic points at its group. Restrict: a group that still has topics cannot be deleted.
            category.HasOne<Category>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);

            // The starter list travels with the schema: it is part of the migration.
            category.HasData(StarterCategories.All.Select(c => new
            {
                c.Id, c.Slug, c.Name, c.ParentId, c.SortOrder,
            }));
        });

        builder.Entity<ReaderInterest>(interest =>
        {
            interest.ToTable("user_interests");

            // One row per (reader, topic) pair. Making the PAIR the key means the
            // database itself refuses to store the same choice twice.
            interest.HasKey(i => new { i.ReaderId, i.CategoryId });

            // Deleting a reader deletes their choices with them.
            interest.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.ReaderId).OnDelete(DeleteBehavior.Cascade);

            // A topic that readers have chosen cannot be deleted by accident.
            interest.HasOne<Category>().WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Restrict);

            // The key above already makes "all topics of reader X" fast.
            // THIS index makes the opposite question fast: "all readers of topic X".
            interest.HasIndex(i => i.CategoryId).HasDatabaseName("ix_user_interests_category");
        });
    }
}
