using System.Text.Json;
using System.Text.Json.Serialization;

namespace TechDebtMaster.Cli.Services;

public interface IDialService
{
    Task<List<DialModel>> GetModelsAsync();
    Task<DialModelLimits?> GetModelLimitsAsync(string modelId);
}

public class DialService(HttpClient httpClient, IConfigurationService configService) : IDialService
{
    public async Task<List<DialModel>> GetModelsAsync()
    {
        var config = configService.GetConfiguration();
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException(
                "AI API key not configured. Use 'config set ai.key <your-key>' to set it."
            );
        }

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Api-Key", config.ApiKey);

        var response = await httpClient.GetAsync(new Uri($"{config.BaseUrl}/openai/models"));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var modelsResponse = JsonSerializer.Deserialize<DialModelsResponse>(content);

        return modelsResponse?.Data ?? [];
    }

    public async Task<DialModelLimits?> GetModelLimitsAsync(string modelId)
    {
        var config = configService.GetConfiguration();
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException(
                "AI API key not configured. Use 'config set ai.key <your-key>' to set it."
            );
        }

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Api-Key", config.ApiKey);

        var response = await httpClient.GetAsync(
            new Uri($"{config.BaseUrl}/v1/deployments/{modelId}/limits")
        );
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<DialModelLimits>(content);
    }
}

#pragma warning disable CA1002 // Do not expose generic lists

public record DialModelsResponse
{
    [JsonPropertyName("data")]
    public List<DialModel> Data { get; init; } = [];
}

public record DialModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; init; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; init; } = string.Empty;
}

public record DialModelLimits
{
    [JsonPropertyName("minuteTokenStats")]
    public TokenStats? MinuteTokenStats { get; init; }

    [JsonPropertyName("dayTokenStats")]
    public TokenStats? DayTokenStats { get; init; }

    [JsonPropertyName("weekTokenStats")]
    public TokenStats? WeekTokenStats { get; init; }

    [JsonPropertyName("monthTokenStats")]
    public TokenStats? MonthTokenStats { get; init; }

    [JsonPropertyName("hourRequestStats")]
    public RequestStats? HourRequestStats { get; init; }

    [JsonPropertyName("dayRequestStats")]
    public RequestStats? DayRequestStats { get; init; }
}

public record TokenStats
{
    [JsonPropertyName("total")]
    public ulong Total { get; init; }

    [JsonPropertyName("used")]
    public ulong Used { get; init; }
}

public record RequestStats
{
    [JsonPropertyName("total")]
    public ulong Total { get; init; }

    [JsonPropertyName("used")]
    public ulong Used { get; init; }
}
