using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

public class AnalyzeStatusCommand(
    IIndexStorageService storageService,
    IConfigurationService configurationService
) : AsyncCommand<AnalyzeStatusSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        AnalyzeStatusSettings settings
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

        AnsiConsole.MarkupLine($"[green]Analysis Status for:[/] {repositoryPath}");
        AnsiConsole.MarkupLine(
            $"[blue]Last analyzed:[/] {indexData.Timestamp:yyyy-MM-dd HH:mm:ss} UTC"
        );

        var summary = indexData.Summary;
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
                    AnsiConsole.MarkupLine($"  [dim]... and {summary.NewFiles.Count - 5} more[/]");
                }
            }

            if (summary.ChangedFiles.Count != 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Changed files ({summary.ChangedFiles.Count}):[/]");
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
                AnsiConsole.MarkupLine($"[red]Deleted files ({summary.DeletedFiles.Count}):[/]");
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
}

public class AnalyzeStatusSettings : CommandSettings
{
    [Description("Path to the repository (optional, uses default.repository or current directory)")]
    [CommandArgument(0, "[REPOSITORY_PATH]")]
    public string? RepositoryPath { get; init; }
}
