using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class AnalyzeCommand(Kernel kernel, IRepositoryIndexService indexService)
    : AsyncCommand<AnalyzeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Directory.Exists(settings.RepositoryPath))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Repository path '{settings.RepositoryPath}' does not exist."
            );
            return 1;
        }

        // Validate regex patterns before proceeding
        try
        {
            if (!string.IsNullOrWhiteSpace(settings.IncludePattern))
            {
                _ = new Regex(settings.IncludePattern, RegexOptions.IgnoreCase);
            }
            if (!string.IsNullOrWhiteSpace(settings.ExcludePattern))
            {
                _ = new Regex(settings.ExcludePattern, RegexOptions.IgnoreCase);
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

        AnsiConsole.MarkupLine($"[green]Analyzing repository:[/] {settings.RepositoryPath}");

        // Display filtering information if patterns are provided
        if (!string.IsNullOrWhiteSpace(settings.IncludePattern))
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Include pattern:[/] [cyan]{settings.IncludePattern}[/] (only files matching this pattern will be analyzed)"
            );
        }
        if (!string.IsNullOrWhiteSpace(settings.ExcludePattern))
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Exclude pattern:[/] [cyan]{settings.ExcludePattern}[/] (files matching this pattern will be skipped)"
            );
        }

        if (
            !string.IsNullOrWhiteSpace(settings.IncludePattern)
            || !string.IsNullOrWhiteSpace(settings.ExcludePattern)
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
                            settings.RepositoryPath,
                            settings.IncludePattern,
                            settings.ExcludePattern
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

            // Create and display statistics table
            var table = new Table();
            table.AddColumn("[bold]Metric[/]");
            table.AddColumn("[bold]Value[/]");
            table.Border(TableBorder.Rounded);

            var summary = indexResult.ChangeSummary;
            table.AddRow("Total files", summary.TotalFiles.ToString(CultureInfo.InvariantCulture));

            if (indexResult.HasChanges)
            {
                table.AddRow("[green]New files[/]", $"[green]{summary.NewFiles.Count}[/]");
                table.AddRow(
                    "[yellow]Changed files[/]",
                    $"[yellow]{summary.ChangedFiles.Count}[/]"
                );
                table.AddRow("[red]Deleted files[/]", $"[red]{summary.DeletedFiles.Count}[/]");
            }
            else
            {
                table.AddRow("[dim]Changes detected[/]", "[dim]None[/]");
            }

            // Add analysis statistics to table if available
            if (indexResult.AnalysisReport != null)
            {
                var totalAnalyzed = indexResult.AnalysisReport.FileHistories.Count;
                var changedAnalyses = indexResult.AnalysisReport.FileHistories.Count(kv =>
                    kv.Value.Previous != null
                    && kv.Value.Current.Preview != kv.Value.Previous.Preview
                );

                table.AddRow(
                    "Files analyzed",
                    totalAnalyzed.ToString(CultureInfo.InvariantCulture)
                );
                if (changedAnalyses > 0)
                {
                    table.AddRow("[yellow]Content changes[/]", $"[yellow]{changedAnalyses}[/]");
                }
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

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
    [Description("Path to the repository to analyze")]
    [CommandArgument(0, "<REPOSITORY_PATH>")]
    public string RepositoryPath { get; init; } = string.Empty;

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
