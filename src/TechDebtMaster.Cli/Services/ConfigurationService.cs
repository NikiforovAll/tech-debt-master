using System.Text.Json;

namespace TechDebtMaster.Cli.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationService()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDirectory = Path.Combine(homeDirectory, ".techdebtmaster");
        _configPath = Path.Combine(configDirectory, "config.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        EnsureConfigDirectoryExists(configDirectory);
    }

    public async Task<string?> GetAsync(string key)
    {
        var config = await LoadConfigAsync();
        return config.TryGetValue(key, out var value) ? value : null;
    }

    public async Task SetAsync(string key, string value)
    {
        var config = await LoadConfigAsync();
        config[key] = value;
        await SaveConfigAsync(config);
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        return await LoadConfigAsync();
    }

    public async Task<bool> RemoveAsync(string key)
    {
        var config = await LoadConfigAsync();
        if (config.Remove(key))
        {
            await SaveConfigAsync(config);
            return true;
        }
        return false;
    }

    private async Task<Dictionary<string, string>> LoadConfigAsync()
    {
        if (!File.Exists(_configPath))
        {
            return [];
        }

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var json = await File.ReadAllTextAsync(_configPath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private async Task SaveConfigAsync(Dictionary<string, string> config)
    {
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await File.WriteAllTextAsync(_configPath, json);
    }

    private static void EnsureConfigDirectoryExists(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
