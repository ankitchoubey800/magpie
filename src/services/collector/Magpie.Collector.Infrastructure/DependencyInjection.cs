using System.Net;
using Magpie.Collector.Application.Collecting;
using Magpie.Collector.Application.Sources;
using Magpie.Collector.Infrastructure.Persistence;
using Magpie.Collector.Infrastructure.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Magpie.Collector.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCollectorInfrastructure(this IServiceCollection services)
    {
        // --- Settings, checked at start-up ---
        services.AddOptions<CollectorOptions>()
            .BindConfiguration(CollectorOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CollectorDatabaseOptions>()
            .BindConfiguration(CollectorDatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<CollectorOptions>>().Value;
            return new CollectionLimits(TimeSpan.FromSeconds(options.PerSourceTimeoutSeconds), options.MaxItemsPerFetch);
        });
        services.AddSingleton(RetrySettings.Default);

        // --- The collector's own database ---
        services.AddDbContext<CollectorDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<IOptions<CollectorDatabaseOptions>>().Value.CollectorDb));

        services.AddScoped<ISourceStore, SourceStore>();
        services.AddScoped<IItemStore, ItemStore>();
        services.AddHealthChecks().AddCheck<CollectorDatabaseHealthCheck>("postgresql", tags: ["ready"]);

        // --- The outside web ---
        // IHttpClientFactory hands out HttpClients that share a pool of
        // connections. Creating "new HttpClient()" per request is a classic way
        // to run a busy server out of network sockets.
        services.AddHttpClient(HttpFetcher.ClientName, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<CollectorOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);

                // Say honestly who we are and where to complain.
                client.DefaultRequestHeaders.UserAgent.ParseAdd($"MagpieCollector/0.1 (+{options.ContactUrl})");
                client.DefaultRequestHeaders.Accept.ParseAdd(
                    "application/rss+xml, application/atom+xml, application/xml, application/json;q=0.9, */*;q=0.1");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5), // notice when a site moves to a new address
            });

        services.AddScoped<HttpFetcher>();

        // --- Plug-ins and the factory that hands them out ---
        services.AddScoped<RssContentSource>();
        services.AddScoped<HackerNewsContentSource>();
        services.AddScoped<IContentSourceFactory, ContentSourceFactory>();

        return services;
    }
}
