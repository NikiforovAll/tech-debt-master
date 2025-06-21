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
            new Markup("[green]analyze[/]"),
            new Text("Index and analyze repository for technical debt")
        );
        table.AddRow(
            new Markup("  [dim]├─[/] [green]index[/] [[path]]"),
            new Text("Index repository and analyze all files for technical debt")
        );
        table.AddRow(
            new Markup("  [dim]├─[/] [green]debt[/] [[path]]"),
            new Text("Perform debt analysis on all indexed files")
        );
        table.AddRow(
            new Markup("  [dim]├─[/] [green]show[/] [[path]]"),
            new Text("Show technical debt statistics in a tree structure grouped by tags")
        );
        table.AddRow(
            new Markup("  [dim]└─[/] [green]status[/] [[path]]"),
            new Text("Show status of previous analysis")
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
        table.AddRow(new Markup("[green]prompts[/]"), new Text("Manage prompt templates"));
        table.AddRow(new Markup("  [dim]├─[/] [green]edit[/]"), new Text("Edit a prompt template"));
        table.AddRow(
            new Markup("  [dim]└─[/] [green]restore[/]"),
            new Text("Restore prompt templates to default state")
        );

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Display examples
        AnsiConsole.MarkupLine("[bold underline]Examples:[/]");
        AnsiConsole.MarkupLine("  [dim]# Index and analyze all files in a repository[/]");
        AnsiConsole.MarkupLine("  [blue]tdm analyze index /home/user/my-repo[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Run debt analysis on all indexed files[/]");
        AnsiConsole.MarkupLine("  [blue]tdm analyze debt /home/user/my-repo[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Run debt analysis only on latest changes[/]");
        AnsiConsole.MarkupLine("  [blue]tdm analyze debt /home/user/my-repo --latest[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Check analysis status[/]");
        AnsiConsole.MarkupLine("  [blue]tdm analyze status /home/user/my-repo[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Show debt analysis in tree structure[/]");
        AnsiConsole.MarkupLine("  [blue]tdm analyze show /home/user/my-repo[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Set AI API key[/]");
        AnsiConsole.MarkupLine("  [blue]tdm config set ai.key [[<your-api-key>]][/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Get token limits for a model[/]");
        AnsiConsole.MarkupLine("  [blue]tdm dial limits[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Edit a prompt template[/]");
        AnsiConsole.MarkupLine("  [blue]tdm prompts edit[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Restore prompt templates to defaults[/]");
        AnsiConsole.MarkupLine("  [blue]tdm prompts restore[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Run '[blue]tdm --help[/]' for more information.");

        return 0;
    }

    public class Settings : CommandSettings { }
}
