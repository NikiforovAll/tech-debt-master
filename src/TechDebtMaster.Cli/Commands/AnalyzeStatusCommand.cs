using System.ComponentModel;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class AnalyzeStatusCommand(
    IIndexStorageService storageService,
    IAnalysisService analysisService
) : AsyncCommand<AnalyzeStatusSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        AnalyzeStatusSettings settings
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

        try
        {
            // Load latest index data
            var indexData = await storageService.LoadLatestIndexAsync(repositoryPath);
            if (indexData == null)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]No analysis data found for repository:[/] {repositoryPath}"
                );
                AnsiConsole.MarkupLine(
                    "[yellow]Run 'analyze index <path>' first to generate analysis data.[/]"
                );
                return 0;
            }

            // Load analysis report
            var analysisReport = await analysisService.LoadAnalysisReportAsync(repositoryPath);

            AnsiConsole.MarkupLine($"[green]Analysis Status for:[/] {repositoryPath}");
            AnsiConsole.MarkupLine(
                $"[blue]Last analyzed:[/] {indexData.Timestamp:yyyy-MM-dd HH:mm:ss} UTC"
            );
            AnsiConsole.WriteLine();

            // Create and display statistics table
            var table = new Table();
            table.AddColumn("[bold]Metric[/]");
            table.AddColumn("[bold]Value[/]");
            table.Border(TableBorder.Rounded);

            var summary = indexData.Summary;
            table.AddRow("Total files", summary.TotalFiles.ToString(CultureInfo.InvariantCulture));

            if (
                indexData.Summary.ChangedFiles.Count != 0
                || indexData.Summary.NewFiles.Count != 0
                || indexData.Summary.DeletedFiles.Count != 0
            )
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

            // Add analysis statistics if available
            if (analysisReport != null)
            {
                var totalAnalyzed = analysisReport.FileHistories.Count;
                var changedAnalyses = analysisReport.FileHistories.Count(kv =>
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

            // Show changed files if any
            if (
                summary.NewFiles.Count != 0
                || summary.ChangedFiles.Count != 0
                || summary.DeletedFiles.Count != 0
            )
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Recent Changes:[/]");

                if (summary.NewFiles.Count != 0)
                {
                    AnsiConsole.MarkupLine($"[green]New files ({summary.NewFiles.Count}):[/]");
                    foreach (var file in summary.NewFiles.Take(5))
                    {
                        AnsiConsole.MarkupLine($"  [green]+[/] {file}");
                    }
                    if (summary.NewFiles.Count > 5)
                    {
                        AnsiConsole.MarkupLine(
                            $"  [dim]... and {summary.NewFiles.Count - 5} more[/]"
                        );
                    }
                }

                if (summary.ChangedFiles.Count != 0)
                {
                    AnsiConsole.MarkupLine(
                        $"[yellow]Changed files ({summary.ChangedFiles.Count}):[/]"
                    );
                    foreach (var file in summary.ChangedFiles.Take(5))
                    {
                        AnsiConsole.MarkupLine($"  [yellow]~[/] {file}");
                    }
                    if (summary.ChangedFiles.Count > 5)
                    {
                        AnsiConsole.MarkupLine(
                            $"  [dim]... and {summary.ChangedFiles.Count - 5} more[/]"
                        );
                    }
                }

                if (summary.DeletedFiles.Count != 0)
                {
                    AnsiConsole.MarkupLine(
                        $"[red]Deleted files ({summary.DeletedFiles.Count}):[/]"
                    );
                    foreach (var file in summary.DeletedFiles.Take(5))
                    {
                        AnsiConsole.MarkupLine($"  [red]-[/] {file}");
                    }
                    if (summary.DeletedFiles.Count > 5)
                    {
                        AnsiConsole.MarkupLine(
                            $"  [dim]... and {summary.DeletedFiles.Count - 5} more[/]"
                        );
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Failed to load analysis data: {ex.Message}");
            return 1;
        }
    }
}

public class AnalyzeStatusSettings : CommandSettings
{
    [Description("Path to the repository (optional, defaults to current directory)")]
    [CommandArgument(0, "[REPOSITORY_PATH]")]
    public string? RepositoryPath { get; init; }
}
