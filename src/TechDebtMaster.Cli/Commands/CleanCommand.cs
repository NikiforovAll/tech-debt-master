using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Commands;

public class CleanCommand : Command<CleanCommand.Settings>
{
    private const string IndexDirectoryName = ".tdm";

    public override int Execute(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Validate scope parameter
        var validScopes = new[] { "all", "analysis" };
        if (!validScopes.Contains(settings.Scope.ToLowerInvariant()))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error:[/] Invalid scope '{settings.Scope}'. Valid options are: {string.Join(", ", validScopes)}"
            );
            return 1;
        }

        var normalizedScope = settings.Scope.ToLowerInvariant();
        var indexDirectory = Path.Combine(Directory.GetCurrentDirectory(), IndexDirectoryName);

        if (!Directory.Exists(indexDirectory))
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No {IndexDirectoryName} folder found in current directory.[/]"
            );
            return 0;
        }

        try
        {
            switch (normalizedScope)
            {
                case "all":
                    return CleanAll(indexDirectory);
                case "analysis":
                    return CleanAnalysis(indexDirectory);
                default:
                    AnsiConsole.MarkupLine($"[red]Error:[/] Unhandled scope: {normalizedScope}");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during cleanup: {ex.Message}[/]");
            return 1;
        }
    }

    private static int CleanAll(string indexDirectory)
    {
        try
        {
            Directory.Delete(indexDirectory, recursive: true);
            AnsiConsole.MarkupLine(
                $"[green]✓[/] Successfully removed all data from {IndexDirectoryName} folder."
            );
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[red]Error removing {IndexDirectoryName} folder: {ex.Message}[/]"
            );
            return 1;
        }
    }

    private static int CleanAnalysis(string indexDirectory)
    {
        var techDebtDirectory = Path.Combine(indexDirectory, "techdebt");

        if (!Directory.Exists(techDebtDirectory))
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No tech debt analysis data found in {IndexDirectoryName}/techdebt folder.[/]"
            );
            return 0;
        }

        try
        {
            Directory.Delete(techDebtDirectory, recursive: true);
            AnsiConsole.MarkupLine(
                $"[green]✓[/] Successfully removed tech debt analysis data from {IndexDirectoryName}/techdebt folder."
            );
            AnsiConsole.MarkupLine(
                $"[dim]Repository index data preserved in {IndexDirectoryName} folder.[/]"
            );
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error removing tech debt analysis data: {ex.Message}[/]");
            return 1;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [Description(
            "Scope of cleaning operation. 'all' removes everything, 'analysis' removes only tech debt analysis files"
        )]
        [CommandOption("--scope")]
        [DefaultValue("all")]
        public string Scope { get; init; } = "all";
    }
}
