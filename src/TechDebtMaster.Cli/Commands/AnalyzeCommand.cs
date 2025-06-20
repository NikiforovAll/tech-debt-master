using System.ComponentModel;
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

        AnsiConsole.MarkupLine($"[green]Analyzing repository:[/] {settings.RepositoryPath}");

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
                            settings.RepositoryPath
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
            AnsiConsole.WriteLine();

            // Create a tree to display results
            var tree = new Tree("[bold]Analysis Results[/]");

            // Add statistics node
            var statsNode = tree.AddNode("[blue]Statistics[/]");
            var summary = indexResult.ChangeSummary;
            statsNode.AddNode($"Total files: {summary.TotalFiles}");

            if (indexResult.HasChanges)
            {
                statsNode.AddNode($"[green]New files: {summary.NewFiles.Count}[/]");
                statsNode.AddNode($"[yellow]Changed files: {summary.ChangedFiles.Count}[/]");
                statsNode.AddNode($"[red]Deleted files: {summary.DeletedFiles.Count}[/]");
            }
            else
            {
                statsNode.AddNode("[dim]No changes detected since last analysis[/]");
            }

            // Add file changes if showing details
            if (settings.ShowDetails && indexResult.HasChanges)
            {
                if (summary.NewFiles.Count != 0)
                {
                    var newFilesNode = tree.AddNode(
                        $"[green]New Files ({summary.NewFiles.Count})[/]"
                    );
                    foreach (var file in summary.NewFiles.Take(10))
                    {
                        newFilesNode.AddNode($"[green]{file}[/]");
                    }
                    if (summary.NewFiles.Count > 10)
                    {
                        newFilesNode.AddNode($"[dim]... and {summary.NewFiles.Count - 10} more[/]");
                    }
                }

                if (summary.ChangedFiles.Count != 0)
                {
                    var changedFilesNode = tree.AddNode(
                        $"[yellow]Changed Files ({summary.ChangedFiles.Count})[/]"
                    );
                    foreach (var file in summary.ChangedFiles.Take(10))
                    {
                        changedFilesNode.AddNode($"[yellow]{file}[/]");
                    }
                    if (summary.ChangedFiles.Count > 10)
                    {
                        changedFilesNode.AddNode(
                            $"[dim]... and {summary.ChangedFiles.Count - 10} more[/]"
                        );
                    }
                }

                if (summary.DeletedFiles.Count != 0)
                {
                    var deletedFilesNode = tree.AddNode(
                        $"[red]Deleted Files ({summary.DeletedFiles.Count})[/]"
                    );
                    foreach (var file in summary.DeletedFiles.Take(10))
                    {
                        deletedFilesNode.AddNode($"[red]{file}[/]");
                    }
                    if (summary.DeletedFiles.Count > 10)
                    {
                        deletedFilesNode.AddNode(
                            $"[dim]... and {summary.DeletedFiles.Count - 10} more[/]"
                        );
                    }
                }
            }

            // Add analysis statistics if available
            if (indexResult.AnalysisReport != null)
            {
                var analysisNode = tree.AddNode("[purple]Analysis Summary[/]");

                var totalAnalyzed = indexResult.AnalysisReport.FileHistories.Count;
                var changedAnalyses = indexResult.AnalysisReport.FileHistories.Count(kv =>
                    kv.Value.Previous != null
                    && kv.Value.Current.Preview != kv.Value.Previous.Preview
                );

                analysisNode.AddNode($"Files analyzed: {totalAnalyzed}");
                if (changedAnalyses > 0)
                {
                    analysisNode.AddNode(
                        $"[yellow]Files with content changes: {changedAnalyses}[/]"
                    );
                }
            }

            AnsiConsole.Write(tree);

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

public class AnalyzeSettings : CommandSettings
{
    [Description("Path to the repository to analyze")]
    [CommandArgument(0, "<REPOSITORY_PATH>")]
    public string RepositoryPath { get; init; } = string.Empty;

    [Description("Show detailed file changes")]
    [CommandOption("-d|--details")]
    public bool ShowDetails { get; init; }
}
