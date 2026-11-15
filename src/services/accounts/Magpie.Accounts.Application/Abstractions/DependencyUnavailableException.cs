namespace Magpie.Accounts.Application.Abstractions;

/// <summary>
/// Thrown when something this service depends on (a database, another service)
/// cannot be reached. Infrastructure code translates technology-specific errors
/// (for example NpgsqlException) into this one, so the layers above never need
/// to know which database library is in use.
/// </summary>
public sealed class DependencyUnavailableException(string dependency, Exception innerException)
    : Exception($"The dependency '{dependency}' is unavailable.", innerException)
{
    public string Dependency { get; } = dependency;
}
