using Magpie.Collector.Domain;
using Microsoft.EntityFrameworkCore;

namespace Magpie.Collector.Infrastructure.Persistence;

/// <summary>
/// The collector's OWN database (magpie_collector). No other service reads or
/// writes these tables, and this service touches nobody else's.
/// </summary>
public sealed class CollectorDbContext(DbContextOptions<CollectorDbContext> options) : DbContext(options)
{
    public DbSet<Source> Sources => Set<Source>();

    public DbSet<CollectedItem> Items => Set<CollectedItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Source>(source =>
        {
            source.ToTable("sources");
            source.HasKey(s => s.Id);
            source.Property(s => s.Id).ValueGeneratedNever();
            source.Property(s => s.Name).HasMaxLength(200);
            source.Property(s => s.Address).HasMaxLength(2000);
            source.Property(s => s.CategoryHint).HasMaxLength(64);
            source.Property(s => s.LastError).HasMaxLength(500);
            source.Property(s => s.Type).HasConversion<string>().HasMaxLength(32); // stored as "Rss", readable in psql

            // The same address must not be collected twice.
            source.HasIndex(s => s.Address).IsUnique();

            // The scheduler's question, every tick: "enabled sources, soonest first".
            source.HasIndex(s => new { s.Enabled, s.NextFetchAtUtc }).HasDatabaseName("ix_sources_due");
        });

        builder.Entity<CollectedItem>(item =>
        {
            item.ToTable("collected_items");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedNever();
            item.Property(i => i.ExternalId).HasMaxLength(2000);
            item.Property(i => i.Url).HasMaxLength(2000);
            item.Property(i => i.Title).HasMaxLength(RawItem.MaxTitleLength);
            item.Property(i => i.Summary).HasMaxLength(RawItem.MaxSummaryLength);
            item.Property(i => i.Author).HasMaxLength(100);
            item.Property(i => i.CategoryHint).HasMaxLength(64);

            item.HasOne<Source>().WithMany().HasForeignKey(i => i.SourceId).OnDelete(DeleteBehavior.Cascade);

            // The duplicate guard. Our code checks first, but if two collectors
            // ever race, THIS is what stops the same item being stored twice.
            item.HasIndex(i => new { i.SourceId, i.ExternalId }).IsUnique().HasDatabaseName("ux_collected_items_source_external");
        });
    }
}
