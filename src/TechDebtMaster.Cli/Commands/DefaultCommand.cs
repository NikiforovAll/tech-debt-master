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
        AnsiConsole.MarkupLine(
            "[yellow]A comprehensive tool for analyzing and managing technical debt in your codebase. Track code quality metrics, identify problematic patterns, and get actionable insights to improve your project's maintainability.[/]"
        );
        AnsiConsole.WriteLine();

        // Display commands
        AnsiConsole.MarkupLine("[bold underline]Available Commands:[/]");
        AnsiConsole.WriteLine();

        var table = new Table().Border(TableBorder.None).HideHeaders().AddColumn("").AddColumn("");

        table.AddRow(
            new Markup("[green]analyze[/] [[path]]"),
            new Text("Analyze repository for technical debt")
        );
        table.AddRow(
            new Markup("  [dim]├─[/] [green]run[/] [[path>]]"),
            new Text("Analyze a repository for technical debt")
        );
        table.AddRow(
            new Markup("  [dim]└─[/] [green]status[/] [[path]]"),
            new Text("Show status of previous analysis")
        );
        table.AddRow(
            new Markup("[green]clean[/]"),
            new Text("Remove the .tdm folder from the current directory")
        );
        table.AddRow(new Markup("[green]config[/]"), new Text("Manage configuration settings"));
        table.AddRow(
            new Markup("  [dim]├─[/] [green]show[/]"),
            new Text("Display current configuration")
        );
        table.AddRow(
            new Markup("  [dim]└─[/] [green]set[/] [[key]] [[value]]"),
            new Text("Set a configuration value")
        );
        table.AddRow(new Markup("[green]dial[/]"), new Text("DIAL API operations"));
        table.AddRow(
            new Markup("  [dim]├─[/] [green]list-models[/]"),
            new Text("List all available DIAL models")
        );
        table.AddRow(
            new Markup("  [dim]└─[/] [green]limits[/]"),
            new Text("Get token limits for a specific model")
        );

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Display examples
        AnsiConsole.MarkupLine("[bold underline]Examples:[/]");
        AnsiConsole.MarkupLine("  [dim]# Analyze a repository[/]");
        AnsiConsole.MarkupLine("  [blue]TechDebtMaster.Cli.dll analyze index /home/user/my-repo[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Check analysis status[/]");
        AnsiConsole.MarkupLine("  [blue]TechDebtMaster.Cli.dll analyze status /home/user/my-repo[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Set AI API key[/]");
        AnsiConsole.MarkupLine(
            "  [blue]TechDebtMaster.Cli.dll config set ai.key [[<your-api-key>]][/]"
        );
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Get token limits for a model[/]");
        AnsiConsole.MarkupLine(
            "  [blue]TechDebtMaster.Cli.dll dial limits[/]"
        );
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(
            "Run '[blue]TechDebtMaster.Cli.dll --help[/]' for more information."
        );

        return 0;
    }

    public class Settings : CommandSettings { }
}
