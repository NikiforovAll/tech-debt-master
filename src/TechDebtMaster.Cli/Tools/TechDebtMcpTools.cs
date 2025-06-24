using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;
using TechDebtMaster.Cli.Utilities;

namespace TechDebtMaster.Cli.Tools;

[McpServerToolType]
public static class TechDebtMcpTools
{
    [
        McpServerTool(Name = "tdm-get-current-repo"),
        Description("Get the current repository path being served.")
    ]
    public static string GetRepositoryPath([FromServices] IRepositoryPathProvider pathProvider)
    {
        return $"Current repository: {pathProvider.RepositoryPath}";
    }

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
                CommonIssues = [],
            };
        }

        // Apply filtering
        var filteredFileDebtMap = ApplyFiltering(
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
            CommonIssues = allItems
                .GroupBy(item => item.Summary)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Count()),
        };
    }

    [
        McpServerTool(Name = "tdm-list-file-issues"),
        Description(
            "Get a list of technical debt issues in a specific file, similar to 'debt view' command output with issue ID, tags, severity, and summary."
        )
    ]
    public static async Task<FileIssuesResponse> GetFileIssues(
        [FromServices] IRepositoryPathProvider pathProvider,
        [FromServices] IAnalysisService analysisService,
        [Description("The file path to get issues for")] string filePath,
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
        var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (analysisReport == null)
        {
            throw new InvalidOperationException(
                $"No analysis data found. Run 'analyze debt <path>' first to generate analysis data."
            );
        }

        // Extract debt items from analysis report
        var fileDebtMap = AnalysisResultUtils.ExtractDebtItems(analysisReport);

        // Check if the specific file exists in the analysis
        if (!fileDebtMap.TryGetValue(filePath, out var debtItems))
        {
            // Try to find the file with a relative path or filename match
            var matchingEntry = fileDebtMap.FirstOrDefault(kvp =>
                kvp.Key.EndsWith(filePath, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(kvp.Key)
                    .Equals(Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase)
            );

            if (matchingEntry.Key != null)
            {
                filePath = matchingEntry.Key;
                debtItems = matchingEntry.Value;
            }
            else
            {
                return new FileIssuesResponse
                {
                    RepositoryPath = repositoryPath,
                    FilePath = filePath,
                    FileFound = false,
                    Issues = [],
                    Summary = new FileIssuesSummary { TotalIssues = 0, FilteredIssues = 0 },
                };
            }
        }

        // Apply filtering to the debt items
        var filteredItems = ApplyItemFiltering(debtItems, severityFilter, tagFilter);

        // Convert to issue responses
        var issues = filteredItems
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.Id)
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
            FilePath = filePath,
            FileFound = true,
            Issues = issues.AsReadOnly(),
            Summary = new FileIssuesSummary
            {
                TotalIssues = debtItems.Count,
                FilteredIssues = filteredItems.Count,
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
        McpServerTool(Name = "tdm-get-debt-item"),
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
        if (!TryParseDebtId(debtId, out var targetFilePath, out var targetItemId))
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
        var debtItems = ExtractDebtItemsWithFilePath(fileDebtMap);

        // Find the specific debt item
        var targetItem = FindSpecificDebtItem(debtItems, targetFilePath, targetItemId);
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

    [
        McpServerTool(Name = "tdm-remove-debt"),
        Description(
            "Remove a specific technical debt item by its ID in the format 'filePath:id'. This will delete both the debt item metadata from analysis and its associated content file."
        )
    ]
    public static async Task<RemoveDebtResponse> RemoveDebt(
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
        if (!TryParseDebtId(debtId, out var targetFilePath, out var targetItemId))
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error =
                    "Invalid debt ID format. Expected 'filePath:id' (e.g., 'src/MyClass.cs:DEBT001')",
            };
        }

        // Load analysis report
        var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (analysisReport == null)
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error =
                    "No analysis data found. Run 'analyze debt <path>' first to generate analysis data.",
            };
        }

        // Extract debt items from analysis report
        var fileDebtMap = AnalysisResultUtils.ExtractDebtItems(analysisReport);
        var debtItems = ExtractDebtItemsWithFilePath(fileDebtMap);

        // Find the specific debt item
        var targetItem = FindSpecificDebtItem(debtItems, targetFilePath, targetItemId);
        if (targetItem == null)
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error =
                    $"Debt item '{debtId}' not found. Use tdm-list-file-issues to list available items.",
            };
        }

        var result = new RemoveDebtResponse
        {
            RepositoryPath = repositoryPath,
            DebtId = debtId,
            FilePath = targetItem.FilePath,
            ItemId = targetItem.DebtItem.Id,
        };

        try
        {
            // Step 1: Try to delete the detailed content file
            try
            {
                result.ContentDeleted = await techDebtStorage.DeleteAsync(
                    targetItem.DebtItem.Reference
                );
            }
            catch (FileNotFoundException)
            {
                result.ContentDeleted = false;
            }
            catch (UnauthorizedAccessException)
            {
                result.ContentDeleted = false;
            }
            catch (IOException)
            {
                result.ContentDeleted = false;
            }

            // Step 2: Remove the debt item from the analysis report
            if (
                analysisReport.FileHistories.TryGetValue(targetItem.FilePath, out var fileHistory)
                && fileHistory.Current.AnalysisResults.TryGetValue(
                    TechDebtAnalysisHandler.ResultKey,
                    out var resultObj
                )
            )
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                };

                var json = JsonSerializer.Serialize(resultObj, jsonOptions);
                var techDebtResult = JsonSerializer.Deserialize<TechDebtAnalysisResult>(
                    json,
                    jsonOptions
                );

                if (techDebtResult?.Items != null)
                {
                    var originalCount = techDebtResult.Items.Count;

                    // Remove the specific debt item
                    techDebtResult.Items.RemoveAll(item =>
                        string.Equals(
                            item.Id,
                            targetItem.DebtItem.Id,
                            StringComparison.OrdinalIgnoreCase
                        )
                    );

                    // Check if we actually removed an item
                    if (techDebtResult.Items.Count < originalCount)
                    {
                        // Update the analysis results
                        fileHistory.Current.AnalysisResults[TechDebtAnalysisHandler.ResultKey] =
                            techDebtResult;

                        // Save the updated analysis report
                        await analysisService.SaveAnalysisReportAsync(
                            repositoryPath,
                            analysisReport
                        );
                        result.MetadataDeleted = true;
                    }
                }
            }

            result.Success = result.ContentDeleted || result.MetadataDeleted;

            if (result.Success)
            {
                if (result.ContentDeleted && result.MetadataDeleted)
                {
                    result.Message =
                        $"Successfully removed debt item '{targetItem.DebtItem.Id}' and its content.";
                }
                else if (result.MetadataDeleted)
                {
                    result.Message =
                        $"Successfully removed debt item '{targetItem.DebtItem.Id}' from analysis (content was already missing).";
                }
                else
                {
                    result.Message =
                        $"Successfully deleted content for '{targetItem.DebtItem.Id}' (metadata will be cleaned up later).";
                }
            }
            else
            {
                result.Error =
                    $"Failed to remove debt item '{targetItem.DebtItem.Id}'. The item may have already been deleted or is inaccessible.";
            }

            return result;
        }
        catch (InvalidOperationException ex)
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error = $"An error occurred while removing the debt item: {ex.Message}",
                FilePath = targetItem.FilePath,
                ItemId = targetItem.DebtItem.Id,
            };
        }
        catch (JsonException ex)
        {
            return new RemoveDebtResponse
            {
                RepositoryPath = repositoryPath,
                DebtId = debtId,
                Success = false,
                Error = $"Failed to process analysis data: {ex.Message}",
                FilePath = targetItem.FilePath,
                ItemId = targetItem.DebtItem.Id,
            };
        }
    }

    // Helper method to parse debt ID format "filePath:id"
    private static bool TryParseDebtId(string debtId, out string filePath, out string itemId)
    {
        filePath = string.Empty;
        itemId = string.Empty;

        if (string.IsNullOrWhiteSpace(debtId))
        {
            return false;
        }

        var lastColonIndex = debtId.LastIndexOf(':');
        if (lastColonIndex <= 0 || lastColonIndex == debtId.Length - 1)
        {
            return false;
        }

        filePath = debtId.Substring(0, lastColonIndex);
        itemId = debtId.Substring(lastColonIndex + 1);

        return !string.IsNullOrWhiteSpace(filePath) && !string.IsNullOrWhiteSpace(itemId);
    }

    // Helper method to extract debt items with file path information
    private static List<DebtItemWithFile> ExtractDebtItemsWithFilePath(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap
    )
    {
        var debtItems = new List<DebtItemWithFile>();

        foreach (var (filePath, items) in fileDebtMap)
        {
            foreach (var item in items)
            {
                debtItems.Add(new DebtItemWithFile { DebtItem = item, FilePath = filePath });
            }
        }

        return debtItems;
    }

    // Helper method to find specific debt item (similar to AnalyzeViewCommand logic)
    private static DebtItemWithFile? FindSpecificDebtItem(
        List<DebtItemWithFile> debtItems,
        string targetFilePath,
        string targetItemId
    )
    {
        // First try exact file path match
        var exactMatch = debtItems.FirstOrDefault(item =>
            string.Equals(item.FilePath, targetFilePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.DebtItem.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
        );

        if (exactMatch != null)
        {
            return exactMatch;
        }

        // Try relative path match (match end of file path)
        var relativeMatch = debtItems.FirstOrDefault(item =>
            item.FilePath.EndsWith(targetFilePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.DebtItem.Id, targetItemId, StringComparison.OrdinalIgnoreCase)
        );

        return relativeMatch;
    }

    private static Dictionary<string, List<TechnicalDebtItem>> ApplyFiltering(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap,
        string? includePattern,
        string? excludePattern,
        string? severityFilter,
        string? tagFilter
    )
    {
        var filteredMap = new Dictionary<string, List<TechnicalDebtItem>>();

        System.Text.RegularExpressions.Regex? includeRegex = null;
        System.Text.RegularExpressions.Regex? excludeRegex = null;
        DebtSeverity? parsedSeverity = null;
        DebtTag? parsedTag = null;

        // Parse filters
        if (!string.IsNullOrWhiteSpace(includePattern))
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                includeRegex = new System.Text.RegularExpressions.Regex(
                    includePattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }
            catch
            {
                // Invalid regex, ignore
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        if (!string.IsNullOrWhiteSpace(excludePattern))
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                excludeRegex = new System.Text.RegularExpressions.Regex(
                    excludePattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }
            catch
            {
                // Invalid regex, ignore
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        if (!string.IsNullOrWhiteSpace(severityFilter))
        {
            Enum.TryParse<DebtSeverity>(severityFilter, true, out var severity);
            parsedSeverity = severity;
        }

        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            Enum.TryParse<DebtTag>(tagFilter, true, out var tag);
            parsedTag = tag;
        }

        foreach (var (filePath, debtItems) in fileDebtMap)
        {
            // Apply file pattern filtering
            if (includeRegex != null && !includeRegex.IsMatch(filePath))
            {
                continue;
            }

            if (excludeRegex != null && excludeRegex.IsMatch(filePath))
            {
                continue;
            }

            // Apply item filtering
            var filteredItems = debtItems
                .Where(item =>
                {
                    if (parsedSeverity.HasValue && item.Severity != parsedSeverity.Value)
                    {
                        return false;
                    }

                    if (parsedTag.HasValue && !item.Tags.Contains(parsedTag.Value))
                    {
                        return false;
                    }

                    return true;
                })
                .ToList();

            filteredMap[filePath] = filteredItems;
        }

        return filteredMap;
    }

    private static List<TechnicalDebtItem> ApplyItemFiltering(
        List<TechnicalDebtItem> items,
        string? severityFilter,
        string? tagFilter
    )
    {
        return
        [
            .. items.Where(item =>
            {
                // Apply severity filtering
                if (
                    !string.IsNullOrWhiteSpace(severityFilter)
                    && Enum.TryParse<DebtSeverity>(severityFilter, true, out var severity)
                    && item.Severity != severity
                )
                {
                    return false;
                }

                // Apply tag filtering
                if (
                    !string.IsNullOrWhiteSpace(tagFilter)
                    && Enum.TryParse<DebtTag>(tagFilter, true, out var tag)
                    && !item.Tags.Contains(tag)
                )
                {
                    return false;
                }

                return true;
            }),
        ];
    }
}

public class TechDebtStatisticsResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public TechDebtFilters Filters { get; set; } = new();
    public TechDebtSummary Summary { get; set; } = new();
    public Dictionary<string, int> SeverityDistribution { get; init; } = [];
    public Dictionary<string, int> TagDistribution { get; init; } = [];
    public Dictionary<string, int> TopFilesByDebtCount { get; init; } = [];
    public Dictionary<string, int> CommonIssues { get; init; } = [];
}

public class TechDebtFilters
{
    public string? IncludePattern { get; set; }
    public string? ExcludePattern { get; set; }
    public string? SeverityFilter { get; set; }
    public string? TagFilter { get; set; }
}

public class TechDebtSummary
{
    public int TotalFilesAnalyzed { get; set; }
    public int FilesDisplayed { get; set; }
    public int TotalDebtItems { get; set; }
    public int FilteredDebtItems { get; set; }
    public int FilesWithDebt { get; set; }
    public int FilteredFilesWithDebt { get; set; }
}

public class RepositoryStatsResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public Dictionary<string, int> FileTypeDistribution { get; init; } = [];
    public int DirectoryCount { get; set; }
}

// Helper service to provide repository path to MCP tools
public interface IRepositoryPathProvider
{
    string RepositoryPath { get; }
}

public class RepositoryPathProvider(string repositoryPath) : IRepositoryPathProvider
{
    public string RepositoryPath { get; } = repositoryPath;
}

public class FileIssuesResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool FileFound { get; set; }
    public IReadOnlyList<TechnicalDebtIssue> Issues { get; init; } = [];
    public FileIssuesSummary Summary { get; set; } = new();
    public TechDebtFilters Filters { get; set; } = new();
}

public class FileIssuesSummary
{
    public int TotalIssues { get; set; }
    public int FilteredIssues { get; set; }
}

public class TechnicalDebtIssue
{
    public string Id { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
}

public class DebtItemResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string DebtId { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string? Error { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DebtItemDetail? DebtItem { get; set; }
    public McpResourceInfo? Resource { get; set; }
}

public class DebtItemDetail
{
    public string Id { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public DateTime Timestamp { get; set; }
    public string ContentHash { get; set; } = string.Empty;
}

public class McpResourceInfo
{
    public Uri Uri { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class DebtItemWithFile
{
    public TechnicalDebtItem DebtItem { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
}

public class RemoveDebtResponse
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string DebtId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public bool ContentDeleted { get; set; }
    public bool MetadataDeleted { get; set; }
    public string? Message { get; set; }
}
