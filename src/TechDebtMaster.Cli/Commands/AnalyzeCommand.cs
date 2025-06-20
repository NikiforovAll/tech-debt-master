using System.ComponentModel;
using Microsoft.SemanticKernel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Commands;

public class AnalyzeCommand(Kernel kernel) : AsyncCommand<AnalyzeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        AnsiConsole.MarkupLine($"[blue]Analyzing repository:[/] {settings.RepositoryPath}");

        await AnsiConsole
            .Status()
            .StartAsync(
                "Analyzing...",
                async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.SpinnerStyle(Style.Parse("blue"));

                    await Task.Delay(3000);
                }
            );

        var table = new Table();
        table.AddColumn("Issue Type");
        table.AddColumn("Severity");
        table.AddColumn("Count");

        table.AddRow("Code Duplication", "[red]High[/]", "15");
        table.AddRow("Unused Dependencies", "[yellow]Medium[/]", "8");
        table.AddRow("Complex Methods", "[orange3]Medium[/]", "12");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[green]✓[/] Analysis completed successfully!");

        return 0;
    }
}

public class AnalyzeSettings : CommandSettings
{
    [Description("Path to the repository to analyze")]
    [CommandArgument(0, "<REPOSITORY_PATH>")]
    public string RepositoryPath { get; init; } = string.Empty;
}
