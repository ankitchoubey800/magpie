using Magpie.Accounts.Api.Endpoints;
using Magpie.Accounts.Api.ErrorHandling;
using Magpie.Accounts.Api.Security;
using Magpie.Accounts.Application.PasswordReset;
using Magpie.Accounts.Application.Ping;
using Magpie.Accounts.Application.Registration;
using Magpie.Accounts.Application.SignIn;
using Magpie.Accounts.Infrastructure;
using Magpie.Accounts.Infrastructure.Persistence;
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

// This service sits behind the gateway: believe its X-Forwarded-... headers.
builder.TrustGatewayForwardedHeaders();

// Infrastructure: PostgreSQL, EF Core, Identity, email.
builder.Services.AddAccountsInfrastructure();

// Cookie sign-in, anti-forgery and the keys that protect them.
builder.AddAccountsSecurity();

// Application: use cases. TimeProvider.System is the real clock.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GetPingHandler>();
builder.Services.AddScoped<RegisterReaderHandler>();
builder.Services.AddScoped<ResendConfirmationHandler>();
builder.Services.AddScoped<ConfirmEmailHandler>();
builder.Services.AddScoped<SignInHandler>();
builder.Services.AddScoped<RequestPasswordResetHandler>();
builder.Services.AddScoped<ResetPasswordHandler>();

var app = builder.Build();

// --- 2. One-off jobs ---------------------------------------------------------

// "dotnet Magpie.Accounts.Api.dll --migrate" updates the database tables and
// exits. It never runs as part of normal start-up: with several copies of the
// service starting at once, they would all try to change the tables together.
if (args.Contains("--migrate"))
{
    await app.Services.MigrateAccountsDatabaseAsync();
    return;
}

// --- 3. Build the request pipeline (what happens to each request) ----------
// Order matters: each line wraps everything below it.

app.UseForwardedHeaders(); // learn the reader's real address from the gateway
app.UseMagpieDefaults();   // errors become Problem Details
app.UseRateLimiter();      // too many attempts from one address? stop here
app.UseAuthentication();   // read the session cookie: who is this?
app.UseAuthorization();    // is that person allowed to call this endpoint?

app.MapMagpieHealthEndpoints();
app.MapPingEndpoints();
app.MapAuthEndpoints();
app.MapExternalSignInEndpoints();

app.Run();
