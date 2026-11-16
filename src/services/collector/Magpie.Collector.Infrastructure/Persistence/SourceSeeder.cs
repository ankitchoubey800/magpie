using System.Text.Json;
using System.Text.Json.Serialization;
using Magpie.Collector.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Magpie.Collector.Infrastructure.Persistence;

/// <summary>One entry of the sources file (see infra/seed/sources.json).</summary>
public sealed record SourceSeed(string Name, SourceType Type, string Address, string? CategoryHint, int FetchIntervalMinutes);

public static class CollectorDatabase
{
    private static readonly JsonSerializerOptions SeedJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Creates the database if needed and brings its tables up to date. Run by "--migrate".</summary>
    public static async Task MigrateCollectorDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CollectorDbContext>().Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the sources from a JSON file that are not in the table yet (matched
    /// by address). Safe to run again and again. Returns how many were added.
    /// </summary>
    public static async Task<int> SeedSourcesAsync(this IServiceProvider services, string jsonFile, CancellationToken cancellationToken = default)
    {
        var seeds = JsonSerializer.Deserialize<List<SourceSeed>>(await File.ReadAllTextAsync(jsonFile, cancellationToken), SeedJson) ?? [];

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<CollectorDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        var existing = await database.Sources.Select(source => source.Address).ToListAsync(cancellationToken);
        var added = seeds
            .Where(seed => !existing.Contains(seed.Address))
            .Select(seed => Source.Create(seed.Name, seed.Type, seed.Address, seed.CategoryHint, seed.FetchIntervalMinutes, now))
            .ToList();

        database.Sources.AddRange(added);
        await database.SaveChangesAsync(cancellationToken);
        return added.Count;
    }
}
