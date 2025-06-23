using System.ComponentModel;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
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

        // Create panel components
        var filesAnalyzedPanel = CreateFilesAnalyzedPanel(
            fileDebtMap,
            filteredFileDebtMap,
            settings,
            includePattern,
            excludePattern
        );

        var summaryStatisticsPanel = CreateSummaryStatisticsPanel(filteredFileDebtMap);
        var technicalDebtAnalysisPanel = CreateTechnicalDebtAnalysisPanel(filteredFileDebtMap);

        // Create top row with two columns side by side
        var topRow = new Columns(filesAnalyzedPanel, summaryStatisticsPanel);

        // Create complete layout using Rows to stack naturally without blank space
        var layout = new Rows(topRow, technicalDebtAnalysisPanel);

        // Display the complete layout
        AnsiConsole.Write(layout);

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
#pragma warning disable CA1031 // Do not catch general exception types
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
#pragma warning restore CA1031 // Do not catch general exception types

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

    private static Panel CreateFilesAnalyzedPanel(
        Dictionary<string, List<TechnicalDebtItem>> original,
        Dictionary<string, List<TechnicalDebtItem>> filtered,
        AnalyzeShowSettings settings,
        string? includePattern,
        string? excludePattern
    )
    {
        var totalOriginalItems = original.Values.Sum(list => list.Count);
        var totalFilteredItems = filtered.Values.Sum(list => list.Count);

        var content = new List<string>
        {
            $"[blue]Files analyzed:[/] {original.Count}",
            $"[blue]Files displayed:[/] {filtered.Count}",
            $"[blue]Total debt items:[/] {totalOriginalItems}",
        };

        if (totalFilteredItems != totalOriginalItems)
        {
            content.Add($"[blue]Filtered debt items:[/] {totalFilteredItems}");
        }

        if (!string.IsNullOrWhiteSpace(includePattern))
        {
            var source = settings.IncludePattern == includePattern ? "" : " (from default.include)";
            content.Add($"[blue]Include pattern{source}:[/] [white]{includePattern}[/]");
        }
        if (!string.IsNullOrWhiteSpace(excludePattern))
        {
            var source = settings.ExcludePattern == excludePattern ? "" : " (from default.exclude)";
            content.Add($"[blue]Exclude pattern{source}:[/] [white]{excludePattern}[/]");
        }
        if (settings.SeverityFilter.HasValue)
        {
            content.Add($"[blue]Severity filter:[/] [white]{settings.SeverityFilter.Value}[/]");
        }
        if (settings.TagFilter.HasValue)
        {
            content.Add($"[blue]Tag filter:[/] [white]{settings.TagFilter.Value}[/]");
        }

        return new Panel(string.Join("\n", content))
            .Header("[bold]Files Analyzed[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue)
            .Expand();
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
        foreach (var dir in directories.Values.Select(d => d.Path))
        {
            var parentPath = GetParentPath(dir);
            if (
                !string.IsNullOrEmpty(parentPath)
                && directories.TryGetValue(parentPath, out var parent)
            )
            {
                parent.Subdirectories.Add(dir);
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
                    var tagNode = fileNode.AddNode($"[blue]{tagName}[/] ({tagItems.Count} items)");

                    // Add individual debt items
                    foreach (var item in tagItems.OrderByDescending(i => i.Severity))
                    {
                        tagNode.AddNode($"{ConsoleFormattingUtility.FormatSeverityWithColor(item.Severity)} {item.Id}: {item.Summary}");
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

    private static Panel CreateSummaryStatisticsPanel(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap
    )
    {
        var allItems = fileDebtMap.Values.SelectMany(items => items).ToList();

        if (allItems.Count == 0)
        {
            return new Panel("[dim]No debt items found[/]")
                .Header("[bold]Summary Statistics[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green)
                .Expand();
        }

        var renderables = new List<IRenderable>();

        // Severity distribution bar chart
        var severityGroups = allItems.GroupBy(item => item.Severity).OrderByDescending(g => g.Key);

        var severityChart = new BarChart()
            .Width(50)
            .Label("[bold]Severity Distribution[/]")
            .CenterLabel();

        foreach (var group in severityGroups)
        {
            var color = ConsoleFormattingUtility.GetSeverityColorEnum(group.Key);
            severityChart.AddItem(group.Key.ToString(), group.Count(), color);
        }

        renderables.Add(severityChart);

        // Tag distribution bar chart
        var tagGroups = allItems
            .SelectMany(item => item.Tags)
            .GroupBy(tag => tag)
            .OrderByDescending(g => g.Count())
            .Take(10) // Show top 10 tags
            .ToList();

        if (tagGroups.Count > 0)
        {
            renderables.Add(new Text("")); // Spacing

            var tagChart = new BarChart()
                .Width(50)
                .Label("[bold]Tag Distribution (Top 10)[/]")
                .CenterLabel();

            // Define harmonious colors for tags - professional palette
            var tagColors = new[]
            {
                Color.Blue,
                Color.Grey,
                Color.DarkBlue,
                Color.SlateBlue1,
                Color.SteelBlue1,
            };

            for (int i = 0; i < tagGroups.Count; i++)
            {
                var group = tagGroups[i];
                var color = tagColors[i % tagColors.Length];
                tagChart.AddItem(group.Key.ToString(), group.Count(), color);
            }

            renderables.Add(tagChart);
        }

        var rows = new Rows(renderables);
        return new Panel(rows)
            .Header("[bold]Summary Statistics[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue)
            .Expand();
    }

    private static Panel CreateTechnicalDebtAnalysisPanel(
        Dictionary<string, List<TechnicalDebtItem>> fileDebtMap
    )
    {
        var tree = BuildDebtTree(fileDebtMap);
        return new Panel(tree)
            .Header("[bold]Technical Debt Analysis[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue)
            .Expand();
    }

    private static string GetParentPath(string path)
    {
        if (path == "." || !path.Contains('/', StringComparison.Ordinal))
        {
            return "";
        }

        var lastSlash = path.LastIndexOf('/');
        return lastSlash > 0 ? path[..lastSlash] : "";
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
