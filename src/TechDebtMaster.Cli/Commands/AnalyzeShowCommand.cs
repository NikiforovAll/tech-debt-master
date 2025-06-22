using System.ComponentModel;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;

namespace TechDebtMaster.Cli.Commands;

public class AnalyzeShowCommand(
    IAnalysisService analysisService,
    IConfigurationService configurationService
) : AsyncCommand<AnalyzeShowSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        AnalyzeShowSettings settings
    )
    {
        ArgumentNullException.ThrowIfNull(settings);

        var repositoryPath = settings.RepositoryPath;
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            // Try to get default from configuration
            var defaultRepo = await configurationService.GetAsync("default.repository");
            repositoryPath = !string.IsNullOrWhiteSpace(defaultRepo)
                ? defaultRepo
                : Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(repositoryPath))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Repository path '{repositoryPath}' does not exist."
            );
            return 1;
        }

        // Determine include/exclude patterns (command line takes priority over defaults)
        var includePattern = settings.IncludePattern;
        if (string.IsNullOrWhiteSpace(includePattern))
        {
            var defaultInclude = await configurationService.GetAsync("default.include");
            includePattern = !string.IsNullOrWhiteSpace(defaultInclude) ? defaultInclude : null;
        }

        var excludePattern = settings.ExcludePattern;
        if (string.IsNullOrWhiteSpace(excludePattern))
        {
            var defaultExclude = await configurationService.GetAsync("default.exclude");
            excludePattern = !string.IsNullOrWhiteSpace(defaultExclude) ? defaultExclude : null;
        }

        // Validate regex patterns before proceeding
        try
        {
            if (!string.IsNullOrWhiteSpace(includePattern))
            {
                _ = new Regex(includePattern, RegexOptions.IgnoreCase);
            }
            if (!string.IsNullOrWhiteSpace(excludePattern))
            {
                _ = new Regex(excludePattern, RegexOptions.IgnoreCase);
            }
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid regex pattern - {ex.Message}");
            return 1;
        }

        // Load analysis report
        var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (analysisReport == null)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No analysis data found for repository:[/] {repositoryPath}"
            );
            AnsiConsole.MarkupLine(
                "[yellow]Run 'analyze debt <path>' first to generate analysis data.[/]"
            );
            return 0;
        }

        // Extract debt items from analysis report
        var fileDebtMap = ExtractDebtItems(analysisReport);

        if (fileDebtMap.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No technical debt items found in analysis results.[/]");
            return 0;
        }

        // Apply filtering if patterns are provided
        var filteredFileDebtMap = ApplyFiltering(
            fileDebtMap,
            settings,
            includePattern,
            excludePattern
        );

        if (filteredFileDebtMap.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files match the specified filters.[/]");
            return 0;
        }

        // Display filtering information
        DisplayFilteringInfo(
            fileDebtMap,
            filteredFileDebtMap,
            settings,
            includePattern,
            excludePattern
        );

        // Build and display tree
        var tree = BuildDebtTree(filteredFileDebtMap);
        AnsiConsole.Write(tree);

        // Display summary statistics
        DisplaySummaryStatistics(filteredFileDebtMap);

        return 0;
    }

    private static Dictionary<string, List<TechnicalDebtItem>> ExtractDebtItems(
        AnalysisReport analysisReport
    )
    {
        var fileDebtMap = new Dictionary<string, List<TechnicalDebtItem>>();
        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        foreach (var (filePath, fileHistory) in analysisReport.FileHistories)
        {
            if (
                fileHistory.Current.AnalysisResults.TryGetValue(
                    TechDebtAnalysisHandler.ResultKey,
                    out var resultObj
                )
            )
            {
                TechDebtAnalysisResult? techDebtResult = null;
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(resultObj, jsonOptions);
                    techDebtResult =
                        System.Text.Json.JsonSerializer.Deserialize<TechDebtAnalysisResult>(
                            json,
                            jsonOptions
                        );
                }
                catch
                {
                    // Ignore deserialization errors
                }

                if (techDebtResult?.Items?.Count > 0)
                {
                    fileDebtMap[filePath] = techDebtResult.Items;
                }
            }
        }

        return fileDebtMap;
    }

    private static Dictionary<string, List<TechnicalDebtItem>> ApplyFiltering(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap,
        AnalyzeShowSettings settings,
        string? includePattern,
        string? excludePattern
    )
    {
        var filteredMap = new Dictionary<string, List<TechnicalDebtItem>>();

        var includeRegex = !string.IsNullOrWhiteSpace(includePattern)
            ? new Regex(includePattern, RegexOptions.IgnoreCase)
            : null;
        var excludeRegex = !string.IsNullOrWhiteSpace(excludePattern)
            ? new Regex(excludePattern, RegexOptions.IgnoreCase)
            : null;

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

            // Apply severity filtering
            var filteredItems = debtItems
                .Where(item =>
                {
                    if (
                        settings.SeverityFilter.HasValue
                        && item.Severity != settings.SeverityFilter.Value
                    )
                    {
                        return false;
                    }

                    // Apply tag filtering
                    if (
                        settings.TagFilter.HasValue && !item.Tags.Contains(settings.TagFilter.Value)
                    )
                    {
                        return false;
                    }

                    return true;
                })
                .ToList();

            if (filteredItems.Count > 0 || settings.ShowAllFiles)
            {
                filteredMap[filePath] = filteredItems;
            }
        }

        return filteredMap;
    }

    private static void DisplayFilteringInfo(
        Dictionary<string, List<TechnicalDebtItem>> original,
        Dictionary<string, List<TechnicalDebtItem>> filtered,
        AnalyzeShowSettings settings,
        string? includePattern,
        string? excludePattern
    )
    {
        var totalOriginalItems = original.Values.Sum(list => list.Count);
        var totalFilteredItems = filtered.Values.Sum(list => list.Count);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[blue]Files analyzed:[/] {original.Count}");
        AnsiConsole.MarkupLine($"[blue]Files displayed:[/] {filtered.Count}");
        AnsiConsole.MarkupLine($"[blue]Total debt items:[/] {totalOriginalItems}");

        if (totalFilteredItems != totalOriginalItems)
        {
            AnsiConsole.MarkupLine($"[blue]Filtered debt items:[/] {totalFilteredItems}");
        }

        if (!string.IsNullOrWhiteSpace(includePattern))
        {
            var source = settings.IncludePattern == includePattern ? "" : " (from default.include)";
            AnsiConsole.MarkupLine(
                $"[yellow]Include pattern{source}:[/] [cyan]{includePattern}[/]"
            );
        }
        if (!string.IsNullOrWhiteSpace(excludePattern))
        {
            var source = settings.ExcludePattern == excludePattern ? "" : " (from default.exclude)";
            AnsiConsole.MarkupLine(
                $"[yellow]Exclude pattern{source}:[/] [cyan]{excludePattern}[/]"
            );
        }
        if (settings.SeverityFilter.HasValue)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Severity filter:[/] [cyan]{settings.SeverityFilter.Value}[/]"
            );
        }
        if (settings.TagFilter.HasValue)
        {
            AnsiConsole.MarkupLine($"[yellow]Tag filter:[/] [cyan]{settings.TagFilter.Value}[/]");
        }

        AnsiConsole.WriteLine();
    }

    private static Tree BuildDebtTree(Dictionary<string, List<TechnicalDebtItem>> fileDebtMap)
    {
        var tree = new Tree("[bold]Technical Debt Analysis[/]");
        var directoryStructure = BuildDirectoryStructure(fileDebtMap);

        // Render tree starting from root directories
        var rootDirs = directoryStructure
            .Values.Where(d => string.IsNullOrEmpty(GetParentPath(d.Path)) || d.Path == ".")
            .OrderBy(d => d.Name);

        foreach (var rootDir in rootDirs)
        {
            RenderDirectory(tree, rootDir, directoryStructure, fileDebtMap);
        }

        return tree;
    }

    private static Dictionary<string, DirectoryNode> BuildDirectoryStructure(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap
    )
    {
        var directories = new Dictionary<string, DirectoryNode>();

        foreach (var filePath in fileDebtMap.Keys)
        {
            var parts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var isFile = i == parts.Length - 1;
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

                if (!isFile)
                {
                    // It's a directory
                    if (!directories.ContainsKey(currentPath))
                    {
                        directories[currentPath] = new DirectoryNode
                        {
                            Path = currentPath,
                            Name = part,
                            Files = [],
                            Subdirectories = [],
                        };
                    }
                }
                else
                {
                    // It's a file
                    var parentDir = i > 0 ? string.Join("/", parts.Take(i)) : "";

                    if (string.IsNullOrEmpty(parentDir))
                    {
                        // File in root
                        if (!directories.ContainsKey("."))
                        {
                            directories["."] = new DirectoryNode
                            {
                                Path = ".",
                                Name = ".",
                                Files = [],
                                Subdirectories = [],
                            };
                        }
                        parentDir = ".";
                    }
                    else if (!directories.ContainsKey(parentDir))
                    {
                        directories[parentDir] = new DirectoryNode
                        {
                            Path = parentDir,
                            Name = parentDir.Split('/')[^1],
                            Files = [],
                            Subdirectories = [],
                        };
                    }

                    directories[parentDir].Files.Add(filePath);
                }
            }
        }

        // Build directory hierarchy
        foreach (var dir in directories.Values)
        {
            var parentPath = GetParentPath(dir.Path);
            if (
                !string.IsNullOrEmpty(parentPath)
                && directories.TryGetValue(parentPath, out var parent)
            )
            {
                parent.Subdirectories.Add(dir.Path);
            }
        }

        return directories;
    }

    private static void RenderDirectory(
        IHasTreeNodes parentNode,
        DirectoryNode dirNode,
        Dictionary<string, DirectoryNode> allDirs,
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap
    )
    {
        // Calculate directory statistics
        var directoryStats = CalculateDirectoryStats(dirNode, allDirs, fileDebtMap);

        var dirName = dirNode.Name == "." ? "[blue].[/]" : $"[blue]{dirNode.Name}/[/]";
        var nodeText =
            directoryStats.TotalItems > 0
                ? $"{dirName} ({directoryStats.TotalItems} debt items)"
                : dirName;

        var dirTreeNode = parentNode.AddNode(nodeText);

        // Add files in this directory
        foreach (var filePath in dirNode.Files.OrderBy(f => f))
        {
            var fileName = Path.GetFileName(filePath);
            var debtItems = fileDebtMap.TryGetValue(filePath, out var items) ? items : [];

            if (debtItems.Count > 0)
            {
                var fileNode = dirTreeNode.AddNode(
                    $"[white]{fileName}[/] ({debtItems.Count} debt items)"
                );

                // Group debt items by tags
                var itemsByTag = debtItems
                    .GroupBy(item => item.Tags.FirstOrDefault(DebtTag.CodeSmell))
                    .OrderBy(g => g.Key);

                foreach (var tagGroup in itemsByTag)
                {
                    var tagName = tagGroup.Key.ToString();
                    var tagItems = tagGroup.ToList();
                    var tagNode = fileNode.AddNode($"[cyan]{tagName}[/] ({tagItems.Count} items)");

                    // Add individual debt items
                    foreach (var item in tagItems.OrderByDescending(i => i.Severity))
                    {
                        var severityColor = GetSeverityColor(item.Severity);
                        tagNode.AddNode($"[{severityColor}]●[/] {item.Id}: {item.Summary}");
                    }
                }
            }
            else
            {
                dirTreeNode.AddNode($"[dim]{fileName}[/] (no debt)");
            }
        }

        // Render subdirectories
        foreach (
            var subDirPath in dirNode.Subdirectories.Where(allDirs.ContainsKey).OrderBy(x => x)
        )
        {
            RenderDirectory(dirTreeNode, allDirs[subDirPath], allDirs, fileDebtMap);
        }
    }

    private static DirectoryStats CalculateDirectoryStats(
        DirectoryNode dirNode,
        Dictionary<string, DirectoryNode> allDirs,
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap
    )
    {
        var stats = new DirectoryStats();

        // Count items in files in this directory
        foreach (var filePath in dirNode.Files)
        {
            if (fileDebtMap.TryGetValue(filePath, out var items))
            {
                stats.TotalItems += items.Count;
                stats.FilesWithDebt++;
            }
            stats.TotalFiles++;
        }

        // Count items in subdirectories
        foreach (var subDirPath in dirNode.Subdirectories)
        {
            if (allDirs.TryGetValue(subDirPath, out var subDir))
            {
                var subStats = CalculateDirectoryStats(subDir, allDirs, fileDebtMap);
                stats.TotalItems += subStats.TotalItems;
                stats.TotalFiles += subStats.TotalFiles;
                stats.FilesWithDebt += subStats.FilesWithDebt;
            }
        }

        return stats;
    }

    private static void DisplaySummaryStatistics(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap
    )
    {
        var allItems = fileDebtMap.Values.SelectMany(items => items).ToList();

        if (allItems.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Summary Statistics[/]");
        AnsiConsole.WriteLine();

        // Severity distribution bar chart
        var severityGroups = allItems.GroupBy(item => item.Severity).OrderByDescending(g => g.Key);

        var severityChart = new BarChart()
            .Width(60)
            .Label("[bold]Severity Distribution[/]")
            .CenterLabel();

        foreach (var group in severityGroups)
        {
            var color = GetSeverityColorEnum(group.Key);
            severityChart.AddItem(group.Key.ToString(), group.Count(), color);
        }

        AnsiConsole.Write(severityChart);
        AnsiConsole.WriteLine();

        // Tag distribution bar chart
        var tagGroups = allItems
            .SelectMany(item => item.Tags)
            .GroupBy(tag => tag)
            .OrderByDescending(g => g.Count())
            .Take(10) // Show top 10 tags
            .ToList();

        if (tagGroups.Count > 0)
        {
            var tagChart = new BarChart()
                .Width(60)
                .Label("[bold]Tag Distribution (Top 10)[/]")
                .CenterLabel();

            // Define colors for tags to provide variety
            var tagColors = new[]
            {
                Color.Cyan1,
                Color.Green,
                Color.Yellow,
                Color.Magenta1,
                Color.Orange1,
                Color.Purple,
                Color.Turquoise2,
                Color.Pink1,
                Color.LightGreen,
                Color.Gold1,
            };

            for (int i = 0; i < tagGroups.Count; i++)
            {
                var group = tagGroups[i];
                var color = tagColors[i % tagColors.Length];
                tagChart.AddItem(group.Key.ToString(), group.Count(), color);
            }

            AnsiConsole.Write(tagChart);
        }
    }

    private static string GetParentPath(string path)
    {
        if (path == "." || !path.Contains('/', StringComparison.Ordinal))
        {
            return "";
        }

        var lastSlash = path.LastIndexOf('/');
        return lastSlash > 0 ? path.Substring(0, lastSlash) : "";
    }

    private static string GetSeverityColor(DebtSeverity severity)
    {
        return severity switch
        {
            DebtSeverity.Critical => "red",
            DebtSeverity.High => "orange3",
            DebtSeverity.Medium => "yellow",
            DebtSeverity.Low => "green",
            _ => "gray",
        };
    }

    private static Color GetSeverityColorEnum(DebtSeverity severity)
    {
        return severity switch
        {
            DebtSeverity.Critical => Color.Red,
            DebtSeverity.High => Color.Orange3,
            DebtSeverity.Medium => Color.Yellow,
            DebtSeverity.Low => Color.Green,
            _ => Color.Grey,
        };
    }

    private sealed class DirectoryNode
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> Files { get; set; } = [];
        public List<string> Subdirectories { get; set; } = [];
    }

    private sealed class DirectoryStats
    {
        public int TotalItems { get; set; }
        public int TotalFiles { get; set; }
        public int FilesWithDebt { get; set; }
    }
}

public class AnalyzeShowSettings : CommandSettings
{
    [Description("Path to the repository (optional, uses default.repository or current directory)")]
    [CommandArgument(0, "[REPOSITORY_PATH]")]
    public string? RepositoryPath { get; init; }

    [Description(
        "Regex pattern to include files (only files matching this pattern will be displayed)"
    )]
    [CommandOption("--include")]
    public string? IncludePattern { get; init; }

    [Description("Regex pattern to exclude files (files matching this pattern will be hidden)")]
    [CommandOption("--exclude")]
    public string? ExcludePattern { get; init; }

    [Description("Filter by specific severity level (Critical, High, Medium, Low)")]
    [CommandOption("--severity")]
    public DebtSeverity? SeverityFilter { get; init; }

    [Description("Filter by specific debt tag (CodeSmell, Naming, Performance, etc.)")]
    [CommandOption("--tag")]
    public DebtTag? TagFilter { get; init; }

    [Description("Show all analyzed files, including those with no debt")]
    [CommandOption("--all")]
    public bool ShowAllFiles { get; init; }
}
