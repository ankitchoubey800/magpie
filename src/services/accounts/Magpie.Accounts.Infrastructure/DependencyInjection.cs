using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Application.Interests;
using Magpie.Accounts.Infrastructure.Email;
using Magpie.Accounts.Infrastructure.Identity;
using Magpie.Accounts.Infrastructure.Persistence;
using Magpie.Accounts.Infrastructure.Postgres;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Magpie.Accounts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsInfrastructure(this IServiceCollection services)
    {
        // --- Settings: read from configuration, checked when the service starts ---
        services.AddOptions<PostgresOptions>()
            .BindConfiguration(PostgresOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // --- PostgreSQL: one connection pool for the whole process ---
        services.AddSingleton(serviceProvider =>
            NpgsqlDataSource.Create(serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value.AccountsDb!));

        services.AddScoped<IDatabaseTimeSource, PostgresTimeSource>();
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);

        services.AddAccountsDbContext();

        // --- Accounts: ASP.NET Core Identity ---
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Password rules: length matters far more than forcing symbols.
                options.Password.RequiredLength = 10;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true;

                // No signing in until the email address is proven to be yours.
                options.SignIn.RequireConfirmedEmail = true;

                // Five wrong passwords in a row: wait five minutes.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName)
            .AddAccountsStores();

        // Reset links use their own, shorter-lived token maker.
        services.Configure<IdentityOptions>(options =>
            options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName);

        services.AddScoped<IReaderAccounts, IdentityReaderAccounts>();
        services.AddScoped<IExternalSignIn, IdentityExternalSignIn>();

        // --- Interests: one door for reading, one for writing ---
        services.AddScoped<IInterestQueries, InterestQueries>();
        services.AddScoped<IInterestStore, InterestStore>();

        // --- Email ---
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
