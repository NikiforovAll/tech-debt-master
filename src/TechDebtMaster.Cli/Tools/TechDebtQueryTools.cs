using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Tools.Responses;
using TechDebtMaster.Cli.Tools.Shared;
using TechDebtMaster.Cli.Tools.Utils;
using TechDebtMaster.Cli.Utilities;

namespace TechDebtMaster.Cli.Tools;

[McpServerToolType]
public static class TechDebtQueryTools
{
    [
        McpServerTool(Name = "tdm-list-items"),
        Description(
            "Get a list of technical debt issues across all files, with optional filtering by pattern, severity, and tags."
        )
    ]
    public static async Task<FileIssuesResponse> GetFileIssues(
        [FromServices] IRepositoryPathProvider pathProvider,
        [FromServices] IAnalysisService analysisService,
        [Description("Include pattern regex (optional)")] string? includePattern = null,
        [Description("Exclude pattern regex (optional)")] string? excludePattern = null,
        [Description("Filter by severity: Low, Medium, High, Critical (optional)")]
            string? severityFilter = null,
        [Description(
            "Filter by tag: CodeSmell, Naming, MagicNumber, Complexity, ErrorHandling, OutdatedPattern, Todo, Performance, Security, General (optional)"
        )]
            string? tagFilter = null,
        [Description("Page number (1-based, default: 1)")] int page = 1,
        [Description("Items per page (default: 5)")] int pageSize = 5
    )
    {
        var repositoryPath = pathProvider.RepositoryPath;

        var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (analysisReport == null)
        {
            throw new InvalidOperationException(
                $"No analysis data found. Run 'analyze debt <path>' first to generate analysis data."
            );
        }

        var fileDebtMap = AnalysisResultUtils.ExtractDebtItems(analysisReport);

        var filteredFileDebtMap = TechDebtToolsUtils.ApplyFiltering(
            fileDebtMap,
            includePattern,
            excludePattern,
            severityFilter,
            tagFilter
        );

        var allFilteredItems = filteredFileDebtMap.Values.SelectMany(items => items).ToList();
        var totalOriginalItems = fileDebtMap.Values.SelectMany(items => items).ToList();

        var orderedItems = allFilteredItems
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.Id)
            .ToList();

        var totalPages = (int)Math.Ceiling((double)allFilteredItems.Count / pageSize);
        var validatedPage = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));
        var skip = (validatedPage - 1) * pageSize;

        var paginatedItems = orderedItems.Skip(skip).Take(pageSize).ToList();

        var issues = paginatedItems
            .Select(item => new TechnicalDebtIssue
            {
                Id = item.Id,
                Summary = item.Summary,
                Severity = item.Severity.ToString(),
                Tags = [.. item.Tags.Select(t => t.ToString())],
            })
            .ToList();

        return new FileIssuesResponse
        {
            RepositoryPath = repositoryPath,
            FileFound = true,
            Issues = issues.AsReadOnly(),
            Summary = new FileIssuesSummary
            {
                TotalIssues = totalOriginalItems.Count,
                FilteredIssues = allFilteredItems.Count,
                CurrentPage = validatedPage,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = validatedPage < totalPages,
                HasPreviousPage = validatedPage > 1,
            },
            Filters = new TechDebtFilters
            {
                IncludePattern = includePattern,
                ExcludePattern = excludePattern,
                SeverityFilter = severityFilter,
                TagFilter = tagFilter,
            },
        };
    }

    [
        McpServerTool(Name = "tdm-get-item"),
        Description(
            "Get a specific technical debt item by its ID in the format 'filePath:id' and return its markdown description as an MCP resource"
        )
    ]
    public static async Task<DebtItemResponse> GetDebt(
        [FromServices] IRepositoryPathProvider pathProvider,
        [FromServices] IAnalysisService analysisService,
        [FromServices] ITechDebtStorageService techDebtStorage,
        [Description(
            "The debt item identifier in the format 'filePath:id' (e.g., 'src/MyClass.cs:DEBT001')"
        )]
            string debtId
    )
    {
        var repositoryPath = pathProvider.RepositoryPath;

        // Validate and parse the debt ID format
        if (
            !TechDebtToolsUtils.TryParseDebtId(debtId, out var targetFilePath, out var targetItemId)
        )
        {
            return new DebtItemResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Found = false,
                Error =
                    "Invalid debt ID format. Expected 'filePath:id' (e.g., 'src/MyClass.cs:DEBT001')",
            };
        }

        // Load analysis report
        var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (analysisReport == null)
        {
            return new DebtItemResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Found = false,
                Error =
                    "No analysis data found. Run 'analyze debt <path>' first to generate analysis data.",
            };
        }

        // Extract debt items from analysis report
        var fileDebtMap = AnalysisResultUtils.ExtractDebtItems(analysisReport);
        var debtItems = TechDebtToolsUtils.ExtractDebtItemsWithFilePath(fileDebtMap);

        // Find the specific debt item
        var targetItem = TechDebtToolsUtils.FindSpecificDebtItem(
            debtItems,
            targetFilePath,
            targetItemId
        );
        if (targetItem == null)
        {
            return new DebtItemResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Found = false,
                Error =
                    $"Debt item '{debtId}' not found. Use GetFileIssues to list available items.",
            };
        }

        // Load the markdown content using MCP resource pattern
        string markdownContent;
        try
        {
            markdownContent = await techDebtStorage.LoadTechDebtAsync(
                targetItem.DebtItem.Reference
            );

            if (string.IsNullOrWhiteSpace(markdownContent))
            {
                markdownContent = "No detailed content available for this debt item.";
            }
        }
        catch (FileNotFoundException)
        {
            markdownContent = "Detailed content file not found.";
        }
        catch (UnauthorizedAccessException)
        {
            markdownContent = "Access denied to detailed content file.";
        }
        catch (IOException ex)
        {
            markdownContent = $"I/O error loading detailed content: {ex.Message}";
        }

        // Create MCP resource URI following the dynamic resource pattern
        var resourceUri = $"debt://item/{Uri.EscapeDataString(debtId)}";

        return new DebtItemResponse
        {
            RepositoryPath = repositoryPath,
            DebtId = debtId,
            Found = true,
            FilePath = targetItem.FilePath,
            DebtItem = new DebtItemDetail
            {
                Id = targetItem.DebtItem.Id,
                Summary = targetItem.DebtItem.Summary,
                Severity = targetItem.DebtItem.Severity.ToString(),
                Tags = [.. targetItem.DebtItem.Tags.Select(t => t.ToString())],
                Timestamp = targetItem.DebtItem.Reference.Timestamp,
                ContentHash = targetItem.DebtItem.Reference.ContentHash,
            },
            // MCP Resource information following the dynamic resource pattern
            Resource = new McpResourceInfo
            {
                Uri = new Uri(resourceUri),
                Name = $"Debt Item: {targetItem.DebtItem.Id}",
                Description =
                    $"Technical debt analysis for {targetItem.FilePath}:{targetItem.DebtItem.Id}",
                MimeType = "text/markdown",
                Content = markdownContent,
            },
        };
    }
}
