using System.ComponentModel;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class AnalyzeIndexCommand(
    IRepositoryIndexService indexService,
    IConfigurationService configurationService
) : AsyncCommand<AnalyzeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Determine repository path
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
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Regex Tips:[/]");
            AnsiConsole.MarkupLine(
                "• Use [cyan]\\\\[/] to escape special characters like [cyan].[/] [cyan]*[/] [cyan]+[/] [cyan]?[/] [cyan]([/] [cyan])[/] [cyan][[/] [cyan]][/]"
            );
            AnsiConsole.MarkupLine(
                "• Use [cyan]$[/] to match end of filename: [cyan]\"\\.cs$\"[/] for C# files"
            );
            AnsiConsole.MarkupLine("• Use [cyan]|[/] for OR: [cyan]\"(Controllers|Services)/\"[/]");
            AnsiConsole.MarkupLine(
                "• Use [cyan].*[/] to match any characters: [cyan]\"src/.*\\.js$\"[/]"
            );
            AnsiConsole.MarkupLine("• Pattern matching is case-insensitive");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Analyzing repository:[/] {repositoryPath}");

        // Display filtering information if patterns are provided
        if (!string.IsNullOrWhiteSpace(includePattern))
        {
            var source = settings.IncludePattern == includePattern ? "" : " (from default.include)";
            AnsiConsole.MarkupLine(
                $"[yellow]Include pattern{source}:[/] [cyan]{includePattern}[/] (only files matching this pattern will be analyzed)"
            );
        }
        if (!string.IsNullOrWhiteSpace(excludePattern))
        {
            var source = settings.ExcludePattern == excludePattern ? "" : " (from default.exclude)";
            AnsiConsole.MarkupLine(
                $"[yellow]Exclude pattern{source}:[/] [cyan]{excludePattern}[/] (files matching this pattern will be skipped)"
            );
        }

        if (
            !string.IsNullOrWhiteSpace(includePattern) || !string.IsNullOrWhiteSpace(excludePattern)
        )
        {
            AnsiConsole.WriteLine();
        }

        IndexResult? indexResult = null;

        try
        {
            await AnsiConsole
                .Status()
                .StartAsync(
                    "Analyzing repository...",
                    async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Star);
                        ctx.SpinnerStyle(Style.Parse("green"));

                        indexResult = await indexService.IndexRepositoryAsync(
                            repositoryPath,
                            includePattern,
                            excludePattern
                        );
                    }
                );

            if (indexResult == null)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Failed to analyze repository.");
                return 1;
            }

            if (string.IsNullOrEmpty(indexResult.FileSummary))
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Warning:[/] No file_summary section found in repomix output."
                );
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Repository analyzed successfully!");

            // Display message if no changes detected
            if (!indexResult.HasChanges)
            {
                AnsiConsole.MarkupLine("[dim]No changes detected since last analysis.[/]");
            }

            AnsiConsole.WriteLine();

            // Display filtering results if filtering was applied
            if (indexResult.FilteringStats?.WasFiltered == true)
            {
                var stats = indexResult.FilteringStats;
                AnsiConsole.MarkupLine($"[blue]Filtering Results:[/]");
                AnsiConsole.MarkupLine($"  Total files found: [white]{stats.TotalFiles}[/]");
                AnsiConsole.MarkupLine($"  Files analyzed: [green]{stats.FilteredFiles}[/]");
                if (stats.ExcludedFiles > 0)
                {
                    AnsiConsole.MarkupLine($"  Files excluded: [yellow]{stats.ExcludedFiles}[/]");
                }
                AnsiConsole.WriteLine();
            }
            var summary = indexResult.ChangeSummary;

            // Create and display repository structure tree
            if (indexResult.HasChanges)
            {
                var tree = new Tree("[bold]Repository Structure[/]");
                BuildFileTree(tree, summary);
                AnsiConsole.Write(tree);
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]No file changes to display.[/]");
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static void BuildFileTree(Tree tree, IndexSummary summary)
    {
        var filesByDirectory = new Dictionary<string, DirectoryInfo>();

        // Process all changed files
        foreach (
            var file in summary.NewFiles.Concat(summary.ChangedFiles).Concat(summary.DeletedFiles)
        )
        {
            var parts = file.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var isFile = i == parts.Length - 1;
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

                if (!isFile)
                {
                    // It's a directory
                    if (!filesByDirectory.ContainsKey(currentPath))
                    {
                        filesByDirectory[currentPath] = new DirectoryInfo
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
                        if (!filesByDirectory.ContainsKey("."))
                        {
                            filesByDirectory["."] = new DirectoryInfo
                            {
                                Path = ".",
                                Name = ".",
                                Files = [],
                                Subdirectories = [],
                            };
                        }
                        parentDir = ".";
                    }
                    else if (!filesByDirectory.ContainsKey(parentDir))
                    {
                        filesByDirectory[parentDir] = new DirectoryInfo
                        {
                            Path = parentDir,
                            Name = parentDir.Split('/').Last(),
                            Files = [],
                            Subdirectories = [],
                        };
                    }

                    var fileStatus =
                        summary.NewFiles.Contains(file) ? "new"
                        : summary.ChangedFiles.Contains(file) ? "changed"
                        : "deleted";

                    filesByDirectory[parentDir]
                        .Files.Add(new FileItem { Name = part, Status = fileStatus });
                }
            }
        }

        // Build directory hierarchy
        foreach (var dir in filesByDirectory.Values)
        {
            var parentPath = GetParentPath(dir.Path);
            if (
                !string.IsNullOrEmpty(parentPath)
                && filesByDirectory.TryGetValue(parentPath, out var value)
            )
            {
                value.Subdirectories.Add(dir.Path);
            }
        }

        // Render tree starting from root directories
        var rootDirs = filesByDirectory
            .Values.Where(d => string.IsNullOrEmpty(GetParentPath(d.Path)) || d.Path == ".")
            .OrderBy(d => d.Name);

        foreach (var rootDir in rootDirs)
        {
            RenderDirectory(tree, rootDir, filesByDirectory);
        }
    }

    private static void RenderDirectory(
        IHasTreeNodes parentNode,
        DirectoryInfo dirInfo,
        Dictionary<string, DirectoryInfo> allDirs
    )
    {
        var newCount = dirInfo.Files.Count(f => f.Status == "new");
        var changedCount = dirInfo.Files.Count(f => f.Status == "changed");
        var deletedCount = dirInfo.Files.Count(f => f.Status == "deleted");

        // Count changes in subdirectories
        foreach (var subDirPath in dirInfo.Subdirectories)
        {
            if (allDirs.TryGetValue(subDirPath, out var subDir))
            {
                newCount += CountFilesRecursive(subDir, allDirs, "new");
                changedCount += CountFilesRecursive(subDir, allDirs, "changed");
                deletedCount += CountFilesRecursive(subDir, allDirs, "deleted");
            }
        }

        var counters = new List<string>();
        if (newCount > 0)
        {
            counters.Add($"[green]{newCount} new[/]");
        }

        if (changedCount > 0)
        {
            counters.Add($"[yellow]{changedCount} changed[/]");
        }

        if (deletedCount > 0)
        {
            counters.Add($"[red]{deletedCount} deleted[/]");
        }

        var dirName = dirInfo.Name == "." ? "[blue].[/]" : $"[blue]{dirInfo.Name}/[/]";
        var nodeText = counters.Count != 0 ? $"{dirName} ({string.Join(", ", counters)})" : dirName;

        var dirNode = parentNode.AddNode(nodeText);

        // Add files in this directory
        foreach (var file in dirInfo.Files.OrderBy(f => f.Name))
        {
            var color = file.Status switch
            {
                "new" => "green",
                "changed" => "yellow",
                "deleted" => "red",
                _ => "white",
            };
            dirNode.AddNode($"[{color}]{file.Name}[/]");
        }

        foreach (
            var subDirPath in dirInfo.Subdirectories.Where(allDirs.ContainsKey).OrderBy(x => x)
        )
        {
            RenderDirectory(dirNode, allDirs[subDirPath], allDirs);
        }
    }

    private static int CountFilesRecursive(
        DirectoryInfo dir,
        Dictionary<string, DirectoryInfo> allDirs,
        string status
    )
    {
        var count = dir.Files.Count(f => f.Status == status);
        foreach (var subDirPath in dir.Subdirectories)
        {
            if (allDirs.TryGetValue(subDirPath, out var value))
            {
                count += CountFilesRecursive(value, allDirs, status);
            }
        }
        return count;
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

    private sealed class DirectoryInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<FileItem> Files { get; set; } = [];
        public List<string> Subdirectories { get; set; } = [];
    }

    private sealed class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}

public class AnalyzeSettings : CommandSettings
{
    [Description(
        "Path to the repository to analyze (optional, uses default.repository or current directory)"
    )]
    [CommandArgument(0, "[REPOSITORY_PATH]")]
    public string? RepositoryPath { get; init; }

    [Description(
        "Regex pattern to include files (only files matching this pattern will be analyzed)\n"
            + "Examples:\n"
            + "  --include \"\\.cs$\"           # Only C# files\n"
            + "  --include \"src/.*\\.js$\"     # Only JS files in src directory\n"
            + "  --include \"(Controllers|Services)/.*\\.cs$\"  # Only C# files in Controllers or Services folders"
    )]
    [CommandOption("--include")]
    public string? IncludePattern { get; init; }

    [Description(
        "Regex pattern to exclude files (files matching this pattern will be skipped)\n"
            + "Examples:\n"
            + "  --exclude \"\\.min\\.(js|css)$\"  # Exclude minified files\n"
            + "  --exclude \"test.*\\.cs$\"       # Exclude test files\n"
            + "  --exclude \"(bin|obj|node_modules)/\"  # Exclude build/dependency folders\n"
            + "  --exclude \"\\.(log|tmp|cache)$\"      # Exclude temporary files"
    )]
    [CommandOption("--exclude")]
    public string? ExcludePattern { get; init; }
}
