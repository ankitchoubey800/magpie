using Magpie.Collector.Application.Collecting;
using Magpie.Collector.Application.Sources;
using Magpie.Collector.Domain;
using Magpie.Collector.Infrastructure;
using Magpie.Collector.Infrastructure.Persistence;
using Magpie.Collector.Infrastructure.Sources;
using Magpie.Collector.Tests.Fakes;
using Magpie.Collector.Worker.Collecting;
using Magpie.Defaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Magpie.Collector.Tests.Integration;

/// <summary>One real PostgreSQL server in Docker, shared by all the tests in this project.</summary>
public sealed class CollectorPostgres : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    /// <summary>
    /// Every test gets its OWN empty database on that server. The collector
    /// visits "all sources that are due", so tests sharing one table would
    /// trip over each other's sources.
    /// </summary>
    public string NewDatabase() =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = $"collector_{Guid.NewGuid():N}" }.ConnectionString;
}

[CollectionDefinition(Name)]
public sealed class CollectorCollection : ICollectionFixture<CollectorPostgres>
{
    public const string Name = "collector";
}

/// <summary>
/// The whole collector, wired exactly as Program.cs wires it, with three
/// replacements: the internet (StubWeb), the calendar (ManualClock), and
/// shorter time limits so the "hanging source" test takes seconds, not minutes.
/// </summary>
public sealed class CollectorTestHost : IAsyncDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;

    public StubWeb Web { get; } = new();

    public ManualClock Clock { get; } = new(Start);

    private CollectorTestHost(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CollectorDb"] = connectionString,
                ["Collector:ContactUrl"] = "https://magpie.test/about-our-collector",
                ["Collector:HttpTimeoutSeconds"] = "1",
                ["Collector:PerSourceTimeoutSeconds"] = "2",
                ["Collector:MaxParallelSources"] = "4",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddCollectorInfrastructure();
        services.AddMagpieMediator(typeof(CollectSourceCommand).Assembly);
        services.AddSingleton<CollectionRunner>();

        // The three replacements. For a service registered twice, the LAST registration wins.
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton(new RetrySettings(MaxAttempts: 2, FirstDelay: TimeSpan.FromMilliseconds(50)));
        services.AddHttpClient(HttpFetcher.ClientName).ConfigurePrimaryHttpMessageHandler(() => Web);

        _services = services.BuildServiceProvider();
    }

    public static async Task<CollectorTestHost> StartAsync(CollectorPostgres postgres)
    {
        var host = new CollectorTestHost(postgres.NewDatabase());
        await host._services.MigrateCollectorDatabaseAsync();
        return host;
    }

    public IServiceProvider Services => _services;

    public Task<IReadOnlyList<CollectSourceResult>> RunOneRoundAsync() =>
        _services.GetRequiredService<CollectionRunner>().RunOnceAsync(CancellationToken.None);

    public async Task<Guid> AddSourceAsync(string name, string address, SourceType type = SourceType.Rss, int intervalMinutes = 30)
    {
        await using var scope = _services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<CollectorDbContext>();
        var source = Source.Create(name, type, address, "programming", intervalMinutes, Clock.GetUtcNow());
        database.Sources.Add(source);
        await database.SaveChangesAsync();
        return source.Id;
    }

    public async Task<Source> SourceAsync(Guid id)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CollectorDbContext>().Sources.AsNoTracking().SingleAsync(source => source.Id == id);
    }

    public async Task<List<CollectedItem>> ItemsAsync(Guid sourceId)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CollectorDbContext>().Items.AsNoTracking()
            .Where(item => item.SourceId == sourceId).OrderBy(item => item.ExternalId).ToListAsync();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
