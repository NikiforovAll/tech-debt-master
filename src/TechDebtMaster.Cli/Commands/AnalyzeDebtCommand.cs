using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;

namespace TechDebtMaster.Cli.Commands;

public class AnalyzeDebtCommand(
    IIndexStorageService storageService,
    IAnalysisService analysisService,
    IRepomixParser repomixParser,
    IProcessRunner processRunner,
    IConfigurationService configurationService
) : AsyncCommand<AnalyzeDebtSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        AnalyzeDebtSettings settings
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

        // Load latest index data
        var indexData = await storageService.LoadLatestIndexAsync(repositoryPath);
        if (indexData == null)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No index data found for repository:[/] {repositoryPath}"
            );
            AnsiConsole.MarkupLine(
                "[yellow]Run 'analyze index <path>' first to generate index data.[/]"
            );
            return 0;
        }

        // Determine which files to analyze
        List<string> filesToAnalyze;

        if (settings.LatestOnly)
        {
            // Only analyze changed files
            filesToAnalyze = [.. indexData.Summary.ChangedFiles, .. indexData.Summary.NewFiles];

            if (filesToAnalyze.Count == 0)
            {
                AnsiConsole.MarkupLine($"[green]No changes to analyze in:[/] {repositoryPath}");
                AnsiConsole.MarkupLine(
                    $"[blue]Last indexed:[/] {indexData.Timestamp:yyyy-MM-dd HH:mm:ss} UTC"
                );
                return 0;
            }

            AnsiConsole.MarkupLine(
                $"[green]Analyzing debt for changed files in:[/] {repositoryPath}"
            );
        }
        else
        {
            // Analyze all files from the index
            filesToAnalyze = [.. indexData.Files.Keys];

            if (filesToAnalyze.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No files found in index for:[/] {repositoryPath}");
                AnsiConsole.MarkupLine(
                    $"[blue]Last indexed:[/] {indexData.Timestamp:yyyy-MM-dd HH:mm:ss} UTC"
                );
                return 0;
            }

            AnsiConsole.MarkupLine($"[green]Analyzing debt for all files in:[/] {repositoryPath}");
        }

        // Apply filtering if patterns are provided
        var originalCount = filesToAnalyze.Count;
        if (
            !string.IsNullOrWhiteSpace(includePattern) || !string.IsNullOrWhiteSpace(excludePattern)
        )
        {
            var includeRegex = !string.IsNullOrWhiteSpace(includePattern)
                ? new Regex(includePattern, RegexOptions.IgnoreCase)
                : null;
            var excludeRegex = !string.IsNullOrWhiteSpace(excludePattern)
                ? new Regex(excludePattern, RegexOptions.IgnoreCase)
                : null;

            filesToAnalyze =
            [
                .. filesToAnalyze.Where(file =>
                {
                    // If include pattern is specified, file must match it
                    if (includeRegex != null && !includeRegex.IsMatch(file))
                    {
                        return false;
                    }

                    // If exclude pattern is specified, file must not match it
                    if (excludeRegex != null && excludeRegex.IsMatch(file))
                    {
                        return false;
                    }

                    return true;
                }),
            ];

            // Display filtering information
            AnsiConsole.WriteLine();
            if (!string.IsNullOrWhiteSpace(includePattern))
            {
                var source =
                    settings.IncludePattern == includePattern ? "" : " (from default.include)";
                AnsiConsole.MarkupLine(
                    $"[yellow]Include pattern{source}:[/] [cyan]{includePattern}[/]"
                );
            }
            if (!string.IsNullOrWhiteSpace(excludePattern))
            {
                var source =
                    settings.ExcludePattern == excludePattern ? "" : " (from default.exclude)";
                AnsiConsole.MarkupLine(
                    $"[yellow]Exclude pattern{source}:[/] [cyan]{excludePattern}[/]"
                );
            }
            AnsiConsole.MarkupLine(
                $"[blue]Files after filtering:[/] {filesToAnalyze.Count} (from {originalCount})"
            );

            if (filesToAnalyze.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No files match the specified patterns.[/]");
                return 0;
            }
        }

        AnsiConsole.MarkupLine(
            $"[blue]Based on index from:[/] {indexData.Timestamp:yyyy-MM-dd HH:mm:ss} UTC"
        );
        AnsiConsole.MarkupLine($"[blue]Files to analyze:[/] {filesToAnalyze.Count}");

        // First, get the file contents from repomix
        var filesToAnalyzeMap = new Dictionary<string, string>();
        await AnsiConsole
            .Status()
            .StartAsync(
                "Scanning repository files...",
                async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    var repomixOutput = await processRunner.RunRepomixAsync(repositoryPath);
                    var parsedData = repomixParser.ParseXmlOutput(repomixOutput);

                    // Prepare files for analysis
                    foreach (
                        var path in filesToAnalyze.Where(path => parsedData.Files.ContainsKey(path))
                    )
                    {
                        filesToAnalyzeMap[path] = parsedData.Files[path].Content;
                    }
                }
            );

        if (filesToAnalyzeMap.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files were found to analyze.[/]");
            return 0;
        }

        // Now analyze files one by one with progress display
        var debtItemsFound = new List<(string FilePath, TechnicalDebtItem Item)>();
        var analyzedCount = 0;
        var filesWithDebt = 0;
        var failedCount = 0;

        var totalFiles = filesToAnalyzeMap.Count;
        var currentFileIndex = 0;

        foreach (var (filePath, content) in filesToAnalyzeMap.OrderBy(kvp => kvp.Key))
        {
            currentFileIndex++;
            try
            {
                await AnsiConsole
                    .Status()
                    .StartAsync(
                        $"[[{currentFileIndex}/{totalFiles}]] Analyzing {Path.GetFileName(filePath)}...",
                        async ctx =>
                        {
                            ctx.Spinner(Spinner.Known.Dots);
                            ctx.SpinnerStyle(Style.Parse("cyan"));

                            // Analyze single file
                            var singleFileDict = new Dictionary<string, string>
                            {
                                { filePath, content },
                            };
                            var singleFileReport = await analysisService.AnalyzeChangedFilesAsync(
                                singleFileDict,
                                repositoryPath
                            );

                            analyzedCount++;

                            if (
                                singleFileReport?.FileHistories.TryGetValue(
                                    filePath,
                                    out var history
                                ) == true
                            )
                            {
                                if (
                                    history.Current.AnalysisResults.TryGetValue(
                                        TechDebtAnalysisHandler.ResultKey,
                                        out var resultObj
                                    )
                                    && resultObj is TechDebtAnalysisResult techDebtResult
                                    && techDebtResult.Items?.Count > 0
                                )
                                {
                                    filesWithDebt++;
                                    foreach (var item in techDebtResult.Items)
                                    {
                                        debtItemsFound.Add((filePath, item));
                                    }

                                    // Display debt items for this file immediately
                                    AnsiConsole.MarkupLine(
                                        $"[green]✓[/] {filePath} - [yellow]{techDebtResult.Items.Count} debt item(s) found[/]"
                                    );
                                    foreach (var item in techDebtResult.Items)
                                    {
                                        var severityColor = GetSeverityColor(item.Severity);
                                        var tagsText =
                                            item.Tags.Length > 0
                                                ? $" [dim]({string.Join(", ", item.Tags)})[/]"
                                                : "";
                                        AnsiConsole.MarkupLine(
                                            $"    [{severityColor}] • [/] [bold]{item.Id}[/]: {item.Summary.EscapeMarkup()}{tagsText}"
                                        );
                                    }
                                }
                                else
                                {
                                    AnsiConsole.MarkupLine(
                                        $"[green]✓[/] {filePath} - [green]No debt found[/]"
                                    );
                                }
                            }
                            else
                            {
                                AnsiConsole.MarkupLine(
                                    $"[yellow]⚠[/] {filePath} - [dim]Skipped[/]"
                                );
                            }
                        }
                    );
            }
            catch (XmlException)
            {
                failedCount++;
                AnsiConsole.MarkupLine($"[red]✗[/] {filePath} - [red]XML parsing failed[/]");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓[/] Analysis completed!");
        AnsiConsole.MarkupLine($"[blue]Files analyzed:[/] {analyzedCount}");
        AnsiConsole.MarkupLine($"[blue]Files with debt:[/] {filesWithDebt}");
        if (failedCount > 0)
        {
            AnsiConsole.MarkupLine($"[red]Files failed:[/] {failedCount}");
        }
        AnsiConsole.MarkupLine($"[blue]Total debt items:[/] {debtItemsFound.Count}");
        AnsiConsole.WriteLine();

        return 0;
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
}

public class AnalyzeDebtSettings : CommandSettings
{
    [Description("Path to the repository (optional, uses default.repository or current directory)")]
    [CommandArgument(0, "[REPOSITORY_PATH]")]
    public string? RepositoryPath { get; init; }

    [Description("Only analyze changed files since last index (instead of all files)")]
    [CommandOption("--latest")]
    public bool LatestOnly { get; init; }

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
