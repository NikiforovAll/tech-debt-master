using System.Text.Json;

namespace TechDebtMaster.Cli.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;

    private static readonly Dictionary<string, string> s_defaultValues = new()
    {
        ["ai.url"] = "https://ai-proxy.lab.epam.com",
        ["ai.model"] = "gpt-4o",
    };

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

    public async Task EnsureDefaultsAsync()
    {
        var config = await LoadConfigAsync();
        bool hasChanges = false;

        foreach (var defaultValue in s_defaultValues)
        {
            if (!config.ContainsKey(defaultValue.Key))
            {
                config[defaultValue.Key] = defaultValue.Value;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await SaveConfigAsync(config);
        }
    }

    public AppConfiguration GetConfiguration()
    {
        var config = LoadConfigAsync().GetAwaiter().GetResult();
        return new AppConfiguration
        {
            ApiKey = config.TryGetValue("ai.key", out var apiKey)
                ? apiKey
                : Environment.GetEnvironmentVariable("DIAL_API_KEY") ?? string.Empty,
            BaseUrl = config.TryGetValue("ai.url", out var baseUrl)
                ? baseUrl
                : s_defaultValues["ai.url"],
            Model = config.TryGetValue("ai.model", out var model)
                ? model
                : s_defaultValues["ai.model"],
        };
    }

    private static void EnsureConfigDirectoryExists(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
