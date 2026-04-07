namespace LegacyApiProxy.Models;

/// <summary>
/// Credential set associated with a single API key, loaded from configuration.
/// </summary>
public sealed class ApiKeyConfig
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
