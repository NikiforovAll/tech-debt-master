using System.Text.Json;
using TechDebtMaster.Cli.Services.Analysis;

namespace TechDebtMaster.Cli.Utilities;

/// <summary>
/// Utilities for working with analysis results, including serialization/deserialization
/// </summary>
public static class AnalysisResultUtils
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Extracts TechDebtAnalysisResult from an analysis result object
    /// </summary>
    /// <param name="resultObj">The analysis result object from the analysis report</param>
    /// <returns>TechDebtAnalysisResult if successful, null otherwise</returns>
    public static TechDebtAnalysisResult? ExtractTechDebtAnalysisResult(object? resultObj)
    {
        if (resultObj == null)
        {
            return null;
        }

        // If it's already the correct type, return it directly
        if (resultObj is TechDebtAnalysisResult directResult)
        {
            return directResult;
        }

        // Try to deserialize through JSON (this handles cases where the object is a dictionary or JsonElement)
#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            var json = JsonSerializer.Serialize(resultObj, s_jsonOptions);
            return JsonSerializer.Deserialize<TechDebtAnalysisResult>(json, s_jsonOptions);
        }
        catch
        {
            // Ignore deserialization errors and return null
            return null;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Extracts debt items from an analysis report
    /// </summary>
    /// <param name="analysisReport">The analysis report to extract items from</param>
    /// <returns>Dictionary mapping file paths to their debt items</returns>
    public static Dictionary<string, List<TechnicalDebtItem>> ExtractDebtItems(
        AnalysisReport analysisReport
    )
    {
        var fileDebtMap = new Dictionary<string, List<TechnicalDebtItem>>();

        foreach (var (filePath, fileHistory) in analysisReport.FileHistories)
        {
            if (
                fileHistory.Current.AnalysisResults.TryGetValue(
                    Services.Analysis.Handlers.TechDebtAnalysisHandler.ResultKey,
                    out var resultObj
                )
            )
            {
                var techDebtResult = ExtractTechDebtAnalysisResult(resultObj);
                if (techDebtResult?.Items?.Count > 0)
                {
                    fileDebtMap[filePath] = techDebtResult.Items;
                }
            }
        }

        return fileDebtMap;
    }
}
