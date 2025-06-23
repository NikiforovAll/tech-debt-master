using System.ComponentModel;
using Spectre.Console.Cli;

namespace TechDebtMaster.Cli.Commands;

public class DebtReportCommandSettings : CommandSettings
{
    [Description("Path to the repository (optional, uses default.repository or current directory)")]
    [CommandArgument(0, "[REPOSITORY_PATH]")]
    public string? RepositoryPath { get; init; }

    [Description("Output file path for the HTML report")]
    [CommandOption("-o|--output")]
    public string OutputPath { get; init; } = "tech-debt-report.html";

    [Description(
        "Regex pattern to include files (only files matching this pattern will be included)"
    )]
    [CommandOption("--include")]
    public string? IncludePattern { get; init; }

    [Description("Regex pattern to exclude files (files matching this pattern will be excluded)")]
    [CommandOption("--exclude")]
    public string? ExcludePattern { get; init; }

    [Description("Include repository name in the report")]
    [CommandOption("--repo-name")]
    public string? RepositoryName { get; init; }

    [Description("Open the report in the default browser after generation")]
    [CommandOption("--open")]
    public bool OpenInBrowser { get; init; }
}
