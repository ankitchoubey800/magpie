using Magpie.Collector.Application.Collecting;
using Magpie.Collector.Infrastructure;
using Magpie.Collector.Infrastructure.Persistence;
using Magpie.Collector.Worker.Collecting;
using Magpie.Defaults;

// The collector is a WORKER: it has no pages and no public API. It is still
// built as a small web application, for one reason: so that it can answer the
// same /health/live and /health/ready questions as every other Magpie service.

var builder = WebApplication.CreateBuilder(args);

builder.AddMagpieDefaults("collector");

builder.Services.AddCollectorInfrastructure();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMagpieMediator(typeof(CollectSourceCommand).Assembly);

builder.Services.AddSingleton<CollectionRunner>();
builder.Services.AddSingleton<LoopHeartbeat>();

// One-off jobs do not start the loop.
var isOneOffJob = args.Contains("--migrate") || args.Contains("--seed-sources");
if (!isOneOffJob)
{
    builder.Services.AddHostedService<CollectionLoop>();
    builder.Services.AddHealthChecks()
        .AddCheck<LoopHealthCheck>("collection-loop", tags: [MagpieDefaultsExtensions.LiveTag]);
}

var app = builder.Build();

// "--migrate"                 create or update the collector's tables, then exit
// "--seed-sources <file>"     add sources from a JSON file that are not there yet, then exit
// The two can be combined; migration runs first.
if (isOneOffJob)
{
    if (args.Contains("--migrate"))
    {
        await app.Services.MigrateCollectorDatabaseAsync();
    }

    var seedFlag = Array.IndexOf(args, "--seed-sources");
    if (seedFlag >= 0)
    {
        var added = await app.Services.SeedSourcesAsync(args[seedFlag + 1]);
        app.Logger.LogInformation("Seeded {Count} new sources", added);
    }

    return;
}

app.UseMagpieDefaults();
app.MapMagpieHealthEndpoints();

app.Run();
