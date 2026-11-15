using Magpie.Accounts.Application.Abstractions;
using Magpie.Accounts.Domain;

namespace Magpie.Accounts.Application.Ping;

/// <summary>
/// A "use case": one thing the system can do, written as one small class.
/// It coordinates (ask the port, build the domain object, shape the answer)
/// and contains no HTTP code and no SQL.
/// </summary>
public sealed class GetPingHandler(IDatabaseTimeSource databaseTimeSource, TimeProvider timeProvider)
{
    public const string ServiceName = "accounts";

    public async Task<PingResponse> HandleAsync(CancellationToken cancellationToken)
    {
        // TimeProvider (built into .NET) is used instead of Stopwatch so that
        // tests can control time and get the same result on every run.
        var startedAt = timeProvider.GetTimestamp();
        var serverTime = await databaseTimeSource.GetServerTimeAsync(cancellationToken);
        var roundTrip = timeProvider.GetElapsedTime(startedAt);

        var heartbeat = DatabaseHeartbeat.Create(serverTime, roundTrip);

        return new PingResponse(
            ServiceName,
            heartbeat.ServerTimeUtc,
            Math.Round(heartbeat.RoundTrip.TotalMilliseconds, 1),
            heartbeat.IsSlow);
    }
}
