using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;
using TechDebtMaster.Cli.Services.Analysis;
using TechDebtMaster.Cli.Services.Analysis.Handlers;

namespace TechDebtMaster.Cli.Commands;

public class DebtImportCommand(
    IAnalysisService analysisService,
    IConfigurationService configurationService,
    IReportStateExtractor reportStateExtractor
) : AsyncCommand<DebtImportCommandSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        DebtImportCommandSettings settings
    )
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Validate report file exists
        if (!File.Exists(settings.ReportPath))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Report file '{settings.ReportPath.EscapeMarkup()}' does not exist."
            );
            return 1;
        }

        // Determine repository path
        var repositoryPath = settings.RepositoryPath;
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            var defaultRepo = await configurationService.GetAsync("default.repository");
            repositoryPath = !string.IsNullOrWhiteSpace(defaultRepo)
                ? defaultRepo
                : Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(repositoryPath))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Repository path '{repositoryPath.EscapeMarkup()}' does not exist."
            );
            return 1;
        }

        // Load current analysis
        var currentAnalysis = await analysisService.LoadAnalysisReportAsync(repositoryPath);
        if (currentAnalysis == null)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No analysis data found for repository:[/] {repositoryPath.EscapeMarkup()}"
            );
            AnsiConsole.MarkupLine(
                "[yellow]Run 'debt analyze <path>' first to generate analysis data.[/]"
            );
            return 0;
        }

        // Extract state from HTML report
        AnsiConsole.MarkupLine("[blue]Extracting state from HTML report...[/]");
        var reportState = await reportStateExtractor.ExtractStateAsync(settings.ReportPath);

        // Calculate changes
        var importAnalysis = CalculateImportChanges(currentAnalysis, reportState);

        // Show detailed analysis
        DisplayImportAnalysis(importAnalysis, settings.Verbose);

        if (!settings.Apply)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(
                "[yellow]This was a dry run. To apply changes, use --apply flag.[/]"
            );
            AnsiConsole.MarkupLine(
                $"[dim]Command: debt import \"{settings.ReportPath.EscapeMarkup()}\" --apply[/]"
            );
            return 0;
        }

        // Apply changes
        if (importAnalysis.TotalItemsToRemove > 0)
        {
            AnsiConsole.WriteLine();
            if (!AnsiConsole.Confirm(
                $"[yellow]Are you sure you want to remove {importAnalysis.TotalItemsToRemove} debt items?[/]"
            ))
            {
                AnsiConsole.MarkupLine("[yellow]Import cancelled.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine("[blue]Applying changes...[/]");
            await analysisService.ImportAnalysisReportAsync(repositoryPath, importAnalysis.UpdatedAnalysis);
            AnsiConsole.MarkupLine("[green]Import completed successfully![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No changes to apply.[/]");
        }

        return 0;
    }

    private static ImportAnalysis CalculateImportChanges(
        AnalysisReport currentAnalysis,
        ReportState reportState
    )
    {
        var updatedFileHistories = new Dictionary<string, FileAnalysisHistory>();
        var changes = new List<FileImportChange>();
        int totalItemsRemoved = 0;

        foreach (var (filePath, fileHistory) in currentAnalysis.FileHistories)
        {
            if (!fileHistory.Current.AnalysisResults.TryGetValue(
                TechDebtAnalysisHandler.ResultKey,
                out var resultObj
            ))
            {
                // Keep files without tech debt analysis unchanged
                updatedFileHistories[filePath] = fileHistory;
                continue;
            }

            var techDebtResult = DeserializeTechDebtResult(resultObj);
            if (techDebtResult?.Items == null || techDebtResult.Items.Count == 0)
            {
                // Keep files without debt items unchanged
                updatedFileHistories[filePath] = fileHistory;
                continue;
            }

            // Filter items based on report state
            var remainingItems = techDebtResult.Items
                .Where(item => reportState.IsItemActive(filePath, item.Id))
                .ToList();

            var removedItems = techDebtResult.Items
                .Where(item => !reportState.IsItemActive(filePath, item.Id))
                .ToList();

            if (remainingItems.Count > 0)
            {
                // Update with remaining items
                var updatedResult = new TechDebtAnalysisResult { Items = remainingItems };
                var updatedResults = new Dictionary<string, object>(fileHistory.Current.AnalysisResults)
                {
                    [TechDebtAnalysisHandler.ResultKey] = updatedResult
                };

                var updatedFileHistory = new FileAnalysisHistory
                {
                    FilePath = filePath,
                    Current = new FileAnalysisEntry
                    {
                        Timestamp = fileHistory.Current.Timestamp,
                        AnalysisResults = updatedResults,
                        FileHash = fileHistory.Current.FileHash
                    },
                    Previous = fileHistory.Previous
                };

                updatedFileHistories[filePath] = updatedFileHistory;
            }
            // If no remaining items, don't include the file in updated analysis

            if (removedItems.Count > 0 || remainingItems.Count != techDebtResult.Items.Count)
            {
                changes.Add(new FileImportChange
                {
                    FilePath = filePath,
                    OriginalCount = techDebtResult.Items.Count,
                    RemainingCount = remainingItems.Count,
                    RemovedItems = removedItems
                });

                totalItemsRemoved += removedItems.Count;
            }
        }

        var updatedAnalysis = new AnalysisReport
        {
            Timestamp = DateTime.UtcNow,
            FileHistories = updatedFileHistories
        };

        return new ImportAnalysis
        {
            UpdatedAnalysis = updatedAnalysis,
            Changes = changes,
            TotalItemsToRemove = totalItemsRemoved,
            TotalRemainingItems = updatedFileHistories.Values
                .SelectMany(fh => ExtractDebtItems(fh).Items ?? [])
                .Count()
        };
    }

    private static TechDebtAnalysisResult? DeserializeTechDebtResult(object resultObj)
    {
        try
        {
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            };

            var json = System.Text.Json.JsonSerializer.Serialize(resultObj, jsonOptions);
            return System.Text.Json.JsonSerializer.Deserialize<TechDebtAnalysisResult>(
                json,
                jsonOptions
            );
        }
        catch
        {
            return null;
        }
    }

    private static TechDebtAnalysisResult ExtractDebtItems(FileAnalysisHistory fileHistory)
    {
        if (fileHistory.Current.AnalysisResults.TryGetValue(
            TechDebtAnalysisHandler.ResultKey,
            out var resultObj
        ))
        {
            return DeserializeTechDebtResult(resultObj) ?? new TechDebtAnalysisResult { Items = [] };
        }

        return new TechDebtAnalysisResult { Items = [] };
    }

    private static void DisplayImportAnalysis(ImportAnalysis analysis, bool verbose)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold blue]TECH DEBT IMPORT ANALYSIS[/]");
        AnsiConsole.MarkupLine("".PadRight(50, '='));

        // Summary statistics
        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.Border(TableBorder.Rounded);

        table.AddRow("Items to remove", $"[red]{analysis.TotalItemsToRemove}[/]");
        table.AddRow("Items remaining", $"[green]{analysis.TotalRemainingItems}[/]");
        table.AddRow("Files affected", $"{analysis.Changes.Count}");
        table.AddRow("Files removed", $"{analysis.Changes.Count(c => c.RemainingCount == 0)}");

        AnsiConsole.Write(table);

        if (verbose && analysis.Changes.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]DETAILED CHANGES BY FILE:[/]");
            AnsiConsole.MarkupLine("".PadRight(50, '-'));

            foreach (var change in analysis.Changes.OrderBy(c => c.FilePath))
            {
                var icon = change.RemainingCount == 0 ? "🗑️" : "📁";
                var status = change.RemainingCount == 0 
                    ? "[red]COMPLETE REMOVAL[/]" 
                    : $"[yellow]{change.OriginalCount} → {change.RemainingCount}[/]";

                AnsiConsole.MarkupLine($"{icon} [bold]{change.FilePath.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine($"   {status} (removing {change.RemovedItems.Count} items)");

                foreach (var item in change.RemovedItems)
                {
                    var severityColor = item.Severity.ToString().ToLower() switch
                    {
                        "critical" => "red",
                        "high" => "orange3",
                        "medium" => "yellow",
                        "low" => "green",
                        _ => "white"
                    };

                    AnsiConsole.MarkupLine(
                        $"   ✗ [dim]{item.Id.EscapeMarkup()}[/] [{severityColor}]{item.Severity}[/] {item.Summary.EscapeMarkup()}"
                    );
                }

                AnsiConsole.WriteLine();
            }
        }
    }
}

public class ImportAnalysis
{
    public AnalysisReport UpdatedAnalysis { get; init; } = new();
    public List<FileImportChange> Changes { get; init; } = [];
    public int TotalItemsToRemove { get; init; }
    public int TotalRemainingItems { get; init; }
}

public class FileImportChange
{
    public string FilePath { get; init; } = string.Empty;
    public int OriginalCount { get; init; }
    public int RemainingCount { get; init; }
    public List<TechnicalDebtItem> RemovedItems { get; init; } = [];
}