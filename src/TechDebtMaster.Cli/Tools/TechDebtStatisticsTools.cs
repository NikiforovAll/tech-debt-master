using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Tools.Responses;
using TechDebtMaster.Cli.Tools.Shared;
using TechDebtMaster.Cli.Tools.Utils;
using TechDebtMaster.Cli.Utilities;

namespace TechDebtMaster.Cli.Tools;

[McpServerToolType]
public static class TechDebtStatisticsTools
{
    [
        McpServerTool(Name = "tdm-get-repo-stats"),
        Description(
            "Get comprehensive technical debt statistics including tag distribution, severity distribution, and file analysis counts."
        )
    ]
    public static async Task<TechDebtStatisticsResponse> GetTechnicalDebtStatistics(
        [FromServices] IRepositoryPathProvider pathProvider,
        [FromServices] IAnalysisService analysisService,
        [Description("Include pattern regex (optional)")] string? includePattern = null,
        [Description("Exclude pattern regex (optional)")] string? excludePattern = null,
        [Description("Filter by severity: Low, Medium, High, Critical (optional)")]
            string? severityFilter = null,
        [Description(
            "Filter by tag: CodeSmell, Naming, MagicNumber, Complexity, ErrorHandling, OutdatedPattern, Todo, Performance, Security, General (optional)"
        )]
            string? tagFilter = null
    )
    {
        var repositoryPath = pathProvider.RepositoryPath;

        // Load analysis report
        var analysisReport =
            await analysisService.LoadAnalysisReportAsync(repositoryPath)
            ?? throw new InvalidOperationException(
                $"No analysis data found. Run 'analyze debt <path>' first to generate analysis data."
            );

        // Extract debt items from analysis report
        var fileDebtMap = AnalysisResultUtils.ExtractDebtItems(analysisReport);

        if (fileDebtMap.Count == 0)
        {
            return new TechDebtStatisticsResponse
            {
                RepositoryPath = repositoryPath,
                Filters = new TechDebtFilters
                {
                    IncludePattern = includePattern,
                    ExcludePattern = excludePattern,
                    SeverityFilter = severityFilter,
                    TagFilter = tagFilter,
                },
                Summary = new TechDebtSummary
                {
                    TotalFilesAnalyzed = 0,
                    FilesDisplayed = 0,
                    TotalDebtItems = 0,
                    FilteredDebtItems = 0,
                    FilesWithDebt = 0,
                    FilteredFilesWithDebt = 0,
                },
                SeverityDistribution = [],
                TagDistribution = [],
                TopFilesByDebtCount = [],
            };
        }

        // Apply filtering
        var filteredFileDebtMap = TechDebtToolsUtils.ApplyFiltering(
            fileDebtMap,
            includePattern,
            excludePattern,
            severityFilter,
            tagFilter
        );

        // Calculate statistics
        var allItems = filteredFileDebtMap.Values.SelectMany(items => items).ToList();
        var originalAllItems = fileDebtMap.Values.SelectMany(items => items).ToList();

        // Severity distribution
        var severityDistribution = allItems
            .GroupBy(item => item.Severity)
            .OrderByDescending(g => g.Key)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // Tag distribution
        var tagDistribution = allItems
            .SelectMany(item => item.Tags)
            .GroupBy(tag => tag)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // File statistics
        var filesWithDebt = filteredFileDebtMap.Count(kvp => kvp.Value.Count > 0);
        var originalFilesWithDebt = fileDebtMap.Count(kvp => kvp.Value.Count > 0);

        return new TechDebtStatisticsResponse
        {
            RepositoryPath = repositoryPath,
            Filters = new TechDebtFilters
            {
                IncludePattern = includePattern,
                ExcludePattern = excludePattern,
                SeverityFilter = severityFilter,
                TagFilter = tagFilter,
            },
            Summary = new TechDebtSummary
            {
                TotalFilesAnalyzed = fileDebtMap.Count,
                FilesDisplayed = filteredFileDebtMap.Count,
                TotalDebtItems = originalAllItems.Count,
                FilteredDebtItems = allItems.Count,
                FilesWithDebt = originalFilesWithDebt,
                FilteredFilesWithDebt = filesWithDebt,
            },
            SeverityDistribution = severityDistribution,
            TagDistribution = tagDistribution,
            TopFilesByDebtCount = filteredFileDebtMap
                .Where(kvp => kvp.Value.Count > 0)
                .OrderByDescending(kvp => kvp.Value.Count)
                .Take(10)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count),
        };
    }
}
