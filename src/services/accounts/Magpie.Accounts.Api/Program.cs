using Magpie.Accounts.Api.Endpoints;
using Magpie.Accounts.Api.ErrorHandling;
using Magpie.Accounts.Application.Ping;
using Magpie.Accounts.Infrastructure;
using Magpie.Defaults;

// This file is the "composition root": the one place where the pieces of the
// Accounts service are wired together. Nothing else in the service uses "new"
// to create a service or knows which implementation it receives.

var builder = WebApplication.CreateBuilder(args);

// --- 1. Register services (what exists) ------------------------------------

// Specific error handlers go BEFORE the defaults: handlers run in the order
// they are registered, and the defaults add the catch-everything handler.
builder.Services.AddExceptionHandler<DependencyUnavailableExceptionHandler>();

// Logging, Problem Details and health checks, the same in every Magpie service.
builder.AddMagpieDefaults(GetPingHandler.ServiceName);

// Infrastructure: PostgreSQL adapter and its readiness check.
builder.Services.AddAccountsInfrastructure(builder.Configuration);

// Application: use cases. TimeProvider.System is the real clock.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GetPingHandler>();

var app = builder.Build();

// --- 2. Build the request pipeline (what happens to each request) ----------

app.UseMagpieDefaults();

app.MapMagpieHealthEndpoints();
app.MapPingEndpoints();

app.Run();
