using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;

namespace TechDebtMaster.Cli.Commands;

public class AnalyzeDebtCommand(
    IIndexStorageService storageService,
    IAnalysisService analysisService,
    IRepomixParser repomixParser
) : AsyncCommand<AnalyzeDebtSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        AnalyzeDebtSettings settings
    )
    {
        ArgumentNullException.ThrowIfNull(settings);

        var repositoryPath = string.IsNullOrWhiteSpace(settings.RepositoryPath)
            ? Directory.GetCurrentDirectory()
            : settings.RepositoryPath;

        if (!Directory.Exists(repositoryPath))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Repository path '{repositoryPath}' does not exist."
            );
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

        // Check if there are any changes to analyze
        var changesToAnalyze = indexData
            .Summary.ChangedFiles.Concat(indexData.Summary.NewFiles)
            .ToList();

        if (changesToAnalyze.Count == 0)
        {
            AnsiConsole.MarkupLine($"[green]No changes to analyze in:[/] {repositoryPath}");
            AnsiConsole.MarkupLine(
                $"[blue]Last indexed:[/] {indexData.Timestamp:yyyy-MM-dd HH:mm:ss} UTC"
            );
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]Analyzing debt for:[/] {repositoryPath}");
        AnsiConsole.MarkupLine(
            $"[blue]Based on index from:[/] {indexData.Timestamp:yyyy-MM-dd HH:mm:ss} UTC"
        );

        AnalysisReport? analysisReport = null;

        await AnsiConsole
            .Status()
            .StartAsync(
                "Running debt analysis on changed files...",
                async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    // Re-run repomix to get current file contents
                    ctx.Status("Scanning repository files...");
                    var repomixOutput = await RunRepomixAsync(repositoryPath);
                    var parsedData = repomixParser.ParseXmlOutput(repomixOutput);

                    // Prepare files for analysis (changed and new files only)
                    var filesToAnalyze = new Dictionary<string, string>();
                    foreach (
                        var path in changesToAnalyze.Where(path =>
                            parsedData.Files.ContainsKey(path)
                        )
                    )
                    {
                        filesToAnalyze[path] = parsedData.Files[path].Content;
                    }

                    if (filesToAnalyze.Count != 0)
                    {
                        ctx.Status($"Analyzing {filesToAnalyze.Count} changed files...");
                        analysisReport = await analysisService.AnalyzeChangedFilesAsync(
                            filesToAnalyze,
                            repositoryPath
                        );
                    }
                }
            );

        if (analysisReport == null)
        {
            AnsiConsole.MarkupLine("[yellow]No files were analyzed.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Debt analysis completed!");
        AnsiConsole.WriteLine();

        // Display technical debt items found
        DisplayTechnicalDebtItems(analysisReport, changesToAnalyze);

        // Show analyzed files
        if (analysisReport.FileHistories.Count != 0)
        {
            AnsiConsole.MarkupLine("[bold]Files Analyzed:[/]");
            var fileTree = new Tree("[blue]Repository[/]");

            foreach (
                var (filePath, history) in analysisReport
                    .FileHistories.Where(h => changesToAnalyze.Contains(h.Key))
                    .OrderBy(kv => kv.Key)
            )
            {
                var status = history.Previous == null ? "[green]new[/]" : "[yellow]changed[/]";
                var preview = GetPreviewFromResults(history.Current);

                fileTree.AddNode($"{status} {filePath} [dim]- {preview}[/]");
            }

            AnsiConsole.Write(fileTree);
        }

        return 0;
    }

    private static async Task<string> RunRepomixAsync(string repositoryPath)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "repomix.cmd";
            process.StartInfo.Arguments = $"--stdout --style xml \"{repositoryPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = errorBuilder.ToString();
                throw new InvalidOperationException($"Repomix failed with error: {error}");
            }

            return outputBuilder.ToString();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Unexpected error running repomix: {ex.Message}",
                ex
            );
        }
    }

    private static void DisplayTechnicalDebtItems(
        AnalysisReport analysisReport,
        List<string> changesToAnalyze
    )
    {
        var debtItemsFound = new List<(string FilePath, TechnicalDebtItem Item)>();

        // Extract debt items from analysis results
        foreach (
            var (filePath, history) in analysisReport
                .FileHistories.Where(h => changesToAnalyze.Contains(h.Key))
                .OrderBy(kv => kv.Key)
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
                foreach (var item in techDebtResult.Items)
                {
                    debtItemsFound.Add((filePath, item));
                }
            }
        }

        if (debtItemsFound.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No technical debt found in analyzed files![/]");
            AnsiConsole.WriteLine();
            return;
        }

        // Display debt items grouped by file
        AnsiConsole.MarkupLine(
            $"[bold]Technical Debt Found ({debtItemsFound.Count} item{(debtItemsFound.Count == 1 ? "" : "s")}):[/]"
        );
        AnsiConsole.WriteLine();

        var fileGroups = debtItemsFound.GroupBy(x => x.FilePath).OrderBy(g => g.Key);

        foreach (var fileGroup in fileGroups)
        {
            AnsiConsole.MarkupLine($"[blue]{fileGroup.Key}[/]");

            foreach (var (_, item) in fileGroup)
            {
                var severityColor = GetSeverityColor(item.Severity);
                var tagsText =
                    item.Tags.Length > 0 ? $" [dim]({string.Join(", ", item.Tags)})[/]" : "";

                AnsiConsole.MarkupLine(
                    $"  [{severityColor}]●[/] [bold]{item.Id}[/]: {item.Summary}{tagsText}"
                );
            }

            AnsiConsole.WriteLine();
        }
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

    private static string GetPreviewFromResults(FileAnalysisEntry entry)
    {
        if (
            entry.AnalysisResults.TryGetValue(PreviewHandler.ResultKey, out var previewObj)
            && previewObj is string preview
        )
        {
            return preview;
        }

        return string.Empty;
    }
}

public class AnalyzeDebtSettings : CommandSettings
{
    [Description("Path to the repository (optional, defaults to current directory)")]
    [CommandArgument(0, "[REPOSITORY_PATH]")]
    public string? RepositoryPath { get; init; }
}
