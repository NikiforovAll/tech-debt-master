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

        string fileSummary = "";

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

                        fileSummary = await indexService.IndexRepositoryAsync(
                            settings.RepositoryPath
                        );
                    }
                );

            if (string.IsNullOrEmpty(fileSummary))
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Warning:[/] No file_summary section found in repomix output."
                );
                return 0;
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Repository indexed successfully!");
            AnsiConsole.MarkupLine(
                $"[dim]Extracted {fileSummary.Length} characters of file summary data[/]"
            );

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
}
