namespace TechDebtMaster.Cli.Services;

public interface IConfigurationService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task<Dictionary<string, string>> GetAllAsync();
    Task<bool> RemoveAsync(string key);
}
