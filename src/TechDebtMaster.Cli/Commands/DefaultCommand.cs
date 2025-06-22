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
            "[yellow]A comprehensive tool for analyzing and managing technical debt in your codebase.[/]"
        );
        AnsiConsole.WriteLine();

        // Display main command categories
        AnsiConsole.MarkupLine("[bold underline]Main Commands:[/]");
        AnsiConsole.WriteLine();

        var table = new Table().Border(TableBorder.None).HideHeaders().AddColumn("").AddColumn("");

        table.AddRow(new Markup("[green]repo[/]"), new Text("Repository management and indexing"));
        table.AddRow(
            new Markup("[green]debt[/]"),
            new Text("Technical debt analysis and reporting")
        );
        table.AddRow(new Markup("[green]config[/]"), new Text("Configuration management"));
        table.AddRow(new Markup("[green]prompts[/]"), new Text("Prompt template management"));
        table.AddRow(new Markup("[green]dial[/]"), new Text("DIAL API operations"));
        table.AddRow(new Markup("[green]clean[/]"), new Text("Clean up .tdm folder"));
        table.AddRow(new Markup("[green]help[/]"), new Text("Show detailed help with examples"));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Quick start
        AnsiConsole.MarkupLine("[bold underline]Quick Start:[/]");
        AnsiConsole.MarkupLine("  [blue]tdm repo index[/]     [dim]# Index your repository[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt analyze[/]   [dim]# Analyze technical debt[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt show[/]      [dim]# View results[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(
            "Run '[blue]tdm help[/]' for detailed usage examples and '[blue]tdm --help[/]' for command syntax."
        );

        return 0;
    }

    public class Settings : CommandSettings { }
}
