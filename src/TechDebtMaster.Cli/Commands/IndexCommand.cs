using System.ComponentModel;
using Microsoft.SemanticKernel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class IndexCommand(Kernel kernel, IRepositoryIndexService indexService)
    : AsyncCommand<IndexSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, IndexSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Directory.Exists(settings.RepositoryPath))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Repository path '{settings.RepositoryPath}' does not exist."
            );
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Indexing repository:[/] {settings.RepositoryPath}");

        IndexResult? indexResult = null;

        try
        {
            await AnsiConsole
                .Status()
                .StartAsync(
                    "Running repomix and parsing output...",
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
                AnsiConsole.MarkupLine(
                    "[red]Error:[/] Failed to index repository."
                );
                return 1;
            }

            if (string.IsNullOrEmpty(indexResult.FileSummary))
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Warning:[/] No file_summary section found in repomix output."
                );
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Repository indexed successfully!");
            
            var summary = indexResult.ChangeSummary;
            AnsiConsole.MarkupLine($"[dim]Total files: {summary.TotalFiles}[/]");
            
            if (indexResult.HasChanges)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Changes detected:[/]");
                
                if (summary.NewFiles.Any())
                {
                    AnsiConsole.MarkupLine($"[green]+ {summary.NewFiles.Count} new files[/]");
                    if (settings.ShowDetails)
                    {
                        foreach (var file in summary.NewFiles.Take(10))
                        {
                            AnsiConsole.MarkupLine($"  [green]+ {file}[/]");
                        }
                        if (summary.NewFiles.Count > 10)
                        {
                            AnsiConsole.MarkupLine($"  [dim]... and {summary.NewFiles.Count - 10} more[/]");
                        }
                    }
                }
                
                if (summary.ChangedFiles.Any())
                {
                    AnsiConsole.MarkupLine($"[yellow]~ {summary.ChangedFiles.Count} changed files[/]");
                    if (settings.ShowDetails)
                    {
                        foreach (var file in summary.ChangedFiles.Take(10))
                        {
                            AnsiConsole.MarkupLine($"  [yellow]~ {file}[/]");
                        }
                        if (summary.ChangedFiles.Count > 10)
                        {
                            AnsiConsole.MarkupLine($"  [dim]... and {summary.ChangedFiles.Count - 10} more[/]");
                        }
                    }
                }
                
                if (summary.DeletedFiles.Any())
                {
                    AnsiConsole.MarkupLine($"[red]- {summary.DeletedFiles.Count} deleted files[/]");
                    if (settings.ShowDetails)
                    {
                        foreach (var file in summary.DeletedFiles.Take(10))
                        {
                            AnsiConsole.MarkupLine($"  [red]- {file}[/]");
                        }
                        if (summary.DeletedFiles.Count > 10)
                        {
                            AnsiConsole.MarkupLine($"  [dim]... and {summary.DeletedFiles.Count - 10} more[/]");
                        }
                    }
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]No changes detected since last index.[/]");
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

public class IndexSettings : CommandSettings
{
    [Description("Path to the repository to index")]
    [CommandArgument(0, "<REPOSITORY_PATH>")]
    public string RepositoryPath { get; init; } = string.Empty;

    [Description("Show detailed file changes")]
    [CommandOption("-d|--details")]
    public bool ShowDetails { get; init; }
}
