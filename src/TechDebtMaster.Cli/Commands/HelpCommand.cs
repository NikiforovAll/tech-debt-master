using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Commands;

public class HelpCommand : Command<HelpCommand.Settings>
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

        // Display commands grouped by workflow
        AnsiConsole.MarkupLine("[bold underline]Available Commands:[/]");
        AnsiConsole.WriteLine();

        var table = new Table().Border(TableBorder.None).HideHeaders().AddColumn("").AddColumn("");

        // Repository Management Workflow
        table.AddRow(new Markup("[bold cyan]Repository Management[/]"), new Text(""));
        table.AddRow(
            new Markup("[green]repo[/]"),
            new Text("Repository management and indexing operations")
        );
        table.AddRow(
            new Markup("  [dim]├─[/] [green]index[/] [[path]]"),
            new Text("Index repostitory content")
        );
        table.AddRow(
            new Markup("  [dim]└─[/] [green]status[/] [[path]]"),
            new Text("Show status of previous analysis and repository changes")
        );
        table.AddRow(new Markup(""), new Text(""));

        // Debt Analysis Workflow
        table.AddRow(new Markup("[bold yellow]Debt Analysis[/]"), new Text(""));
        table.AddRow(
            new Markup("[green]debt[/]"),
            new Text("Technical debt analysis and reporting")
        );
        table.AddRow(
            new Markup("  [dim]├─[/] [green]analyze[/] [[path]]"),
            new Text("Perform debt analysis on all indexed files")
        );
        table.AddRow(
            new Markup("  [dim]├─[/] [green]show[/] [[path]]"),
            new Text("Show technical debt statistics in a tree structure grouped by tags")
        );
        table.AddRow(
            new Markup("  [dim]├─[/] [green]view[/] [[path]]"),
            new Text("View detailed content of specific technical debt items")
        );
        table.AddRow(
            new Markup("  [dim]├─[/] [green]report[/] [[path]]"),
            new Text("Generate an interactive HTML report of technical debt")
        );
        table.AddRow(
            new Markup("  [dim]└─[/] [green]import[/] [[report-file]]"),
            new Text("Import modified HTML report to update analysis data")
        );
        table.AddRow(new Markup(""), new Text(""));

        // System Management
        table.AddRow(new Markup("[bold green]System Management[/]"), new Text(""));
        table.AddRow(new Markup("[green]config[/]"), new Text("Manage configuration settings"));
        table.AddRow(
            new Markup("  [dim]├─[/] [green]show[/]"),
            new Text("Display current configuration")
        );
        table.AddRow(
            new Markup("  [dim]└─[/] [green]set[/] [[key]] [[value]]"),
            new Text("Set a configuration value")
        );
        table.AddRow(new Markup("[green]prompts[/]"), new Text("Manage prompt templates"));
        table.AddRow(new Markup("  [dim]├─[/] [green]edit[/]"), new Text("Edit a prompt template"));
        table.AddRow(
            new Markup("  [dim]├─[/] [green]restore[/]"),
            new Text("Restore prompt templates to default state")
        );
        table.AddRow(
            new Markup("  [dim]└─[/] [green]set-default[/]"),
            new Text("Set the default prompt template")
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
        table.AddRow(
            new Markup("[green]clean[/]"),
            new Text("Remove the .tdm folder from the current directory")
        );
        table.AddRow(new Markup("[green]help[/]"), new Text("Show this detailed help information"));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Display workflow examples
        AnsiConsole.MarkupLine("[bold underline]Typical Workflow:[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold cyan]1. Setup & Configuration[/]");
        AnsiConsole.MarkupLine("  [dim]# Set AI API key[/]");
        AnsiConsole.MarkupLine("  [blue]tdm config set ai.key <your-api-key>[/]");
        AnsiConsole.MarkupLine("  [dim]# Set default repository path (optional)[/]");
        AnsiConsole.MarkupLine("  [blue]tdm config set default.repository /home/user/my-repo[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold cyan]2. Repository Management[/]");
        AnsiConsole.MarkupLine("  [dim]# Index repository for analysis[/]");
        AnsiConsole.MarkupLine("  [blue]tdm repo index[/]");
        AnsiConsole.MarkupLine("  [dim]# Check repository status[/]");
        AnsiConsole.MarkupLine("  [blue]tdm repo status[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold yellow]3. Debt Analysis[/]");
        AnsiConsole.MarkupLine("  [dim]# Analyze for technical debt[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt analyze[/]");
        AnsiConsole.MarkupLine("  [dim]# Analyze only latest changes[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt analyze --latest[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold yellow]4. Results & Reporting[/]");
        AnsiConsole.MarkupLine("  [dim]# Show debt statistics in tree structure[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt show[/]");
        AnsiConsole.MarkupLine("  [dim]# View detailed debt items (interactive)[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt view[/]");
        AnsiConsole.MarkupLine("  [dim]# Export specific debt item as JSON[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt view --id \"UserController.cs:TD001\" --json[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold yellow]5. HTML Report Generation & Management[/]");
        AnsiConsole.MarkupLine("  [dim]# Generate HTML report[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt report[/]");
        AnsiConsole.MarkupLine("  [dim]# Generate and open report in browser[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt report --output my-report.html --open[/]");
        AnsiConsole.MarkupLine("  [dim]# Import modified report (preview changes)[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt import modified-report.html[/]");
        AnsiConsole.MarkupLine("  [dim]# Apply changes from modified report[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt import modified-report.html --apply[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold green]Advanced Examples:[/]");
        AnsiConsole.MarkupLine("  [dim]# Filter by file type during indexing[/]");
        AnsiConsole.MarkupLine("  [blue]tdm repo index --include \"\\.cs$\"[/]");
        AnsiConsole.MarkupLine("  [dim]# Filter debt items by severity[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt show --severity Critical[/]");
        AnsiConsole.MarkupLine("  [dim]# Export all debt items as XML[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt view --xml[/]");
        AnsiConsole.MarkupLine("  [dim]# Generate report for specific file types only[/]");
        AnsiConsole.MarkupLine(
            "  [blue]tdm debt report --include \"\\.cs$\" --output csharp-debt.html[/]"
        );
        AnsiConsole.MarkupLine("  [dim]# Import report with verbose output[/]");
        AnsiConsole.MarkupLine("  [blue]tdm debt import report.html --verbose[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Run '[blue]tdm --help[/]' for command syntax help.");

        return 0;
    }

    public class Settings : CommandSettings { }
}
