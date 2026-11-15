using Magpie.Accounts.Application.Abstractions;

namespace Magpie.Accounts.Tests.Fakes;

/// <summary>
/// Stands in for PostgreSQL. Tests decide what "the database" answers,
/// how long it takes, and whether it is down.
/// </summary>
public sealed class FakeDatabaseTimeSource : IDatabaseTimeSource
{
    public DateTimeOffset ServerTime { get; set; } = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    public bool IsDown { get; set; }

    /// <summary>Optional: called during the "query" so a test can move the clock.</summary>
    public Action? OnQuery { get; set; }

    public Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken)
    {
        if (IsDown)
        {
            throw new DependencyUnavailableException("postgresql", new TimeoutException("simulated outage"));
        }

        OnQuery?.Invoke();
        return Task.FromResult(ServerTime);
    }
}
