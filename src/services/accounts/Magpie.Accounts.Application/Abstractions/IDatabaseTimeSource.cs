namespace Magpie.Accounts.Application.Abstractions;

/// <summary>
/// A "port": the Application layer says WHAT it needs ("tell me the database's
/// current time") without knowing HOW it is done. The Infrastructure project
/// supplies the real PostgreSQL version; tests supply a fake.
/// </summary>
public interface IDatabaseTimeSource
{
    /// <exception cref="DependencyUnavailableException">The database could not be reached.</exception>
    Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken);
}
