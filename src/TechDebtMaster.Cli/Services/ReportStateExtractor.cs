using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;

namespace TechDebtMaster.Cli.Services;

/// <summary>
/// DTO for extracting state from JSON block in HTML report
/// </summary>
public class StateData
{
    public Dictionary<string, bool> HiddenItems { get; set; } = new();
    public Dictionary<string, bool> DoneItems { get; set; } = new();
}

public class ReportStateExtractor : IReportStateExtractor
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    public async Task<ReportState> ExtractStateAsync(string htmlReportPath)
    {
        var htmlContent = await File.ReadAllTextAsync(htmlReportPath);
        
        var debtData = ExtractDebtData(htmlContent);
        var stateData = ExtractStateFromJsonBlock(htmlContent);

        return new ReportState
        {
            DebtData = debtData,
            HiddenItems = stateData.HiddenItems,
            DoneItems = stateData.DoneItems
        };
    }

    private Dictionary<string, List<TechnicalDebtItem>> ExtractDebtData(string htmlContent)
    {
        // Extract the debtData JavaScript variable
        var debtDataPattern = @"const debtData = (.*?);";
        var match = Regex.Match(htmlContent, debtDataPattern, RegexOptions.Singleline);
        
        if (!match.Success)
        {
            throw new InvalidOperationException("Could not find debtData in HTML report. The report may be invalid or corrupted.");
        }

        var debtDataJson = match.Groups[1].Value;
        
        try
        {
            // Now that markdown content is excluded from JavaScript data, we can deserialize directly
            return JsonSerializer.Deserialize<Dictionary<string, List<TechnicalDebtItem>>>(debtDataJson, _jsonOptions) 
                   ?? new Dictionary<string, List<TechnicalDebtItem>>();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse debt data from HTML report: {ex.Message}", ex);
        }
    }

    private StateData ExtractStateFromJsonBlock(string htmlContent)
    {
        // Extract state from embedded JSON block
        var statePattern = @"<script type=""application/json"" id=""debt-state"">\s*(.*?)\s*</script>";
        var match = Regex.Match(htmlContent, statePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        if (!match.Success)
        {
            // No state block found, return empty state
            return new StateData();
        }

        var stateJson = match.Groups[1].Value.Trim();
        
        try
        {
            return JsonSerializer.Deserialize<StateData>(stateJson, _jsonOptions) ?? new StateData();
        }
        catch (JsonException)
        {
            // If state parsing fails, return empty state
            return new StateData();
        }
    }

}