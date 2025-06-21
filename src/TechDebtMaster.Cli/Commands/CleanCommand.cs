using Spectre.Console;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Commands;

public class CleanCommand : Command<CleanCommand.Settings>
{
    private const string IndexDirectoryName = ".tdm";

    public override int Execute(CommandContext context, Settings settings)
    {
        var indexDirectory = Path.Combine(Directory.GetCurrentDirectory(), IndexDirectoryName);

        if (!Directory.Exists(indexDirectory))
        {
            AnsiConsole.MarkupLine(
                $"[yellow] No {IndexDirectoryName} folder found in current directory.[/]"
            );
            return 0;
        }

        try
        {
            Directory.Delete(indexDirectory, recursive: true);
            AnsiConsole.MarkupLine($"[green] Successfully removed {IndexDirectoryName} folder.[/]");
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

    public class Settings : CommandSettings { }
}
