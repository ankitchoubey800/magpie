using Magpie.Defaults;

// The gateway is the single front door. It contains NO business logic:
// it reads a routing table from configuration and forwards requests.

var builder = WebApplication.CreateBuilder(args);

builder.AddMagpieDefaults("gateway");

// YARP ("Yet Another Reverse Proxy") reads its routes and destinations from
// the "ReverseProxy" section of appsettings.json (or environment variables).
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseMagpieDefaults();

// The gateway's own health endpoints are matched before the proxy routes.
app.MapMagpieHealthEndpoints();

app.MapReverseProxy();

app.Run();
