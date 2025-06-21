using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Commands;

public class DefaultCommand : Command<DefaultCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        // Render ASCII art title
        var figlet = new FigletText("TechDebtMaster").Centered().Color(Color.Green);
        AnsiConsole.Write(figlet);

        // Add a divider
        AnsiConsole.Write(new Rule().RuleStyle("grey30"));
        AnsiConsole.WriteLine();

        // Display version
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        AnsiConsole.MarkupLine($"[dim]Version {version}[/]");
        AnsiConsole.WriteLine();

        // Display description
        AnsiConsole.MarkupLine("[yellow]Analyze and manage technical debt in your repositories[/]");
        AnsiConsole.WriteLine();

        // Display commands
        AnsiConsole.MarkupLine("[bold underline]Available Commands:[/]");
        AnsiConsole.WriteLine();

        var table = new Table().Border(TableBorder.None).HideHeaders().AddColumn("").AddColumn("");

        table.AddRow("[green]analyze[/] <path>", "Analyze a repository for technical debt");
        table.AddRow("[green]config[/]", "Manage configuration settings");
        table.AddRow("  [dim]├─[/] [green]show[/]", "Display current configuration");
        table.AddRow("  [dim]└─[/] [green]set[/] <key> <value>", "Set a configuration value");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Display examples
        AnsiConsole.MarkupLine("[bold underline]Examples:[/]");
        AnsiConsole.MarkupLine("  [dim]# Analyze a repository[/]");
        AnsiConsole.MarkupLine("  [blue]TechDebtMaster.Cli.dll analyze /path/to/repo[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Set AI API key[/]");
        AnsiConsole.MarkupLine("  [blue]TechDebtMaster.Cli.dll config set ai.key your-api-key[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(
            "Run '[blue]TechDebtMaster.Cli.dll --help[/]' for more information."
        );

        return 0;
    }

    public class Settings : CommandSettings { }
}
