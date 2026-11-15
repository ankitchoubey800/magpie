namespace Magpie.Accounts.Application.Ping;

/// <summary>
/// The shape sent to the browser. It is NOT the domain object: screens get a
/// shape made for them, so the domain can change without breaking the website.
/// </summary>
public sealed record PingResponse(string Service, DateTimeOffset DatabaseTimeUtc, double RoundTripMs, bool IsSlow);
