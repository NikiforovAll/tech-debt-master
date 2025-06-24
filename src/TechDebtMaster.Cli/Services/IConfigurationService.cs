namespace TechDebtMaster.Cli.Services;

public interface IConfigurationService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task<Dictionary<string, string>> GetAllAsync();
    Task<bool> RemoveAsync(string key);
    Task EnsureDefaultsAsync();
    AppConfiguration GetConfiguration();
}

public record AppConfiguration
{
    public string ApiKey { get; init; } = string.Empty;
#pragma warning disable CA1056 // URI-like properties should not be strings
    public string BaseUrl { get; init; } = string.Empty;
#pragma warning restore CA1056 // URI-like properties should not be strings
    public string Model { get; init; } = string.Empty;
    public string Provider { get; init; } = "dial";
}
