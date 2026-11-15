using Magpie.Accounts.Application.Abstractions;
using Npgsql;

namespace Magpie.Accounts.Infrastructure.Postgres;

/// <summary>
/// The "adapter": the real implementation of the IDatabaseTimeSource port.
/// This is the only place in the Accounts service that contains SQL.
/// </summary>
internal sealed class PostgresTimeSource(NpgsqlDataSource dataSource) : IDatabaseTimeSource
{
    public async Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // The data source hands out a pooled connection, and the command
            // returns it to the pool when it is disposed ("await using").
            await using var command = dataSource.CreateCommand("SELECT now()");
            var result = await command.ExecuteScalarAsync(cancellationToken);

            // PostgreSQL's now() is "timestamp with time zone"; Npgsql returns it as a UTC DateTime.
            var utc = DateTime.SpecifyKind((DateTime)result!, DateTimeKind.Utc);
            return new DateTimeOffset(utc);
        }
        catch (NpgsqlException exception)
        {
            // Translate the library-specific error into the application's own error type.
            throw new DependencyUnavailableException("postgresql", exception);
        }
    }
}
