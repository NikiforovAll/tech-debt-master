using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services;

namespace TechDebtMaster.Cli.Commands;

[Description("Display current configuration")]
public class ConfigShowCommand(IConfigurationService configurationService)
    : AsyncCommand<ConfigShowCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var config = await configurationService.GetAllAsync();

        if (config.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No configuration values set.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("[bold]Key[/]");
        table.AddColumn("[bold]Value[/]");
        table.Border(TableBorder.Rounded);

        foreach (var (key, value) in config.OrderBy(x => x.Key))
        {
            var displayValue = key.Contains("key", StringComparison.OrdinalIgnoreCase)
                ? MaskValue(value)
                : value;
            table.AddRow(key, displayValue);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static string MaskValue(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= 8)
        {
            return "********";
        }
        return string.Concat(value.AsSpan(0, 4), "****", value.AsSpan(value.Length - 4));
    }

    public class Settings : CommandSettings { }
}
