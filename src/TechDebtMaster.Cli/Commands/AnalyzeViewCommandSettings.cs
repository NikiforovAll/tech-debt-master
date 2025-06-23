using System.ComponentModel;
using Spectre.Console.Cli;
using TechDebtMaster.Cli.Services.Analysis;

namespace TechDebtMaster.Cli.Commands;

public class AnalyzeViewCommandSettings : CommandSettings
{
    [Description("Path to the repository (optional, uses default.repository or current directory)")]
    [CommandArgument(0, "[REPOSITORY_PATH]")]
    public string? RepositoryPath { get; init; }

    [Description(
        "Regex pattern to include files (only debt items from files matching this pattern will be shown)"
    )]
    [CommandOption("--include")]
    public string? IncludePattern { get; init; }

    [Description(
        "Regex pattern to exclude files (debt items from files matching this pattern will be hidden)"
    )]
    [CommandOption("--exclude")]
    public string? ExcludePattern { get; init; }

    [Description("Filter by specific severity level (Critical, High, Medium, Low)")]
    [CommandOption("--severity")]
    public DebtSeverity? SeverityFilter { get; init; }

    [Description("Filter by specific debt tag (CodeSmell, Naming, Performance, etc.)")]
    [CommandOption("--tag")]
    public DebtTag? TagFilter { get; init; }

    [Description("Output as plain markdown format including all debt item data and content")]
    [CommandOption("--plain")]
    public bool PlainOutput { get; init; }

    [Description("Output as JSON format including all debt item data and content")]
    [CommandOption("--json")]
    public bool JsonOutput { get; init; }

    [Description("Output as XML format including all debt item data and content")]
    [CommandOption("--xml")]
    public bool XmlOutput { get; init; }

    [Description(
        "View specific debt item by file path and debt ID (format: '<pathToFile>:debtId')"
    )]
    [CommandOption("--id")]
    public string? DebtId { get; init; }

    [Description("Enable interactive mode to browse through items and return to selection")]
    [CommandOption("-i|--interactive")]
    public bool InteractiveMode { get; init; }
}
