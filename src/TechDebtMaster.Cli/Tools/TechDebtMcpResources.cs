using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Tools.Shared;
using TechDebtMaster.Cli.Utilities;

namespace TechDebtMaster.Cli.Tools;

/// <summary>
/// MCP Resource type for providing access to technical debt item details
/// </summary>
[McpServerResourceType]
public static class TechDebtMcpResources
{
    [
        McpServerResource(
            UriTemplate = "debt://{filePath}/{id}",
            Name = "tdm-item-as-resource",
            MimeType = "text/markdown"
        ),
        Description(
            "Get detailed information about a specific technical debt item including its markdown description."
        )
    ]
    public static async Task<string> GetDebt(
        [Description("The file path containing the debt item")] string filePath,
        [Description("The unique identifier of the debt item")] string id,
        [FromServices] IRepositoryPathProvider pathProvider,
        [FromServices] IAnalysisService analysisService,
        [FromServices] ITechDebtStorageService techDebtStorage
    )
    {
        var repositoryPath = pathProvider.RepositoryPath;

        // Load analysis report
        var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (analysisReport == null)
        {
            throw new InvalidOperationException(
                $"No analysis data found. Run 'analyze debt <path>' first to generate analysis data."
            );
        }

        // Extract debt items from analysis report
        var fileDebtMap = AnalysisResultUtils.ExtractDebtItems(analysisReport);

        if (fileDebtMap.Count == 0)
        {
            throw new InvalidOperationException(
                "No technical debt items found in analysis results."
            );
        }

        // Find the specific debt item
        var targetDebtItem = FindSpecificDebtItem(fileDebtMap, filePath, id);
        if (targetDebtItem == null)
        {
            throw new ArgumentException($"Debt item '{id}' not found in file '{filePath}'");
        }

        // Load the detailed markdown content
        try
        {
            var markdownContent = await techDebtStorage.LoadTechDebtAsync(targetDebtItem.Reference);

            if (string.IsNullOrWhiteSpace(markdownContent))
            {
                throw new InvalidOperationException(
                    $"No detailed content available for debt item '{id}'. The content file may have been deleted or moved."
                );
            }

            // Build a comprehensive response with metadata and content
            var response = $"""
                # Technical Debt Item: {filePath}:{id}

                **File:** {filePath}  
                **ID:** {id}  
                **Summary:** {targetDebtItem.Summary}  
                **Severity:** {targetDebtItem.Severity}  
                **Tags:** {string.Join(", ", targetDebtItem.Tags)}  
                **Analysis Date:** {targetDebtItem.Reference.Timestamp:yyyy-MM-dd HH:mm:ss} UTC  

                ---

                ## Detailed Analysis

                {markdownContent}
                """;

            return response;
        }
        catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
        {
            throw new InvalidOperationException(
                $"Error loading content for debt item '{id}': {ex.Message}"
            );
        }
    }

    private static TechnicalDebtItem? FindSpecificDebtItem(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap,
        string targetFilePath,
        string targetItemId
    )
    {
        // First try exact file path match
        if (fileDebtMap.TryGetValue(targetFilePath, out var exactItems))
        {
            var exactMatch = exactItems.FirstOrDefault(item =>
                string.Equals(item.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
            );
            if (exactMatch != null)
            {
                return exactMatch;
            }
        }

        // Try relative path match (match end of file path)
        foreach (var (filePath, items) in fileDebtMap)
        {
            if (filePath.EndsWith(targetFilePath, StringComparison.OrdinalIgnoreCase))
            {
                var relativeMatch = items.FirstOrDefault(item =>
                    string.Equals(item.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
                );
                if (relativeMatch != null)
                {
                    return relativeMatch;
                }
            }
        }

        // Try filename only match
        var targetFileName = Path.GetFileName(targetFilePath);
        foreach (var (filePath, items) in fileDebtMap)
        {
            if (
                string.Equals(
                    Path.GetFileName(filePath),
                    targetFileName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                var filenameMatch = items.FirstOrDefault(item =>
                    string.Equals(item.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
                );
                if (filenameMatch != null)
                {
                    return filenameMatch;
                }
            }
        }

        return null;
    }
}
